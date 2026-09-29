import assert from 'node:assert/strict';
import test from 'node:test';
import { fixture, paulistaFixture, validate } from '../test/helpers.mjs';
import { addRecord, creationError, removeHistoricalRecord, availableViews } from './records.js';
import { newEdition, changeFormat, setParticipant, setEditionValue, generateDates, addAuthoredFixture, competitionDeletionBlock } from './competition-model.js';
import { validateCompetitions } from './competition-rules.js';
import { competitionForm, editionForm } from './competition-ui.js';
import { deletionBlock } from './history-model.js';
import { normalizeIssuePath } from './ui.js';

test('competitions can be created on legacy content without inventing historical metadata', () => {
  const document = fixture(2); const id = document.databaseId;
  const competition = addRecord(document, 'competitions', { name: 'Liga regional' });
  assert.equal(document.schemaVersion, 3); assert.equal(document.databaseId, id);
  assert.equal(document.snapshot, undefined);
  assert.ok(availableViews(document).includes('competitionEditions'));
  assert.equal(creationError(document, 'competitionEditions', { name: '2027', competitionId: 'missing' }).field, 'new-competition');
  const edition = addRecord(document, 'competitionEditions', { name: '2027', competitionId: competition.id });
  assert.equal(edition.competitionId, competition.id); assert.notEqual(edition.id, competition.id);
  assert.ok(competitionDeletionBlock(document, 'competitions', competition));
  removeHistoricalRecord(document, 'competitionEditions', edition);
  assert.equal(competitionDeletionBlock(document, 'competitions', competition), '');
});

test('league authoring supports participant changes, points, leap dates and publishing the whole draft', () => {
  const document = fixture(3); const original = structuredClone(document);
  const edition = newEdition(document, document.competitions[0].id, 'Nova edição');
  document.clubs.slice(0, 3).forEach(club => setParticipant(edition, club.id, true));
  setParticipant(edition, document.clubs[0].id, true);
  assert.equal(edition.participantClubIds.length, 3);
  setEditionValue(edition, 'rules.legs', '2', true); setEditionValue(edition, 'rules.points.win', '4', true);
  generateDates(edition, '2024-02-28', 1);
  assert.deepEqual(edition.roundDates.slice(0, 3), ['2024-02-28', '2024-02-29', '2024-03-01']);
  assert.equal(edition.roundDates.length, 6); assert.deepEqual(validateCompetitions(document), []); assert.doesNotThrow(() => validate(document));
  assert.equal(original.competitionEditions.length, 1);
  const previous = [...edition.roundDates]; assert.throws(() => generateDates(edition, '2026-02-30', 7)); assert.deepEqual(edition.roundDates, previous);
  assert.throws(() => generateDates(edition, '9999-12-31', 1)); assert.deepEqual(edition.roundDates, previous);
});

test('changing format is explicit, clears incompatible fields and retains all persistent IDs', () => {
  const document = fixture(4); const edition = document.competitionEditions[0]; const id = edition.id;
  changeFormat(document, edition, 'paulista-2026');
  assert.equal(document.schemaVersion, 5); assert.equal(edition.playoffDates.length, 8); assert.equal(edition.id, id);
  const first = addAuthoredFixture(edition, 1); const second = addAuthoredFixture(edition, 1);
  assert.notEqual(first.id, second.id);
  setEditionValue(edition, 'authoredFixtures[0].date', '2026-01-11'); assert.equal(first.date, '2026-01-11');
  changeFormat(document, edition, 'round-robin'); assert.equal(edition.authoredFixtures, undefined); assert.equal(edition.playoffDates, undefined);
  assert.throws(() => changeFormat(fixture(3), fixture(3).competitionEditions[0], 'paulista-2026'));
});

test('authoring displays at most a selected round of fixtures and escapes all authored labels', () => {
  const document = paulistaFixture(); const edition = document.competitionEditions[0];
  document.competitions[0].name = '<script>author</script>';
  const competition = competitionForm(document.competitions[0], document);
  assert.ok(competition.includes('&lt;script&gt;')); assert.ok(!competition.includes('<script>author'));
  const html = editionForm(edition, document, 2);
  assert.equal((html.match(/class="fixture-card"/g) || []).length, 8);
  assert.ok(html.includes('fixture-8-home')); assert.ok(!html.includes('id="fixture-0-home"'));
  assert.ok(html.includes('playoff-date-7')); assert.ok(html.includes('Temporadas salvas'));
  edition.authoredFixtures[0].stadiumId = 'stadium-demo';
  assert.ok(deletionBlock(document, 'stadiums', document.stadiums[0]));
});

test('invalid calendar stays a draft and exposes a precise path for every affected control', () => {
  const document = paulistaFixture(); const edition = document.competitionEditions[0];
  edition.authoredFixtures[12].date = '2026-02-30';
  edition.playoffDates[7] = '';
  const before = structuredClone(document); const issues = validateCompetitions(document);
  assert.ok(issues.some(issue => issue.path === 'competitionEditions[0].authoredFixtures[12].date'));
  assert.ok(issues.some(issue => issue.path === 'competitionEditions[0].playoffDates[7]'));
  assert.deepEqual(document, before);
  assert.equal(normalizeIssuePath('/competitionEditions/0/authoredFixtures/12/date'), 'competitionEditions[0].authoredFixtures[12].date');
  assert.equal(normalizeIssuePath('$.competitionEditions[0].roundDates[3]'), 'competitionEditions[0].roundDates[3]');
});
