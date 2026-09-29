import assert from 'node:assert/strict';
import test from 'node:test';
import { readFile } from 'node:fs/promises';
import { fixture, validate } from '../test/helpers.mjs';
import { addRecord } from './records.js';
import { changeFormat } from './competition-model.js';
import { addFormat, newStage, duplicateFormat, assignFormat, syncSchedules, distributeGroups, assignGroup, generateStageDates, setPortableValue, recommendedRounds, moveStage, changeStageKind, stageRemovalBlock, declarativeDeletionBlock } from './format-model.js';
import { stageParticipantCounts, validateDeclarativeCompetitions } from './declarative-rules.js';
import { declarativeEditionForm } from './edition-declarative-ui.js';
import { competitionMetadataFields } from './competition-metadata-ui.js';
import { select } from './ui.js';
const authored = JSON.parse(await readFile(new URL('../../Assets/FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json', import.meta.url), 'utf8'));

test('creating a reusable format promotes v5 while preserving legacy edition identities and rules', () => {
  const document = fixture(5), legacy = structuredClone(document.competitionEditions[0]);
  const format = addRecord(document, 'competitionFormats', { name: 'Liga editável' });
  assert.equal(document.schemaVersion, 6); assert.equal(format.version, 1);
  assert.deepEqual(document.competitionEditions[0], legacy); assert.doesNotThrow(() => validate(document));
  changeFormat(document, document.competitionEditions[0], 'paulista-2026'); assert.equal(document.schemaVersion, 6);
});

test('duplicating a branching format remaps internal IDs without changing original or title selection', () => {
  const document = structuredClone(authored), source = document.competitionFormats.find(f => f.stages.some(s => s.source?.selection === 'losers'));
  const original = structuredClone(source), copy = duplicateFormat(document, source, 'Outra copa');
  const oldIds = new Set([source.id, ...source.stages.map(s => s.id), ...source.outcomes.map(o => o.id)]);
  assert.ok(!oldIds.has(copy.id)); assert.deepEqual(source, original);
  assert.ok(copy.stages.every(s => !oldIds.has(s.id)));
  assert.ok(copy.stages.filter(s => s.source).every(s => copy.stages.some(p => p.id === s.source.stageId)));
  assert.equal(copy.stages.findIndex(s => s.id === copy.championStageId), source.stages.findIndex(s => s.id === source.championStageId));
  assert.doesNotThrow(() => validate(document));
  source.championStageId = null; assert.equal(duplicateFormat(document, source, 'Sem título').championStageId, null);
});

test('new edition copies the default reference once and changing format preserves participants and edition ID', () => {
  const document = fixture(5), first = addFormat(document, 'Primeiro'), second = addFormat(document, 'Segundo');
  document.competitions[0].defaultFormatId = first.id;
  const edition = addRecord(document, 'competitionEditions', { name: '2027', competitionId: document.competitions[0].id });
  assert.equal(edition.formatId, first.id); assert.equal(edition.stageSchedules.length, 1); assert.equal(edition.rules, undefined);
  document.competitions[0].defaultFormatId = second.id; assert.equal(edition.formatId, first.id);
  edition.participantClubIds = document.clubs.map(c => c.id); const id = edition.id;
  assignFormat(document, edition, second.id); assert.equal(edition.id, id); assert.equal(edition.participantClubIds.length, 4);
  assert.ok(declarativeDeletionBlock(document, 'competitionFormats', second));
  generateStageDates(second.stages[0], edition.stageSchedules[0], '2027-02-01', 7);
  assert.doesNotThrow(() => validate(document));
});

test('serpentine seeded groups produce 1,4,5,8 and 2,3,6,7 with independent editable assignment', () => {
  const document = structuredClone(authored), format = document.competitionFormats.find(f => f.id === 'format-league-seeded-groups');
  assert.ok(format); const edition = { id: 'edition-seeding', competitionId: document.competitions[0].id, name: 'Teste', participantClubIds: document.clubs.map(c => c.id) };
  assignFormat(document, edition, format.id); const stage = format.stages[1], schedule = edition.stageSchedules[1];
  distributeGroups(edition, format, stage, true);
  assert.deepEqual(schedule.groups[0].seedRanks, [1, 4, 5, 8]); assert.deepEqual(schedule.groups[1].seedRanks, [2, 3, 6, 7]);
  assert.equal(recommendedRounds(format, stage), 3);
  const ids = schedule.groups.map(g => g.id); distributeGroups(edition, format, stage, false); assert.deepEqual(schedule.groups.map(g => g.id), ids);
  assignGroup(schedule, '1', ids[1], true); assert.ok(!schedule.groups[0].seedRanks.includes(1)); assert.ok(schedule.groups[1].seedRanks.includes(1));
});

