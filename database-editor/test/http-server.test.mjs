import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { createEditorServer } from '../server/http-server.mjs';
import { MAX_BYTES } from '../server/strict-json.mjs';
import { schemas, temporaryStore } from './helpers.mjs';

const origin = 'http://localhost:8080';
async function application(t) {
  const state = await temporaryStore(t);
  const clientRoot = join(state.directory, 'client');
  await mkdir(clientRoot);
  await writeFile(join(clientRoot, 'index.html'), '<!doctype html><title>Editor test</title>');
  await writeFile(join(clientRoot, 'app.js'), 'export const editor = true;');
  await writeFile(join(state.directory, 'private.json'), '{"private":"outside-client"}');
  const options = { appearances: { skinTone: ['tone-1', 'tone-2'] } };
  const schema = schemas[1];
  const server = createEditorServer({ store: state.store, options, schema, clientRoot, allowedOrigins: [origin] });
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });
  t.after(() => new Promise((resolve, reject) => {
    server.close(error => error ? reject(error) : resolve());
    server.closeAllConnections();
  }));
  const base = `http://127.0.0.1:${server.address().port}`;
  return { ...state, options, schema, get: (path = '/editor/api/database', init) => fetch(base + path, init),
    put: (body, etag, headers = {}) => fetch(base + '/editor/api/database', { method: 'PUT', body,
      headers: { Origin: origin, 'Content-Type': 'application/json', ...(etag ? { 'If-Match': etag } : {}), ...headers } }) };
}

async function expectError(response, status, code) {
  assert.equal(response.status, status);
  assert.match(response.headers.get('content-type'), /^application\/json/);
  const payload = await response.json();
  assert.equal(payload.error.code, code);
  assert.equal(typeof payload.error.message, 'string');
  assert.ok(payload.error.message.length > 0);
  assert.equal(payload.error.stack, undefined);
  return payload.error;
}

test('API reads the current source with ETag, no-store and immutable schema/options responses', async t => {
  const app = await application(t);
  const response = await app.get();
  assert.equal(response.status, 200);
  assert.equal(response.headers.get('cache-control'), 'no-store');
  assert.equal(response.headers.get('x-content-type-options'), 'nosniff');
  assert.match(response.headers.get('content-security-policy'), /frame-ancestors 'none'/);
  const payload = await response.json();
  assert.equal(response.headers.get('etag'), payload.etag);
  assert.deepEqual(payload.document, (await app.store.read()).document);
  assert.deepEqual(await (await app.get('/editor/api/options')).json(), app.options);
  assert.deepEqual(await (await app.get('/editor/api/schema')).json(), app.schema);
  assert.deepEqual(await (await app.get('/editor/health')).json(), { status: 'ok' });
});

test('PUT saves a valid draft and reports server-assigned revision and ETag', async t => {
  const app = await application(t);
  const current = await app.store.read();
  const document = structuredClone(current.document);
  document.players[0].name = 'Alterado no editor';
  const response = await app.put(JSON.stringify(document), current.etag);
  assert.equal(response.status, 200);
  const saved = await response.json();
  assert.equal(saved.document.databaseRevision, current.document.databaseRevision + 1);
  assert.equal(saved.etag, response.headers.get('etag'));
  assert.notEqual(saved.etag, current.etag);
  assert.equal((await app.store.read()).document.players[0].name, 'Alterado no editor');
});

test('PUT requires an exact allowed Origin, JSON Content-Type and If-Match', async t => {
  const app = await application(t);
  const current = await app.store.read();
  const body = JSON.stringify(current.document);
  for (const forbidden of ['http://localhost:8080.evil.test', 'https://localhost:8080', 'null', ''])
    await expectError(await app.put(body, current.etag, { Origin: forbidden }), 403, 'origin_rejected');
  await expectError(await app.get('/editor/api/database', { method: 'PUT', body,
    headers: { 'Content-Type': 'application/json', 'If-Match': current.etag } }), 403, 'origin_rejected');
  for (const type of ['text/plain', 'application/jsonp', 'application/x-www-form-urlencoded'])
    await expectError(await app.put(body, current.etag, { 'Content-Type': type }), 415, 'content_type');
  await expectError(await app.put(body), 428, 'precondition_required');
  assert.equal(await readFile(app.path, 'utf8'), app.original);
});

test('PUT rejects malformed text, decimal tokens, duplicate keys and invalid UTF-8', async t => {
  const app = await application(t);
  const current = await app.store.read();
  const text = JSON.stringify(current.document);
  for (const body of ['{', text.replace(/"heightCm":\d+/, '"heightCm":180.0'), '{"schemaVersion":2,"schemaVersion":2}'])
    await expectError(await app.put(body, current.etag), 422, 'invalid_json');
  await expectError(await app.put(Buffer.from([0xff, 0xfe]), current.etag), 422, 'invalid_utf8');
  assert.equal(await readFile(app.path, 'utf8'), app.original);
});

test('PUT invalid references returns actionable field issues without changing disk', async t => {
  const app = await application(t);
  const current = await app.store.read();
  current.document.memberships[0].playerId = 'missing-id';
  const error = await expectError(await app.put(JSON.stringify(current.document), current.etag), 422, 'invalid_references');
  assert.ok(error.issues.some(issue => issue.path === '/memberships/0/playerId'));
  assert.equal(await readFile(app.path, 'utf8'), app.original);
});

test('PUT stale ETag returns a conflict and preserves the externally edited source', async t => {
  const app = await application(t);
  const current = await app.store.read();
  await writeFile(app.path, app.original + ' ');
  await expectError(await app.put(JSON.stringify(current.document), current.etag), 409, 'conflict');
  assert.equal(await readFile(app.path, 'utf8'), app.original + ' ');
});

test('PUT rejects request bodies above the one MiB limit with a JSON error', async t => {
  const app = await application(t);
  const current = await app.store.read();
  await expectError(await app.put('x'.repeat(MAX_BYTES + 1), current.etag), 413, 'document_too_large');
  assert.equal(await readFile(app.path, 'utf8'), app.original);
});

test('static routes serve only client assets and reject traversal, hidden files and source paths', async t => {
  const app = await application(t);
  const index = await app.get('/editor/');
  assert.equal(index.status, 200);
  assert.match(index.headers.get('content-type'), /^text\/html/);
  assert.match(await index.text(), /Editor test/);
  const script = await app.get('/editor/app.js', { method: 'HEAD' });
  assert.equal(script.status, 200);
  assert.equal(await script.text(), '');
  for (const path of ['/editor/%2e%2e/private.json', '/editor/%2e%2e%2fprivate.json', '/editor/%2eeditor-backups/previous.database.json',
    '/editor/server/index.mjs', '/editor/server/database-store.mjs', '/editor/Assets/FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json',
    '/editor/%5c..%5cprivate.json', '/editor/app.js%00', '/editor/.env'])
    await expectError(await app.get(path), 404, 'not_found');
  await expectError(await app.get('/editor/%E0%A4'), 400, 'invalid_path');
});

test('unsupported API operations return a structured method error', async t => {
  const app = await application(t);
  await expectError(await app.get('/editor/api/database', { method: 'DELETE' }), 405, 'method_not_allowed');
  await expectError(await app.get('/editor/api/unknown'), 405, 'method_not_allowed');
});
