import assert from 'node:assert/strict';
import test from 'node:test';
import { appearance, fixture, isError, schemas, validate } from './helpers.mjs';

test('validator accepts v1, v2 and v3, including v2 profiles with omitted appearance', () => {
  for (const version of [1, 2, 3]) {
    const document = fixture(version);
    assert.equal(validate(document), document);
  }
  const document = fixture(2);
  for (const profile of document.visualProfiles) delete profile.appearance;
  assert.equal(validate(document), document);
});

test('validator does not coerce, insert defaults, remove fields or mutate its input', () => {
  const document = fixture();
  const before = structuredClone(document);
  validate(document);
  assert.deepEqual(document, before);
  document.players[0].heightCm = '180';
  assert.throws(() => validate(document), isError(422, 'invalid_database', '/players/0/heightCm'));
  assert.equal(document.players[0].heightCm, '180');
});

test('validator rejects unsupported versions and unknown root properties', () => {
  for (const value of [null, [], {}, { schemaVersion: 4 }, { schemaVersion: '2' }])
    assert.throws(() => validate(value), isError(422, 'unsupported_schema_version'));
  const document = fixture();
  document.competitions = [];
  assert.throws(() => validate(document), isError(422, 'invalid_database'));
});

test('v1 cannot silently accept v2 appearance data', () => {
  const document = fixture(1);
  document.visualProfiles[0].appearance = appearance();
  assert.throws(() => validate(document), isError(422, 'invalid_database'));
});

test('validator rejects partial, null, unknown and wrongly cased appearance presets', () => {
  for (const [field, property] of Object.entries(schemas[1].definitions.appearance.properties)) {
    for (const value of property.enum) {
      const valid = fixture();
      valid.visualProfiles[0].appearance[field] = value;
      assert.doesNotThrow(() => validate(valid));
    }
    for (const value of [null, 0, 'not-supported', property.enum[0].toUpperCase()]) {
      const document = fixture();
      document.visualProfiles[0].appearance[field] = value;
      assert.throws(() => validate(document), isError(422, 'invalid_database', `/visualProfiles/0/appearance/${field}`));
    }
    const document = fixture();
    delete document.visualProfiles[0].appearance[field];
    assert.throws(() => validate(document), isError(422, 'invalid_database', `/visualProfiles/0/appearance/${field}`));
  }
  const document = fixture();
  document.visualProfiles[0].appearance = null;
  assert.throws(() => validate(document), isError(422, 'invalid_database', '/visualProfiles/0/appearance'));
});

test('built-in appearance cannot recolor a custom or unsupported skin', () => {
  for (const [field, value] of [['skinId', 'custom-skin'], ['revision', 2], ['compatibilityProfile', 'football-player-v2']]) {
    const document = fixture();
    document.visualProfiles[0].skin[field] = value;
    assert.throws(() => validate(document), isError(422, 'invalid_database'));
  }
});

test('validator rejects duplicate identities, club memberships and visual profiles', () => {
  for (const collection of ['clubs', 'players', 'memberships', 'visualProfiles']) {
    const document = fixture();
    const index = document[collection].length;
    document[collection].push(structuredClone(document[collection][0]));
    const key = ['clubs', 'players'].includes(collection) ? 'id' : 'playerId';
    assert.throws(() => validate(document), isError(422, 'invalid_references', `/${collection}/${index}/${key}`));
  }
});

test('validator rejects dangling references while allowing free agents and large rosters', () => {
  for (const [collection, key] of [['memberships', 'clubId'], ['memberships', 'playerId'], ['visualProfiles', 'playerId']]) {
    const document = fixture();
    document[collection][0][key] = 'missing-id';
    assert.throws(() => validate(document), isError(422, 'invalid_references', `/${collection}/0/${key}`));
  }
  const document = fixture();
  document.memberships.pop();
  for (const member of document.memberships) member.clubId = document.clubs[0].id;
  assert.ok(document.memberships.length > 11);
  assert.doesNotThrow(() => validate(document));
});
