import { createId } from './model.js';
import { validDate } from './history-model.js';
import { leagueTieBreakers, paulistaTieBreakers, roundCount } from './competition-rules.js';

export function ensureCompetitionCollections(document) {
  document.schemaVersion = Math.max(3, document.schemaVersion);
  document.competitions ??= []; document.competitionEditions ??= [];
}

export function newEdition(document, competitionId, name) {
  ensureCompetitionCollections(document);
  const edition = { id: createId('edition'), competitionId, name, participantClubIds: [], roundDates: [],
    rules: { type: 'round-robin', version: 1, legs: 1, points: { win: 3, draw: 1, loss: 0 }, tieBreakers: [...leagueTieBreakers] } };
  document.competitionEditions.push(edition);
  return edition;
}

export function changeFormat(document, edition, type) {
  if (type === edition.rules.type) return;
  if (type === 'paulista-2026' && document.schemaVersion < 4) throw new Error('O formato Paulista 2026 exige a base com países e estádios.');
  if (!['round-robin', 'paulista-2026'].includes(type)) throw new Error('Formato não suportado.');
  edition.rules.type = type;
  edition.rules.version = 1;
  if (type === 'paulista-2026') {
    document.schemaVersion = 5;
    edition.rules.legs = 1;
    edition.rules.points = { win: 3, draw: 1, loss: 0 };
    edition.rules.tieBreakers = [...paulistaTieBreakers];
    edition.roundDates = Array.from({ length: 8 }, (_, index) => edition.roundDates[index] || '');
    edition.authoredFixtures = [];
    edition.playoffDates = Array(8).fill('');
  } else {
    edition.rules.tieBreakers = [...leagueTieBreakers];
    delete edition.authoredFixtures; delete edition.playoffDates;
  }
}

export function setEditionValue(edition, path, value, numeric = false) {
  const keys = path.replace(/\[(\d+)\]/g, '.$1').split('.');
  let owner = edition;
  for (const key of keys.slice(0, -1)) owner = owner[key];
  owner[keys.at(-1)] = numeric ? (value === '' ? null : Number(value)) : value;
}

export function setParticipant(edition, clubId, selected) {
  if (selected && !edition.participantClubIds.includes(clubId)) edition.participantClubIds.push(clubId);
  if (!selected) edition.participantClubIds = edition.participantClubIds.filter(id => id !== clubId);
}

export function generateDates(edition, start, interval) {
  if (!validDate(start) || !Number.isInteger(interval) || interval < 1 || interval > 365) throw new Error('Informe uma data real e um intervalo de 1 a 365 dias.');
  if (edition.participantClubIds.length < 2) throw new Error('Selecione pelo menos dois participantes antes de gerar as datas.');
  const date = new Date(`${start}T12:00:00Z`); const dates = [];
  for (let i = 0; i < roundCount(edition); i++) {
    const iso = date.toISOString().slice(0, 10);
    if (!validDate(iso)) throw new Error('As datas geradas devem permanecer entre os anos 0001 e 9999.');
    dates.push(iso); date.setUTCDate(date.getUTCDate() + interval);
  }
  edition.roundDates = dates;
}

export function addAuthoredFixture(edition, round) {
  if (!edition.authoredFixtures || edition.authoredFixtures.length >= 64) return null;
  const fixture = { id: createId('fixture'), round, date: edition.roundDates[round - 1] || '', homeClubId: '', awayClubId: '' };
  edition.authoredFixtures.push(fixture); return fixture;
}

export function competitionDeletionBlock(document, view, record) {
  if (view === 'competitions' && document.competitionEditions?.some(edition => edition.competitionId === record.id))
    return 'Exclua ou vincule as edições a outro campeonato antes de excluir este cadastro.';
  return '';
}
