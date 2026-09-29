import test from 'node:test';
import assert from 'node:assert/strict';
import { clone, validateDocument, normalize } from './model.js';
import { validDate, validateHistory, setRecordField, deletionBlock, playerDisplayName, playerSearchText } from './history-model.js';
import { availableViews, validFilters, creationError, addRecord, addSource, removeSource } from './records.js';
import { playerHistoryFields, clubHistoryFields, stadiumForm, snapshotForm, referenceStrip } from './history-ui.js';
import { applyDialogError } from './ui.js';

const options = {
  attributes: [{ field: 'passing', label: 'Passe' }],
  limits: { heightCm: { min: 150, max: 210 }, weightKg: { min: 45, max: 100 } },
  defaultAppearance: { skinTone: 'tone-3', hairStyle: 'short', hairColor: 'black', beardStyle: 'none', beardColor: 'black', bootsColor: 'black', sockAccessoryColor: 'none' },
};
const base = () => ({
  schemaVersion: 4, databaseId: 'db-stable', databaseRevision: 9,
  countries: [{ code: 'BR', name: 'Brasil' }, { code: 'AR', name: 'Argentina' }],
  stadiums: [{ id: 'stadium-first', name: 'Estádio de teste', countryCode: 'BR', city: 'São Paulo' }],
  snapshot: { date: '2026-01-01', label: 'Abertura de janeiro de 2026', rosterScope: 'matchday-squads', notes: '', sources: [{ id: 'source-first', title: 'Fonte de teste', url: 'https://example.org/football' }] },
  clubs: [{ id: 'club-first', name: 'Clube de teste', countryCode: 'BR', city: 'São Paulo', stadiumId: 'stadium-first' }],
  players: [{ id: 'player-first', name: 'Nome esportivo', fullName: 'Nome completo confirmado', naturalPositions: ['CM'], heightCm: 180, weightKg: 75, attributes: { passing: 65 } }],
  memberships: [{ clubId: 'club-first', playerId: 'player-first' }], visualProfiles: [],
  competitions: [{ id: 'competition-stable', name: 'Teste', rules: { kind: 'league' } }], competitionEditions: [],
});
const paths = document => validateHistory(document).map(issue => issue.path);

test('stadium creation associates each error with its field and moves focus without duplicating the message below city', () => {
  const controls = {};
  let focusedId;
  for (const id of ['new-name', 'new-country', 'new-city']) {
    controls[id] = { id, attributes: {}, setAttribute(name, value) { this.attributes[name] = value; }, focus() { focusedId = id; } };
    controls[`${id}-error`] = { textContent: '' };
  }
  controls['dialog-error'] = { textContent: '' };
  const dialog = {
    querySelector: selector => controls[selector.slice(1)],
    querySelectorAll: selector => Object.values(controls).filter(control => selector === '.field-error' ? Object.hasOwn(control, 'textContent') : control.attributes?.['aria-invalid'] === 'true'),
  };
  const values = { name: 'Estádio de teste QA', countryCode: '', city: '' };
  applyDialogError(dialog, creationError(base(), 'stadiums', values));
  assert.equal(controls['new-country-error'].textContent, 'Selecione um país cadastrado.');
  assert.equal(controls['new-country'].attributes['aria-invalid'], 'true');
  assert.equal(controls['new-city-error'].textContent, '');
  assert.notEqual(controls['dialog-error'].textContent, controls['new-country-error'].textContent);
  assert.equal(focusedId, 'new-country');

  values.countryCode = 'BR';
  applyDialogError(dialog, creationError(base(), 'stadiums', values));
  assert.equal(controls['new-country-error'].textContent, '');
  assert.equal(controls['new-country'].attributes['aria-invalid'], 'false');
  assert.equal(controls['new-city-error'].textContent, 'Informe uma cidade de até 100 caracteres.');
  assert.equal(controls['new-city'].attributes['aria-invalid'], 'true');
  assert.equal(focusedId, 'new-city');

  values.city = 'São Paulo';
  applyDialogError(dialog, creationError(base(), 'stadiums', values));
  assert.equal(controls['new-city-error'].textContent, '');
  assert.equal(controls['new-city'].attributes['aria-invalid'], 'false');
  assert.equal(controls['dialog-error'].textContent, '');
});

