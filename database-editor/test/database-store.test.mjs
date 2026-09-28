import assert from 'node:assert/strict';
import test from 'node:test';
import { readFile, readdir, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { fixture, isError, temporaryStore } from './helpers.mjs';

test('save increments revision on the server, preserves identities and backs up exact prior bytes', async t => {
  const { store, path, directory, original } = await temporaryStore(t);
  const first = await store.read();
  const draft = structuredClone(first.document);
  draft.players[0].name = 'Nome editado';
  draft.visualProfiles[0].appearance.hairStyle = 'mohawk';
  const saved = await store.save(draft, first.etag);
  assert.equal(saved.document.databaseRevision, first.document.databaseRevision + 1);
  assert.equal(draft.databaseRevision, first.document.databaseRevision);
  assert.equal(saved.document.databaseId, first.document.databaseId);
  assert.equal(saved.document.players[0].id, first.document.players[0].id);
  assert.notEqual(saved.etag, first.etag);
  assert.equal(await readFile(join(directory, '.editor-backups/previous.database.json'), 'utf8'), original);
  const actual = await store.read();
  assert.deepEqual(actual, saved);
  assert.equal(actual.document.players[0].name, 'Nome editado');
  assert.equal(actual.document.visualProfiles[0].appearance.hairStyle, 'mohawk');
  assert.ok((await readFile(path, 'utf8')).endsWith('\n'));
  assert.deepEqual((await readdir(directory)).filter(name => name.endsWith('.tmp')), []);
});

test('invalid data and missing preconditions leave source bytes untouched', async t => {
  const { store, path, original } = await temporaryStore(t);
  const current = await store.read();
  await assert.rejects(store.save(current.document, undefined), isError(428, 'precondition_required'));
  const invalid = structuredClone(current.document);
  invalid.visualProfiles[0].appearance.hairStyle = 'missing';
  await assert.rejects(store.save(invalid, current.etag), isError(422, 'invalid_database'));
  assert.equal(await readFile(path, 'utf8'), original);
});

test('save refuses identity changes and manually incremented draft revisions', async t => {
  const { store, path, original } = await temporaryStore(t);
  const current = await store.read();
  const wrongIdentity = structuredClone(current.document);
  wrongIdentity.databaseId = 'different-database';
  await assert.rejects(store.save(wrongIdentity, current.etag), isError(422, 'immutable_id'));
  const wrongRevision = structuredClone(current.document);
  wrongRevision.databaseRevision++;
  await assert.rejects(store.save(wrongRevision, current.etag), isError(409, 'conflict'));
  assert.equal(await readFile(path, 'utf8'), original);
});

test('an external change is detected by exact file SHA even without a revision change', async t => {
  const { store, path, original } = await temporaryStore(t);
  const current = await store.read();
  await writeFile(path, original + ' ');
  await assert.rejects(store.save(current.document, current.etag), isError(409, 'conflict'));
  assert.equal(await readFile(path, 'utf8'), original + ' ');
});

test('two concurrent saves from the same revision have exactly one winner', async t => {
  const { store } = await temporaryStore(t);
  const current = await store.read();
  const first = structuredClone(current.document);
  const second = structuredClone(current.document);
  first.players[0].name = 'Primeiro';
  second.players[0].name = 'Segundo';
  const results = await Promise.allSettled([store.save(first, current.etag), store.save(second, current.etag)]);
  assert.equal(results.filter(result => result.status === 'fulfilled').length, 1);
  const rejected = results.find(result => result.status === 'rejected');
  assert.equal(rejected.reason.code, 'conflict');
  const actual = await store.read();
  assert.equal(actual.document.databaseRevision, current.document.databaseRevision + 1);
  assert.equal(actual.document.players[0].name, 'Primeiro');
  // A rejected operation does not poison the save queue.
  const next = await store.save(actual.document, actual.etag);
  assert.equal(next.document.databaseRevision, current.document.databaseRevision + 2);
});

test('an invalid later save preserves both the successful file and its recovery copy', async t => {
  const { store, path, directory, original } = await temporaryStore(t);
  const current = await store.read();
  const saved = await store.save(current.document, current.etag);
  const successBytes = await readFile(path, 'utf8');
  const invalid = structuredClone(saved.document);
  invalid.memberships[0].playerId = 'missing';
  await assert.rejects(store.save(invalid, saved.etag), isError(422, 'invalid_references'));
  assert.equal(await readFile(path, 'utf8'), successBytes);
  assert.equal(await readFile(join(directory, '.editor-backups/previous.database.json'), 'utf8'), original);
});

test('v1 can migrate to v2 in one save without replacing sporting identities', async t => {
  const { store } = await temporaryStore(t, fixture(1));
  const current = await store.read();
  const migrated = fixture(2);
  const saved = await store.save(migrated, current.etag);
  assert.equal(saved.document.schemaVersion, 2);
  assert.deepEqual(saved.document.players, current.document.players);
  assert.deepEqual(saved.document.memberships, current.document.memberships);
});

test('store rejects invalid UTF-8 and duplicate JSON fields already on disk', async t => {
  const { store, path } = await temporaryStore(t);
  await writeFile(path, Buffer.from([0xff, 0xfe, 0x7b]));
  await assert.rejects(store.read(), isError(422, 'invalid_utf8'));
  await writeFile(path, '{"schemaVersion":1,"schemaVersion":2}');
  await assert.rejects(store.read(), isError(422, 'invalid_json'));
});

test('revision overflow cannot overwrite the current source', async t => {
  const document = fixture();
  document.databaseRevision = 2147483647;
  const { store, path, original } = await temporaryStore(t, document);
  const current = await store.read();
  await assert.rejects(store.save(current.document, current.etag), isError(422, 'revision_limit'));
  assert.equal(await readFile(path, 'utf8'), original);
});
