// Portable, side-effect-free validation shared by the browser draft and Node adapter.
// Authored definitions only: results, tables and mutable career state never live here.
import { validDate } from './history-model.js';
import { validateDeclarativeCompetitions } from './declarative-rules.js';

export const leagueTieBreakers = ['wins', 'goal-difference', 'goals-for'];
export const paulistaTieBreakers = [...leagueTieBreakers, 'red-cards', 'yellow-cards', 'drawing-lots'];
export const playoffLabels = ['Quartas: 1º × 8º', 'Quartas: 2º × 7º', 'Quartas: 3º × 6º', 'Quartas: 4º × 5º', 'Semifinal: 1º × 4º', 'Semifinal: 2º × 3º', 'Final: ida', 'Final: volta'];
export const roundCount = edition => edition.rules.type === 'paulista-2026' ? 8 : (edition.participantClubIds.length % 2 ? edition.participantClubIds.length : edition.participantClubIds.length - 1) * edition.rules.legs;

export function validateCompetitions(document) {
  if (document.schemaVersion >= 6) return validateDeclarativeCompetitions(document, validateCompetitions);
  if (document.schemaVersion < 3) return [];
  const issues = []; const issue = (path, message) => issues.push({ path, message });
  const name = (value, path) => { if (typeof value !== 'string' || !value.trim() || [...value].length > 100) issue(path, 'Informe um nome de até 100 caracteres.'); };
  const unique = (items, path) => { const seen = new Set(); items.forEach((item, index) => { if (seen.has(item.id)) issue(`${path}[${index}].id`, 'Identificador repetido.'); seen.add(item.id); }); return seen; };
  const competitions = document.competitions || []; const editions = document.competitionEditions || [];
  const competitionIds = unique(competitions, 'competitions'); unique(editions, 'competitionEditions');
  if (competitions.length > 128) issue('competitions', 'A base aceita até 128 campeonatos.');
  if (editions.length > 128) issue('competitionEditions', 'A base aceita até 128 edições.');
  const clubIds = new Set(document.clubs.map(club => club.id));
  const stadiumIds = new Set((document.stadiums || []).map(stadium => stadium.id));
  competitions.forEach((competition, index) => name(competition.name, `competitions[${index}].name`));
  editions.forEach((edition, index) => {
    const path = `competitionEditions[${index}]`;
    name(edition.name, `${path}.name`);
    if (!competitionIds.has(edition.competitionId)) issue(`${path}.competitionId`, 'Selecione um campeonato cadastrado.');
    const participants = new Set(edition.participantClubIds);
    if (participants.size !== edition.participantClubIds.length) issue(`${path}.participantClubIds`, 'Selecione cada clube apenas uma vez.');
    edition.participantClubIds.forEach((id, i) => { if (!clubIds.has(id)) issue(`${path}.participantClubIds[${i}]`, 'Um participante não existe na base.'); });
    if (participants.size < 2 || participants.size > 64) issue(`${path}.participantClubIds`, 'Selecione de 2 a 64 clubes.');
    const rules = edition.rules;
    const paulista = rules.type === 'paulista-2026';
    if (!['round-robin', 'paulista-2026'].includes(rules.type) || (paulista && document.schemaVersion < 5)) issue(`${path}.rules.type`, 'Escolha um formato suportado por esta versão da base.');
    if (rules.version !== 1) issue(`${path}.rules.version`, 'A versão de regulamento suportada é 1.');
    if (![1, 2].includes(rules.legs)) issue(`${path}.rules.legs`, 'Escolha um ou dois turnos.');
    const { win, draw, loss } = rules.points;
    if ([win, draw, loss].some(value => !Number.isInteger(value) || value < 0 || value > 100) || win <= draw || draw < loss) issue(`${path}.rules.points`, 'Use pontos inteiros entre 0 e 100: vitória > empate ≥ derrota.');
    if (JSON.stringify(rules.tieBreakers) !== JSON.stringify(paulista ? paulistaTieBreakers : leagueTieBreakers)) issue(`${path}.rules.tieBreakers`, 'Os critérios devem seguir a ordem do formato selecionado.');
    if (edition.roundDates.length !== roundCount(edition)) issue(`${path}.roundDates`, `Informe ${roundCount(edition)} datas, uma por rodada.`);
    edition.roundDates.forEach((date, round) => {
      if (!validDate(date)) issue(`${path}.roundDates[${round}]`, 'Informe uma data real no formato AAAA-MM-DD.');
      else if (round && date <= edition.roundDates[round - 1]) issue(`${path}.roundDates[${round}]`, 'As rodadas devem começar em datas crescentes.');
    });
    if (!paulista) {
      if (edition.authoredFixtures !== undefined || edition.playoffDates !== undefined) issue(`${path}.rules.type`, 'Pontos corridos usa as datas por rodada; remova o calendário de mata-mata ao mudar o formato.');
      return;
    }
    if (participants.size !== 16) issue(`${path}.participantClubIds`, 'O formato Paulista 2026 exige exatamente 16 clubes.');
    if (rules.legs !== 1 || win !== 3 || draw !== 1 || loss !== 0) issue(`${path}.rules.points`, 'O formato Paulista 2026 usa um turno parcial e pontos 3/1/0.');
    const playoffs = edition.playoffDates || [];
    if (playoffs.length !== 8) issue(`${path}.playoffDates`, 'Informe as datas dos quatro jogos de quartas, duas semifinais e duas finais.');
    playoffs.forEach((date, i) => {
      const prior = i < 4 ? edition.roundDates.slice(-1) : i < 6 ? playoffs.slice(0, 4) : i === 6 ? playoffs.slice(4, 6) : [playoffs[6]];
      if (!validDate(date) || prior.some(previous => date <= previous)) issue(`${path}.playoffDates[${i}]`, 'Informe uma data real posterior à fase anterior. Jogos da mesma fase podem ocorrer no mesmo dia.');
    });
    const fixtures = edition.authoredFixtures || [];
    if (fixtures.length !== 64) issue(`${path}.authoredFixtures`, 'A primeira fase exige 64 jogos, oito por rodada.');
    unique(fixtures, `${path}.authoredFixtures`);
    const roundClubs = new Set(); const pairs = new Set(); const counts = new Map([...participants].map(id => [id, { games: 0, home: 0 }]));
    fixtures.forEach((fixture, i) => {
      const fp = `${path}.authoredFixtures[${i}]`;
      if (!Number.isInteger(fixture.round) || fixture.round < 1 || fixture.round > 8) issue(`${fp}.round`, 'Escolha uma rodada de 1 a 8.');
      if (fixture.homeClubId === fixture.awayClubId) issue(`${fp}.awayClubId`, 'Escolha dois clubes diferentes.');
      for (const field of ['homeClubId', 'awayClubId']) {
        const id = fixture[field]; const count = counts.get(id);
        if (!count) issue(`${fp}.${field}`, 'Escolha um clube participante desta edição.');
        else { count.games++; if (field === 'homeClubId') count.home++; }
        const key = `${fixture.round}:${id}`;
        if (roundClubs.has(key)) issue(`${fp}.${field}`, 'Este clube já tem um jogo nesta rodada.');
        roundClubs.add(key);
      }
      const pair = [fixture.homeClubId, fixture.awayClubId].sort().join(':');
      if (pairs.has(pair)) issue(`${fp}.awayClubId`, 'Os adversários da primeira fase não podem se repetir.');
      pairs.add(pair);
      const start = edition.roundDates[fixture.round - 1];
      const end = edition.roundDates[fixture.round] || [...playoffs.slice(0, 4)].sort()[0];
      if (!validDate(fixture.date) || (start && fixture.date < start) || (end && fixture.date >= end)) issue(`${fp}.date`, 'O jogo deve ocorrer a partir do início da rodada e antes da próxima rodada ou fase.');
      if (fixture.stadiumId !== undefined && !stadiumIds.has(fixture.stadiumId)) issue(`${fp}.stadiumId`, 'Selecione um estádio cadastrado ou deixe o mando padrão.');
    });
    for (const [id, count] of counts) if (count.games !== 8 || count.home !== 4) issue(`${path}.authoredFixtures`, `${document.clubs.find(club => club.id === id)?.name || id}: são necessários oito jogos, quatro em casa e quatro fora.`);
  });
  return issues;
}