test('discarding a draft clears filters for removed clubs and countries while preserving existing filters', () => {
  const original = base();
  const draft = clone(original);
  draft.countries.push({ code: 'UY', name: 'Uruguai' });
  draft.clubs.push({ id: 'club-draft', name: 'Clube do rascunho', countryCode: 'UY', city: 'Montevidéu' });
  const filters = { club: 'club-draft', country: 'UY' };
  assert.deepEqual(validFilters(draft, filters), filters);
  assert.deepEqual(validFilters(clone(original), filters), { club: '', country: '' });
  assert.deepEqual(validFilters(original, { club: 'club-first', country: 'BR' }), { club: 'club-first', country: 'BR' });
  assert.deepEqual(validFilters(original, { club: 'free', country: '' }), { club: 'free', country: '' });
  assert.deepEqual(validFilters({ clubs: original.clubs }, { club: 'free', country: 'BR' }), { club: 'free', country: '' });
});

test('v4 accepts omitted unknowns without inventing player, stadium or financial data', () => {
  const document = base(); const before = clone(document);
  assert.deepEqual(validateDocument(document, options), []);
  assert.deepEqual(document, before);
  assert.equal(document.players[0].birthDate, undefined);
  assert.equal(document.stadiums[0].capacity, undefined);
  assert.equal(document.clubs[0].transferBudget, undefined);
});

test('real dates include year 0001, reject year zero and use Gregorian leap years', () => {
  for (const date of ['0001-01-01', '2000-02-29', '2024-02-29', '9999-12-31']) assert.equal(validDate(date), true, date);
  for (const date of ['', null, '0000-01-01', '1900-02-29', '2026-02-29', '2026-04-31', '2026-13-01', '2026-1-01', '2026-01-01T00:00:00Z']) assert.equal(validDate(date), false, String(date));
  const document = base();
  document.players[0].birthDate = '2026-01-02';
  assert.deepEqual(paths(document), ['players[0].birthDate']);
  document.players[0].birthDate = document.snapshot.date;
  assert.deepEqual(paths(document), []);
});

test('optional blank input removes the property; explicit zero stays a known simulation value', () => {
  const club = base().clubs[0];
  setRecordField(club, 'transferBudget', '0', { optional: true, numeric: true });
  assert.equal(club.transferBudget, 0);
  setRecordField(club, 'transferBudget', '', { optional: true, numeric: true });
  assert.equal(Object.hasOwn(club, 'transferBudget'), false);
  setRecordField(club, 'notes', '  contexto confirmado  ', { optional: true });
  assert.equal(club.notes, '  contexto confirmado  ');
  setRecordField(club, 'city', '');
  assert.equal(club.city, '');
});

test('explicit nulls and partial historical records produce precise errors instead of silent defaults', () => {
  const document = base();
  Object.assign(document.players[0], { fullName: null, nickname: null, preferredFoot: null, nationalityCode: null, birthDate: null, notes: null });
  Object.assign(document.clubs[0], { currency: null, transferBudget: null, reputation: null, supporterCount: null, stadiumId: null });
  document.stadiums[0].capacity = null;
  const errors = paths(document);
  for (const property of ['fullName', 'nickname', 'preferredFoot', 'nationalityCode', 'birthDate', 'notes']) assert.ok(errors.includes(`players[0].${property}`));
  for (const property of ['currency', 'transferBudget', 'reputation', 'supporterCount', 'stadiumId']) assert.ok(errors.includes(`clubs[0].${property}`));
  assert.ok(errors.includes('stadiums[0].capacity'));
  document.snapshot = null; document.countries = null; document.stadiums = [null];
  assert.doesNotThrow(() => validateHistory(document));
  assert.ok(paths(document).includes('snapshot'));
});