test('changing stage model updates descendants and implicit origins remain explicit when edited', () => {
  const first = newStage('Liga', 8); first.qualification.count = 8;
  const second = newStage('Quartas', 8), third = newStage('Semi', 4);
  const format = { participantCount: 8, stages: [first, second, third] };
  changeStageKind(format, second, 'knockout'); changeStageKind(format, third, 'knockout');
  changeStageKind(format, first, 'knockout');
  assert.deepEqual(stageParticipantCounts(format), [8, 4, 2]); assert.deepEqual(format.stages.map(s => s.qualification.count), [4, 2, 1]);
  setPortableValue(format, 'stages[2].source.stageId', first.id); assert.equal(third.source.selection, 'qualified');
  assert.equal(third.qualification.count, 2);
  setPortableValue(format, 'championStageId', '', 'nullable'); assert.equal(format.championStageId, null);
  assert.throws(() => moveStage(format, first.id, 1), /origem/);
});

test('calendar generation rejects malformed bounds transactionally and supports leap dates', () => {
  const stage = newStage('Liga', 4), schedule = { roundDates: ['old'] };
  generateStageDates(stage, schedule, '2024-02-28', 1); assert.deepEqual(schedule.roundDates, ['2024-02-28', '2024-02-29', '2024-03-01']);
  const before = structuredClone(schedule); stage.roundCount = 999999; assert.throws(() => generateStageDates(stage, schedule, '2024-01-01', 1), /128/); assert.deepEqual(schedule, before);
});

test('synchronizing schedules retains matching IDs and refuses deletion while calendars reference a phase', () => {
  const document = structuredClone(authored), edition = document.competitionEditions[0], format = document.competitionFormats.find(f => f.id === edition.formatId);
  const first = structuredClone(edition.stageSchedules[0]); syncSchedules(edition, format); assert.deepEqual(edition.stageSchedules[0], first);
  assert.ok(stageRemovalBlock(document, format, format.stages[1]));
  assert.ok(declarativeDeletionBlock(document, 'countries', document.countries[0]));
});

test('every shipped format has editable schedules and bounded rendering keeps invalid drafts intact', () => {
  const document = structuredClone(authored);
  for (const format of document.competitionFormats) {
    const edition = { id: 'test-edition', competitionId: document.competitions[0].id, name: '<script>draft</script>', participantClubIds: document.clubs.slice(0, format.participantCount).map(c => c.id) };
    assignFormat(document, edition, format.id); document.competitionEditions.push(edition);
    for (const stage of format.stages) {
      const html = declarativeEditionForm(edition, document, stage.id);
      assert.ok(html.includes('edition-stage-filter')); assert.ok(html.includes('data-portable-path="stageSchedules['));
      assert.ok(!html.includes('<script>draft</script>'));
    }
    document.competitionEditions.pop();
  }
  const edition = document.competitionEditions[0], format = document.competitionFormats.find(f => f.id === edition.formatId);
  format.stages[0].roundCount = 999999;
  const html = declarativeEditionForm(edition, document, format.stages[0].id);
  assert.equal((html.match(/data-portable-path="stageSchedules\[0\]\.roundDates\[/g) || []).length, 128);
  assert.equal(format.stages[0].roundCount, 999999);
});

test('competition metadata exposes numeric awards, routes and honest future media fields without raw JSON', () => {
  const document = structuredClone(authored), competition = document.competitions[0];
  const html = competitionMetadataFields(competition, document);
  assert.match(html, /prizes\.currency/); assert.match(html, /eligibility\.stateCodes/); assert.match(html, /trophyModelUri/);
  assert.match(html, /não faz upload/); assert.doesNotMatch(html, /<textarea/);
  assert.match(select({ id: 'stale', label: 'Vaga', value: 'missing', options: [] }), /value="missing" selected>Valor a corrigir/);
});
