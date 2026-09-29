import test from 'node:test';
import assert from 'node:assert/strict';
import { clone, addPlayer, setMembership, deletePlayer, enableAppearance, validateDocument, normalize } from './model.js';

const fields = ['strength', 'acceleration', 'topSpeed', 'dribbleSpeed', 'jump', 'tackling', 'ballKeeping', 'passing', 'longBall', 'agility', 'shooting', 'shootPower', 'positioning', 'reaction', 'ballControl'];
const options = {
  attributes: fields.map(field => ({ field, label: field })),
  limits: { heightCm: { min: 150, max: 210 }, weightKg: { min: 45, max: 100 } },
  defaultAppearance: { skinTone: 'tone-3', hairStyle: 'short', hairColor: 'black', beardStyle: 'none', beardColor: 'black', bootsColor: 'black', sockAccessoryColor: 'none' },
};
const empty = () => ({ schemaVersion: 1, databaseId: 'db-permanent', databaseRevision: 7, clubs: [{ id: 'club-one', name: 'Primeiro clube' }, { id: 'club-two', name: 'Segundo clube' }], players: [], memberships: [], visualProfiles: [] });

test('create player preserves database identity/revision and creates complete independent visual profile', () => {
  const document = empty();
  const player = addPlayer(document, 'Novo jogador', 'club-one', options);
  assert.match(player.id, /^player-[0-9a-f]{32}$/);
  assert.equal(document.databaseId, 'db-permanent');
  assert.equal(document.databaseRevision, 7);
  assert.equal(document.schemaVersion, 2);
  assert.equal(Object.keys(player.attributes).length, 15);
  assert.deepEqual(document.memberships, [{ playerId: player.id, clubId: 'club-one' }]);
  assert.deepEqual(document.visualProfiles[0].appearance, options.defaultAppearance);
  document.visualProfiles[0].appearance.skinTone = 'tone-1';
  assert.equal(options.defaultAppearance.skinTone, 'tone-3');
  assert.deepEqual(validateDocument(document, options), []);
});

test('transfer and free agent change only the membership, not player identity or visual profile', () => {
  const document = empty();
  const player = addPlayer(document, 'Teste', 'club-one', options);
  const identity = player.id;
  const profile = clone(document.visualProfiles[0]);
  setMembership(document, player.id, 'club-two');
  assert.deepEqual(document.memberships, [{ playerId: identity, clubId: 'club-two' }]);
  setMembership(document, player.id, '');
  assert.deepEqual(document.memberships, []);
  assert.equal(document.players[0].id, identity);
  assert.deepEqual(document.visualProfiles[0], profile);
});

test('delete player removes exactly its related memberships and visual profile', () => {
  const document = empty();
  const first = addPlayer(document, 'Primeiro', 'club-one', options);
  const second = addPlayer(document, 'Segundo', 'club-two', options);
  deletePlayer(document, first.id);
  assert.deepEqual(document.players.map(player => player.id), [second.id]);
  assert.deepEqual(document.memberships.map(member => member.playerId), [second.id]);
  assert.deepEqual(document.visualProfiles.map(profile => profile.playerId), [second.id]);
  assert.equal(document.clubs.length, 2);
});

test('enabling appearance refuses unsupported skin without changing any portable data', () => {
  const document = empty();
  const player = addPlayer(document, 'Teste', '', options);
  document.visualProfiles[0] = { playerId: player.id, skin: { skinId: 'community-skin', revision: 4, compatibilityProfile: 'football-player-v1' } };
  const before = clone(document);
  assert.equal(enableAppearance(document, player.id, options.defaultAppearance), null);
  assert.deepEqual(document, before);
});

test('existing appearance is preserved when opened again', () => {
  const document = empty();
  const player = addPlayer(document, 'Teste', '', options);
  document.visualProfiles[0].appearance.hairStyle = 'mohawk';
  enableAppearance(document, player.id, options.defaultAppearance);
  assert.equal(document.visualProfiles[0].appearance.hairStyle, 'mohawk');
});

test('client validation identifies unrelated invalid players for global save and exact fields', () => {
  const document = empty();
  const first = addPlayer(document, 'Válido', 'club-one', options);
  const second = addPlayer(document, 'Inválido', 'club-two', options);
  first.weightKg = 101;
  second.naturalPositions = [];
  second.attributes.shooting = 2.5;
  const paths = validateDocument(document, options).map(issue => issue.path);
  assert.deepEqual(paths, ['players[0].weightKg', 'players[1].naturalPositions', 'players[1].attributes.shooting']);
});

test('names use Unicode code points, reject whitespace and preserve accents in authored values', () => {
  const document = empty();
  const player = addPlayer(document, '⚽'.repeat(100), '', options);
  assert.deepEqual(validateDocument(document, options), []);
  player.name = '⚽'.repeat(101);
  assert.equal(validateDocument(document, options)[0].path, 'players[0].name');
  player.name = '   ';
  assert.equal(validateDocument(document, options)[0].path, 'players[0].name');
  player.name = 'São Paulo';
  assert.equal(normalize(player.name), 'sao paulo');
  assert.equal(player.name, 'São Paulo');
});

test('source snapshot remains unchanged while draft edits and deletes', () => {
  const original = empty();
  addPlayer(original, 'Jogador original', 'club-one', options);
  const draft = clone(original);
  draft.players[0].name = 'Renomeado';
  deletePlayer(draft, draft.players[0].id);
  assert.equal(original.players[0].name, 'Jogador original');
  assert.equal(original.memberships.length, 1);
  assert.equal(original.visualProfiles.length, 1);
});

test('adding appearance or a player to v3 preserves the schema and competition data', () => {
  const document = empty();
  document.schemaVersion = 3;
  document.competitions = [{ id: 'competition-one', name: 'Liga' }];
  document.competitionEditions = [{ id: 'edition-one', participantClubIds: ['club-one', 'club-two'] }];
  const before = clone(document.competitionEditions);
  const player = addPlayer(document, 'Novo atleta', 'club-one', options);
  enableAppearance(document, player.id, options.defaultAppearance);
  assert.equal(document.schemaVersion, 3);
  assert.deepEqual(document.competitionEditions, before);
  assert.equal(document.competitions[0].id, 'competition-one');
});