test('budgets require a currency and only accept integer units within the portable contract', () => {
  const document = base(); const club = document.clubs[0];
  club.transferBudget = 0;
  assert.deepEqual(paths(document), ['clubs[0].currency']);
  club.currency = 'BRL'; club.monthlyWageBudget = 2147483647; club.reputation = 100; club.supporterCount = 0;
  assert.deepEqual(paths(document), []);
  club.monthlyWageBudget++; club.reputation = 101; club.transferBudget = 1.25; club.currency = 'brl';
  for (const field of ['monthlyWageBudget', 'reputation', 'transferBudget', 'currency']) assert.ok(paths(document).includes(`clubs[0].${field}`));
});

test('country/stadium references cannot be deleted while used, including player nationality', () => {
  const document = base();
  assert.ok(deletionBlock(document, 'stadiums', document.stadiums[0]));
  assert.ok(deletionBlock(document, 'countries', document.countries[0]));
  assert.equal(deletionBlock(document, 'countries', document.countries[1]), '');
  document.players[0].nationalityCode = 'AR';
  assert.ok(deletionBlock(document, 'countries', document.countries[1]));
  delete document.clubs[0].stadiumId;
  assert.equal(deletionBlock(document, 'stadiums', document.stadiums[0]), '');
});

test('new historical records require authored country/city and preserve database and competition identity', () => {
  const document = base(); const competition = clone(document.competitions);
  const values = { name: 'Novo clube', countryCode: 'BR', city: 'Campinas' };
  assert.equal(creationError(document, 'clubs', { ...values, city: '' }).field, 'new-city');
  assert.equal(creationError(document, 'clubs', { ...values, countryCode: 'XX' }).field, 'new-country');
  assert.equal(creationError(document, 'countries', { name: 'Duplicado', code: 'BR' }).field, 'new-code');
  const club = addRecord(document, 'clubs', values, options);
  const stadium = addRecord(document, 'stadiums', { ...values, name: 'Estádio novo' }, options);
  assert.match(club.id, /^club-[a-f0-9]{32}$/); assert.match(stadium.id, /^stadium-[a-f0-9]{32}$/);
  assert.equal(stadium.capacity, undefined); assert.equal(club.transferBudget, undefined);
  assert.equal(document.databaseId, 'db-stable'); assert.equal(document.databaseRevision, 9); assert.equal(document.schemaVersion, 4);
  assert.deepEqual(document.competitions, competition);
});

test('legacy 1–3 keep existing scope and schema, without fabricating a historical snapshot', () => {
  for (const version of [1, 2, 3]) {
    const document = base(); document.schemaVersion = version;
    delete document.snapshot; delete document.countries; delete document.stadiums;
    assert.deepEqual(availableViews(document), ['players', 'clubs', 'competitions', 'competitionEditions']);
    assert.deepEqual(validateHistory(document), []);
    addRecord(document, 'clubs', { name: 'Clube legado' }, options);
    assert.equal(document.schemaVersion, version); assert.equal(document.snapshot, undefined);
    assert.equal(document.clubs.at(-1).countryCode, undefined);
    assert.equal(referenceStrip(document), ''); assert.equal(playerHistoryFields(document.players[0], document, 'players[0]'), '');
  }
});

