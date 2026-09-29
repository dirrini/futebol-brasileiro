import { createId, clone } from './model.js';
import { validDate } from './history-model.js';
import { stageParticipantCounts } from './declarative-rules.js';

export function enableFormats(document) {
  if (document.schemaVersion < 4) throw new Error('Formatos reutilizáveis exigem uma base com países, estádios e referência histórica (v4 ou posterior).');
  document.schemaVersion = 6; document.competitionFormats ??= [];
}
export function newStage(name = 'Liga', participants = 4) {
  return { id: createId('stage'), name, kind: 'league', groupCount: 1, opponents: 'all', legs: 1,
    roundCount: participants % 2 ? participants : participants - 1, points: { win: 3, draw: 1, loss: 0 },
    tieBreakers: ['wins', 'goal-difference', 'goals-for', 'seeded-draw'], qualification: { mode: 'overall', count: 1 },
    pairing: 'seeded', venue: 'seeded', awayGoals: false, tiedWinner: 'penalties' };
}
export function addFormat(document, name) {
  enableFormats(document);
  const stage = newStage(); const format = { id: createId('format'), name, version: 1, participantCount: 4,
    championStageId: stage.id, stages: [stage], matchRules: { maxSubstitutions: 5 }, outcomes: [] };
  document.competitionFormats.push(format); return format;
}
export function duplicateFormat(document, source, name) {
  enableFormats(document);
  const format = clone(source); format.id = createId('format'); format.name = name;
  const ids = new Map(source.stages.map(stage => [stage.id, createId('stage')]));
  format.stages.forEach(stage => {
    stage.id = ids.get(stage.id);
    if (stage.source) stage.source.stageId = ids.get(stage.source.stageId);
    if (stage.qualification.rankingStageIds) stage.qualification.rankingStageIds = stage.qualification.rankingStageIds.map(id => ids.get(id));
  });
  if (format.championStageId) format.championStageId = ids.get(format.championStageId);
  format.outcomes.forEach(outcome => { outcome.id = createId('outcome'); outcome.stageId = ids.get(outcome.stageId); });
  document.competitionFormats.push(format); return format;
}
export function formatUses(document, id) {
  return [...document.competitions.filter(c => c.defaultFormatId === id).map(c => `Padrão de ${c.name}`),
    ...document.competitionEditions.filter(e => e.formatId === id).map(e => `Edição ${e.name}`)];
}
export function declarativeDeletionBlock(document, view, record) {
  if (view === 'competitionFormats' && formatUses(document, record.id).length) return 'Troque o formato padrão dos campeonatos e o formato das edições vinculadas antes de excluir.';
  if (view === 'competitions' && (document.competitions || []).some(c => c.qualificationRoutes?.some(r => r.targetCompetitionId === record.id))) return 'Remova os destinos de vagas que apontam para este campeonato antes de excluir.';
  if (view === 'clubs' && (document.competitions || []).some(c => [...(c.eligibility?.allowedClubIds || []), ...(c.eligibility?.excludedClubIds || [])].includes(record.id))) return 'Remova o clube das regras de elegibilidade antes de excluir.';
  if (view === 'countries' && (document.competitions || []).some(c => c.eligibility?.countryCodes?.includes(record.code))) return 'Remova este país das regras de elegibilidade antes de excluir.';
  if (view === 'stadiums' && (document.competitionEditions || []).some(e => e.stageSchedules?.some(s => s.neutralStadiumId === record.id || s.authoredFixtures?.some(f => f.stadiumId === record.id)))) return 'Remova este estádio dos calendários e campos neutros antes de excluir.';
  return '';
}
export function addStage(format) {
  if (format.stages.length >= 16) throw new Error('O limite é de 16 fases.');
  const previous = format.stages.at(-1), counts = stageParticipantCounts(format);
  const n = previous.qualification.count * (previous.qualification.mode === 'per-group' ? previous.groupCount : 1);
  const stage = newStage('Nova fase', Math.max(2, n || counts.at(-1)));
  stage.source = { stageId: previous.id, selection: 'qualified' };
  format.stages.push(stage); return stage;
}
export function stageRemovalBlock(document, format, stage) {
  if (format.stages.length === 1) return 'Mantenha pelo menos uma fase.';
  if (format.championStageId === stage.id) return 'Escolha outra fase para o título, ou Sem taça, antes de remover.';
  if (format.stages.some((s, i) => s.id !== stage.id && (s.source?.stageId === stage.id || !s.source && i && format.stages[i - 1].id === stage.id || s.qualification.rankingStageIds?.includes(stage.id)))) return 'Altere as origens e campanhas das outras fases antes de remover esta fase.';
  if (format.outcomes.some(o => o.stageId === stage.id)) return 'Remova ou transfira os resultados esportivos desta fase primeiro.';
  if ((document.competitionEditions || []).some(e => e.formatId === format.id && e.stageSchedules.some(s => s.stageId === stage.id))) return 'Esta fase tem calendário em uma edição. Troque o formato da edição antes de remover a fase.';
  if ((document.competitions || []).some(c => c.prizes?.rankingAwards?.some(a => a.stageId === stage.id))) return 'Remova a premiação vinculada a esta fase antes de removê-la.';
  return '';
}
export function moveStage(format, id, direction) {
  const from = format.stages.findIndex(s => s.id === id), to = from + direction;
  if (to < 0 || to >= format.stages.length) return;
  // Make the existing implicit parent explicit before reordering, preserving its meaning.
  const stages = clone(format.stages);
  stages.forEach((s, i) => { if (i && !s.source) s.source = { stageId: stages[i - 1].id, selection: 'qualified' }; });
  [stages[from], stages[to]] = [stages[to], stages[from]];
  if (stages.some((s, i) => s.source && stages.findIndex(p => p.id === s.source.stageId) >= i)) throw new Error('A fase de origem deve continuar antes de suas dependentes.');
  format.stages = stages;
}
export function changeStageKind(format, stage, kind) {
  stage.kind = kind;
  if (kind === 'knockout') {
    stage.groupCount = 1; stage.opponents = 'all'; stage.roundCount = stage.legs;
    stage.qualification = { mode: 'winners', count: stageParticipantCounts(format)[format.stages.indexOf(stage)] / 2 };
  } else {
    stage.pairing = 'seeded'; stage.tiedWinner = 'penalties'; stage.awayGoals = false;
    stage.qualification = { mode: 'overall', count: 1 }; stage.roundCount = recommendedRounds(format, stage);
  }
  recomputeKnockoutCounts(format);
}
function recomputeKnockoutCounts(format) {
  format.stages.forEach((stage, i) => {
    if (stage.kind === 'knockout') { stage.roundCount = stage.legs; stage.qualification.count = stageParticipantCounts(format)[i] / 2; }
  });
}
export function recommendedRounds(format, stage) {
  if (stage.kind === 'knockout') return stage.legs;
  const count = stageParticipantCounts(format)[format.stages.indexOf(stage)];
  const n = stage.opponents === 'same-group' ? count / stage.groupCount : count;
  return Math.max(1, (n % 2 ? n : n - 1) * stage.legs);
}
export function setPortableValue(record, path, value, mode = 'text', optional = false) {
  const stageSource = /^stages\[(\d+)\]\.source\./.exec(path);
  if (stageSource) {
    const index = Number(stageSource[1]);
    record.stages[index].source ??= { stageId: record.stages[index - 1]?.id || '', selection: 'qualified' };
  }
  const keys = path.replace(/\[(\d+)\]/g, '.$1').split('.'); let owner = record;
  for (const key of keys.slice(0, -1)) owner = owner[key] ??= {};
  const key = keys.at(-1);
  if (optional && value === '') delete owner[key];
  else owner[key] = mode === 'number' ? value === '' ? null : Number(value) : mode === 'boolean' ? value === 'true' : mode === 'nullable' ? value || null : mode === 'list' ? value.split(',').map(v => v.trim()).filter(Boolean) : value;
  if (record.stages && (path.startsWith('stages[') || path === 'participantCount')) {
    recomputeKnockoutCounts(record);
  }
}
export function scheduleFor(edition, stageId) { return edition.stageSchedules?.find(s => s.stageId === stageId); }
export function syncSchedules(edition, format) {
  edition.stageSchedules = format.stages.map(stage => scheduleFor(edition, stage.id) || { stageId: stage.id, roundDates: [], groups: [], authoredFixtures: [] });
}
export function assignFormat(document, edition, formatId) {
  enableFormats(document);
  const format = document.competitionFormats.find(f => f.id === formatId);
  if (!format) throw new Error('Selecione um formato cadastrado.');
  edition.formatId = formatId; edition.stageSchedules = [];
  delete edition.rules; delete edition.roundDates; delete edition.authoredFixtures; delete edition.playoffDates;
  syncSchedules(edition, format);
}
export function distributeGroups(edition, format, stage, serpentine = false) {
  const schedule = scheduleFor(edition, stage.id), index = format.stages.indexOf(stage);
  const inputCount = stageParticipantCounts(format)[index];
  if (!Number.isInteger(inputCount) || inputCount < 2 || inputCount > 64) throw new Error('Corrija a fase: a origem deve fornecer de 2 a 64 participantes.');
  const incoming = index ? Array.from({ length: inputCount }, (_, i) => i + 1) : [...edition.participantClubIds];
  const count = stage.groupCount;
  if (stage.kind !== 'league') throw new Error('Somente fases de liga têm grupos.');
  if (!Number.isInteger(count) || count < 1 || count > 32 || incoming.length > 64 || incoming.length % count) throw new Error('Use até 32 grupos, com até 64 participantes divididos igualmente.');
  const key = index ? 'seedRanks' : 'clubIds';
  const groups = Array.from({ length: count }, (_, i) => ({ id: schedule.groups[i]?.id || createId('group'), name: schedule.groups[i]?.name || `Grupo ${String.fromCharCode(65 + i)}`, [key]: [] }));
  incoming.forEach((value, i) => {
    const bucket = serpentine ? (Math.floor(i / count) % 2 ? count - 1 - i % count : i % count) : Math.floor(i / (incoming.length / count));
    groups[bucket][key].push(value);
  });
  schedule.groups = groups;
}
export function assignGroup(schedule, value, groupId, later) {
  const key = later ? 'seedRanks' : 'clubIds', item = later ? Number(value) : value;
  schedule.groups.forEach(g => { g[key] = (g[key] || []).filter(v => v !== item); });
  const target = schedule.groups.find(g => g.id === groupId); if (target) target[key].push(item);
}
export function generateStageDates(stage, schedule, start, interval) {
  if (!Number.isInteger(stage.roundCount) || stage.roundCount < 1 || stage.roundCount > 128) throw new Error('Corrija o formato: a fase deve ter de 1 a 128 rodadas.');
  if (!validDate(start) || !Number.isInteger(interval) || interval < 1 || interval > 365) throw new Error('Informe data real e intervalo de 1 a 365 dias.');
  const date = new Date(`${start}T12:00:00Z`), dates = [];
  for (let i = 0; i < stage.roundCount; i++) { const iso = date.toISOString().slice(0, 10); if (!validDate(iso)) throw new Error('Datas fora dos anos 0001–9999.'); dates.push(iso); date.setUTCDate(date.getUTCDate() + interval); }
  schedule.roundDates = dates;
}
export function addStageFixture(schedule, round) {
  const fixture = { id: createId('fixture'), round, date: schedule.roundDates[round - 1] || '', homeClubId: '', awayClubId: '' };
  schedule.authoredFixtures.push(fixture); return fixture;
}
export function outcomeRemovalBlock(document, outcomeId) {
  return (document.competitions || []).some(c => c.qualificationRoutes?.some(r => r.outcomeId === outcomeId)) ? 'Remova primeiro os destinos desta vaga nas fichas dos campeonatos.' : '';
}
