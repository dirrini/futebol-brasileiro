import assert from 'node:assert/strict';
import test from 'node:test';
import { readFile } from 'node:fs/promises';
import { fixture, validate, isError, temporaryStore } from './helpers.mjs';
import { parseDocument } from '../server/strict-json.mjs';

test('v4 preserves sourced observation, optional unknowns and all previous authored data', () => {
  const document = fixture(4);
  Object.assign(document.clubs[0], { officialName: 'Clube oficial', shortName: 'Clube', stadiumId: 'stadium-demo', reputation: 0,
    supporterCount: 0, transferBudget: 0, monthlyWageBudget: 2147483647, currency: 'BRL', sponsorship: 'Teste', notes: '' });
  Object.assign(document.players[0], { fullName: 'Nome completo', nickname: 'Apelido', birthDate: '2000-02-29', preferredFoot: 'both', nationalityCode: 'BR', notes: 'Aproximações de teste.' });
  const before = structuredClone(document);
  assert.equal(validate(document), document); assert.deepEqual(document, before);
  assert.equal(document.players[1].birthDate, undefined);
  assert.equal(document.players[1].preferredFoot, undefined);
});

test('v4 rejects metadata in earlier versions instead of silently discarding it', () => {
  for (const version of [1, 2, 3]) {
    const document = fixture(version); document.clubs[0].countryCode = 'BR';
    assert.throws(() => validate(document), isError(422, 'invalid_database'));
  }
});

test('v4 requires source declaration and club location, rejects nulls and unknown fields', () => {
  for (const field of ['countries', 'stadiums', 'snapshot']) {
    let document = fixture(4); delete document[field]; assert.throws(() => validate(document), isError(422, 'invalid_database'));
    document = fixture(4); document[field] = null; assert.throws(() => validate(document), isError(422, 'invalid_database'));
  }
  for (const field of ['countryCode', 'city']) {
    const document = fixture(4); delete document.clubs[0][field]; assert.throws(() => validate(document), isError(422, 'invalid_database'));
  }
  for (const [collection, fields] of [['clubs', ['officialName', 'shortName', 'stadiumId', 'reputation', 'supporterCount', 'transferBudget', 'monthlyWageBudget', 'currency', 'sponsorship', 'notes']],
    ['players', ['fullName', 'nickname', 'birthDate', 'preferredFoot', 'nationalityCode', 'notes']], ['stadiums', ['capacity']]]) {
    for (const field of fields) { const document = fixture(4); document[collection][0][field] = null; assert.throws(() => validate(document), isError(422, 'invalid_database')); }
  }
  const document = fixture(4); document.snapshot.careerDate = '2026-01-11'; assert.throws(() => validate(document), isError(422, 'invalid_database'));
});

test('v4 rejects duplicate identities and unknown country or stadium references', () => {
  for (const collection of ['countries', 'stadiums']) {
    const document = fixture(4); document[collection].push(structuredClone(document[collection][0]));
    assert.throws(() => validate(document), isError(422, 'invalid_references', `/${collection}/1/${collection === 'countries' ? 'code' : 'id'}`));
  }
  const document = fixture(4); document.snapshot.sources.push(structuredClone(document.snapshot.sources[0]));
  assert.throws(() => validate(document), isError(422, 'invalid_references', '/snapshot/sources/1/id'));
  for (const [collection, field, value] of [['clubs', 'countryCode', 'XX'], ['players', 'nationalityCode', 'XX'], ['stadiums', 'countryCode', 'XX'], ['clubs', 'stadiumId', 'missing']]) {
    const candidate = fixture(4); candidate[collection][0][field] = value;
    assert.throws(() => validate(candidate), isError(422, 'invalid_references', `/${collection}/0/${field}`));
  }
});

test('v4 validates dates including Gregorian leap years and birth date chronology', () => {
  for (const date of ['0000-01-01', '1900-02-29', '2026-02-29', '2026-04-31', '2026-1-11', '2026-01-11T00:00:00Z']) {
    for (const field of ['snapshot', 'birth']) {
      const document = fixture(4);
      if (field === 'snapshot') document.snapshot.date = date; else document.players[0].birthDate = date;
      assert.throws(() => validate(document), error => error.status === 422);
    }
  }
  let document = fixture(4); document.players[0].birthDate = '2026-01-12';
  assert.throws(() => validate(document), isError(422, 'invalid_references', '/players/0/birthDate'));
  for (const date of ['0001-01-01', '2000-02-29', '2026-01-11']) { document = fixture(4); document.players[0].birthDate = date; validate(document); }
});

test('v4 applies precise integer, text and enum bounds without normalizing input', () => {
  for (const [collection, field, value] of [['clubs', 'reputation', 101], ['clubs', 'supporterCount', -1], ['clubs', 'transferBudget', 2147483648],
    ['clubs', 'currency', 'brl'], ['clubs', 'countryCode', 'br'], ['clubs', 'city', ' '], ['clubs', 'sponsorship', ' '],
    ['players', 'preferredFoot', 'Right'], ['stadiums', 'capacity', 0], ['stadiums', 'capacity', 1000001],
    ['players', 'fullName', '😀'.repeat(201)], ['players', 'notes', 'x'.repeat(4001)], ['players', 'nickname', ' '], ['players', 'nickname', 'x'.repeat(101)]]) {
    const document = fixture(4); document[collection][0][field] = value; assert.throws(() => validate(document), isError(422, 'invalid_database'));
  }
  const document = fixture(4); document.players[0].fullName = '😀'.repeat(200); document.players[0].notes = 'x'.repeat(4000); validate(document);
  document.clubs[0].transferBudget = 0; assert.throws(() => validate(document), isError(422, 'invalid_database'));
  document.clubs[0].currency = 'BRL'; validate(document);
  const raw = JSON.stringify(document).replace('"transferBudget":0', '"transferBudget":0.0');
  assert.throws(() => parseDocument(raw), isError(422, 'invalid_json'));
});

test('v4 limits sourced observation collections and disallows executable/local source links', () => {
  for (const url of ['file:///secret', 'javascript:alert(1)', 'https:///report.pdf', 'https://example.com/with space', 'https://example.com/with\u0085space', 'https://example.com/with\uFEFFspace', 'relative.pdf']) {
    const document = fixture(4); document.snapshot.sources[0].url = url; assert.throws(() => validate(document), isError(422, 'invalid_database'));
  }
  for (const [key, max] of [['countries', 300], ['stadiums', 1024]]) {
    const document = fixture(4); document[key] = Array.from({length:max + 1}, () => structuredClone(document[key][0]));
    assert.throws(() => validate(document), isError(422, 'invalid_database'));
  }
  const document = fixture(4); document.snapshot.sources = []; assert.throws(() => validate(document), isError(422, 'invalid_database'));
});

test('v4 store edits increment revision and preserve snapshot, biography and competition collections', async t => {
  const document = fixture(4); document.players[0].birthDate = '2000-02-29';
  const {store, path} = await temporaryStore(t, document);
  const opened = await store.read();
  const draft = structuredClone(opened.document); draft.clubs[0].officialName = 'Changed official name';
  await store.save(draft, opened.etag);
  const saved = JSON.parse(await readFile(path, 'utf8'));
  assert.equal(saved.databaseRevision, document.databaseRevision + 1);
  assert.deepEqual(saved.snapshot, document.snapshot); assert.deepEqual(saved.competitionEditions, document.competitionEditions);
  assert.equal(saved.players[0].birthDate, '2000-02-29'); assert.equal(saved.schemaVersion, 4);
});