test('historical edits are isolated in a global draft and export preserves provenance/source IDs', () => {
  const original = base(); const draft = clone(original); const firstId = draft.snapshot.sources[0].id;
  const source = addSource(draft); assert.match(source.id, /^source-[a-f0-9]{32}$/);
  setRecordField(source, 'title', 'Pesquisa adicional'); setRecordField(source, 'url', 'https://example.org/new');
  setRecordField(draft.players[0], 'nickname', 'Apelido', { optional: true });
  setRecordField(draft.stadiums[0], 'capacity', '72000', { optional: true, numeric: true });
  const exported = JSON.parse(JSON.stringify(draft));
  assert.equal(exported.snapshot.sources[0].id, firstId); assert.equal(exported.snapshot.sources[1].id, source.id);
  assert.equal(exported.players[0].nickname, 'Apelido'); assert.equal(original.players[0].nickname, undefined);
  assert.equal(original.snapshot.sources.length, 1); assert.equal(original.stadiums[0].capacity, undefined);
  assert.equal(removeSource(draft, firstId), true); assert.equal(removeSource(draft, source.id), false);
  assert.equal(draft.databaseRevision, original.databaseRevision);
});

test('source protocols and required provenance are validated while invalid values stay in the draft', () => {
  const document = base(); document.snapshot.sources[0].url = 'javascript:alert(1)';
  assert.ok(paths(document).includes('snapshot.sources[0].url'));
  assert.equal(document.snapshot.sources[0].url, 'javascript:alert(1)');
  document.snapshot.sources = []; assert.ok(paths(document).includes('snapshot.sources'));
  document.snapshot = { date: '2026-01-01' }; assert.ok(paths(document).includes('snapshot.label'));
});

test('source URLs reject literal whitespace and normalization-dependent syntax without changing the draft', () => {
  const document = base();
  for (const url of [' https://example.org/report', 'https://example.org/report ', 'https://example.org/with space', 'https://example.org/with\u0085space', 'https://example.org/with\uFEFFspace', 'https:///example.org', 'HTTPS://example.org']) {
    document.snapshot.sources[0].url = url;
    assert.ok(paths(document).includes('snapshot.sources[0].url'), JSON.stringify(url));
    assert.equal(document.snapshot.sources[0].url, url);
  }
  document.snapshot.sources[0].url = 'https://example.org/' + '😀'.repeat(2028);
  assert.equal([...document.snapshot.sources[0].url].length, 2048);
  assert.ok(!paths(document).includes('snapshot.sources[0].url'));
});

test('nickname is optional, is used as display name, and all three name fields remain searchable', () => {
  const document = base(); const player = document.players[0];
  assert.equal(playerDisplayName(player), player.name);
  setRecordField(player, 'nickname', 'Ídolo', { optional: true });
  assert.equal(playerDisplayName(player), 'Ídolo');
  for (const query of ['idolo', 'esportivo', 'completo']) assert.ok(normalize(playerSearchText(player)).includes(query));
  player.nickname = '😀'.repeat(100); assert.deepEqual(paths(document), []);
  player.nickname += '😀'; assert.ok(paths(document).includes('players[0].nickname'));
  setRecordField(player, 'nickname', '', { optional: true }); assert.equal(playerDisplayName(player), player.name);
});

test('forms show honest unknowns, safe authored text and the current historical scope', () => {
  const document = base(); const player = document.players[0];
  player.fullName = '<img src=x onerror=alert(1)>';
  const fields = playerHistoryFields(player, document, 'players[0]');
  assert.ok(fields.includes('&lt;img')); assert.ok(!fields.includes('<img'));
  assert.match(fields, /id="player-birth-date"[^>]+value=""/);
  assert.ok(fields.includes('value="" selected>Não informado'));
  assert.ok(fields.includes('data-property="nickname" data-optional="true"'));
  assert.ok(clubHistoryFields(document.clubs[0], document, 'clubs[0]').includes('não altera o estádio 3D'));
  assert.match(stadiumForm(document.stadiums[0], document), /id="stadium-capacity"[^>]+value=""/);
  assert.ok(referenceStrip(document).includes('Relacionados para partidas'));
  assert.ok(referenceStrip(document).includes('01/01/2026'));
  assert.ok(snapshotForm(document).includes('data-source-id="source-first"'));
});
