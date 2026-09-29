import test from 'node:test';
import assert from 'node:assert/strict';
import { formatForm } from './competition-format-ui.js';
import { newStage } from './format-model.js';

function fixture() {
  const league = { ...newStage('Liga', 8), id: 'league', groupCount: 2, opponents: 'cross-group', qualification: { mode: 'per-group', count: 2 } };
  const semifinal = { ...newStage('Semifinal', 4), id: 'semi', kind: 'knockout', groupCount: 1, opponents: 'all', roundCount: 1, source: { stageId: 'league', selection: 'qualified' }, qualification: { mode: 'winners', count: 2 } };
  const final = { ...semifinal, id: 'final', name: 'Final', source: { stageId: 'semi', selection: 'winners' }, qualification: { mode: 'winners', count: 1 } };
  const playoff = { ...final, id: 'playoff', name: 'Playoff dos derrotados', source: { stageId: 'semi', selection: 'losers' } };
  const format = { id: 'format-1', name: 'Formato', version: 1, participantCount: 8, championStageId: 'final', matchRules: { maxSubstitutions: 5 }, stages: [league, semifinal, final, playoff], outcomes: [] };
  return { format, document: { competitionFormats: [format], competitions: [], competitionEditions: [] } };
}

test('format renders one selected stage and keeps independent title and branch source controls', () => {
  const { format, document } = fixture();
  const html = formatForm(format, document, 'playoff');
  assert.equal((html.match(/data-change-stage-kind=/g) || []).length, 1);
  assert.match(html, /data-portable-path="championStageId" data-value-mode="nullable"/);
  assert.match(html, /value="final" selected>Final/);
  assert.match(html, /data-portable-path="stages\[3\]\.source\.selection"/);
  assert.match(html, /value="losers" selected>Derrotados/);
  assert.match(html, /data-format-ranking="playoff" value="semi"/);
  assert.doesNotMatch(html, /data-format-ranking="playoff" value="final"/);
  assert.doesNotMatch(html, /data-portable-path="stages\[3\]\.qualification\.count"/);
});

test('league provides group opponent rules, numeric fields, ranking and ordered tiebreakers', () => {
  const { format, document } = fixture();
  const html = formatForm(format, document, 'league');
  assert.match(html, /value="cross-group" selected>Somente clubes de outros grupos/);
  assert.match(html, /data-portable-path="stages\[0\]\.groupCount" data-value-mode="number"/);
  assert.match(html, /data-portable-path="stages\[0\]\.qualification\.count" data-value-mode="number"/);
  assert.match(html, /data-format-action="move-tie"/);
  assert.match(html, /Último critério: sorteio/);
  assert.doesNotMatch(html, /data-portable-path="stages\[0\]\.source/);
});

test('format escapes authored labels and indicates shared references and blocked removal', () => {
  const { format, document } = fixture();
  format.name = '<script>bad()</script>'; format.stages[0].name = '<img src=x>';
  document.competitions.push({ id: 'c1', name: '<b>Teste</b>', defaultFormatId: format.id });
  const html = formatForm(format, document);
  assert.doesNotMatch(html, /<script>|<img src=x>|<b>Teste/);
  assert.match(html, /&lt;script&gt;bad\(\)&lt;\/script&gt;/);
  assert.match(html, /Este formato é compartilhado/);
  assert.match(html, /Padrão de &lt;b&gt;Teste&lt;\/b&gt;/);
  assert.match(html, /data-format-action="remove-stage" data-id="league" disabled/);
});

test('unsupported away goal draft remains visible so validation can be corrected', () => {
  const { format, document } = fixture();
  format.stages[2].awayGoals = true;
  const html = formatForm(format, document, 'final');
  assert.match(html, /data-portable-path="stages\[2\]\.awayGoals" data-value-mode="boolean"/);
  assert.match(html, /Esta vantagem exige dois jogos em campos dos clubes/);
});
