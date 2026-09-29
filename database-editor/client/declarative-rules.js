// Pure portable validation. Shared by the editor draft and the server before publication.
import { validDate } from './history-model.js';
export const formatTieBreakers = ['wins', 'goal-difference', 'goals-for', 'red-cards', 'yellow-cards', 'seeded-draw'];
export const safeMediaUri = value => {
  if (typeof value !== 'string' || value.length > 2048 || !/^(?:https:\/\/[^\s/?#]+(?:[/?#][^\s]*)?|[A-Za-z0-9_-][A-Za-z0-9._-]*(?:\/[A-Za-z0-9_-][A-Za-z0-9._-]*)*)$/.test(value)) return false;
  if (!value.startsWith('https://')) return true;
  try { const url = new URL(value); return !url.username && !url.password && !value.includes('\\'); } catch { return false; }
};
export function eligibleClub(club, eligibility) {
  if (!eligibility) return true;
  return (!eligibility.countryCodes?.length || eligibility.countryCodes.includes(club.countryCode)) &&
    (!eligibility.stateCodes?.length || eligibility.stateCodes.includes(club.stateCode)) &&
    (!eligibility.allowedClubIds?.length || eligibility.allowedClubIds.includes(club.id)) &&
    !eligibility.excludedClubIds?.includes(club.id);
}
export function stageParticipantCounts(format) {
  const stages = format.stages || [], counts = [];
  stages.forEach((stage, index) => {
    if (!index && !stage.source) { counts.push(format.participantCount); return; }
    const sourceIndex = stage.source ? stages.findIndex(s => s.id === stage.source.stageId) : index - 1;
    const source = stages[sourceIndex], mode = stage.source?.selection || 'qualified', q = source?.qualification || {};
    counts.push(sourceIndex < 0 || sourceIndex >= index ? 0 : mode === 'qualified' ? q.count * (q.mode === 'per-group' ? source.groupCount : 1) : counts[sourceIndex] / 2);
  }); return counts;
}
function sourceIndex(stages, index) { return stages[index]?.source ? stages.findIndex(s => s.id === stages[index].source.stageId) : index - 1; }
function ancestry(stages, index) { const ids = []; for (let i = index; i >= 0 && i < stages.length && !ids.includes(stages[i].id); i = sourceIndex(stages, i)) ids.push(stages[i].id); return ids; }
function knockoutCohort(stages, index) {
  const path = new Map(), seen = new Set();
  while (index >= 0 && index < stages.length && !seen.has(index)) {
    seen.add(index);
    const parentIndex = sourceIndex(stages, index), parent = stages[parentIndex];
    if (parent?.kind === 'knockout') path.set(parent.id, stages[index].source?.selection === 'losers' ? 'losers' : 'winners');
    index = parentIndex;
  }
  return path;
}
function effectiveDates(schedule) {
  return schedule.authoredFixtures?.length ? schedule.authoredFixtures.map(f => f.date) :
    schedule.fixtureDates?.length ? schedule.fixtureDates : schedule.roundDates || [];
}
export function validateDeclarativeCompetitions(document, legacyValidator) {
  const issues = []; const issue = (path, message) => issues.push({ path, message });
  const formats = document.competitionFormats || [], editions = document.competitionEditions || [], competitions = document.competitions || [];
  const clubs = new Map((document.clubs || []).map(c => [c.id, c])); const countries = new Set((document.countries || []).map(c => c.code));
  const stadiums = new Set((document.stadiums || []).map(s => s.id));
  const unique = (items, path, key = 'id') => { const seen = new Set(); (items || []).forEach((v, i) => { if (seen.has(v[key])) issue(`${path}[${i}].${key}`, 'Identificador repetido.'); seen.add(v[key]); }); return seen; };
  const uniqueValues = (items, path) => { if (new Set(items).size !== items.length) issue(path, 'Não repita valores nesta lista.'); };
  const int = (value, min, max) => Number.isInteger(value) && value >= min && value <= max;
  const name = (value, path) => { if (typeof value !== 'string' || !value.trim() || [...value].length > 100) issue(path, 'Informe um nome de até 100 caracteres.'); };
  unique(formats, 'competitionFormats'); unique(editions, 'competitionEditions');
  const competitionIds = unique(competitions, 'competitions');
  if (formats.length > 128) issue('competitionFormats', 'A base aceita até 128 formatos.');
  const formatMap = new Map(formats.map(f => [f.id, f]));
  const checkRanks = (value, stage, count, path) => {
    const limit = value.ranking === 'per-group' ? count / stage.groupCount : count;
    if (!int(value.fromRank, 1, limit) || !int(value.toRank, value.fromRank, limit)) issue(path, 'O intervalo de posições deve caber na classificação da fase.');
    if (value.ranking === 'per-group' && stage.kind !== 'league') issue(`${path}.ranking`, 'Classificação por grupo exige uma fase de liga.');
  };
  formats.forEach((format, fi) => {
    const fp = `competitionFormats[${fi}]`; name(format.name, `${fp}.name`);
    if (format.version !== 1) issue(`${fp}.version`, 'A versão de formato suportada é 1.');
    if (!int(format.participantCount, 2, 64)) issue(`${fp}.participantCount`, 'Informe de 2 a 64 participantes.');
    if (!int(format.matchRules?.maxSubstitutions, 0, 11)) issue(`${fp}.matchRules.maxSubstitutions`, 'Informe de 0 a 11 substituições.');
    const stages = format.stages || [], counts = stageParticipantCounts(format); unique(stages, `${fp}.stages`);
    if (!stages.length || stages.length > 16) issue(`${fp}.stages`, 'Inclua de 1 a 16 fases.');
    const championId = Object.hasOwn(format, 'championStageId') ? format.championStageId : stages.at(-1)?.id;
    if (championId !== null && !stages.some(s => s.id === championId)) issue(`${fp}.championStageId`, 'Selecione uma fase existente para conceder o título ou nenhuma.');
    stages.forEach((stage, si) => {
      const sp = `${fp}.stages[${si}]`, n = counts[si], q = stage.qualification || {}; name(stage.name, `${sp}.name`);
      const parentIndex = sourceIndex(stages, si), parent = stages[parentIndex];
      if (stage.source && (parentIndex < 0 || parentIndex >= si || !['qualified', 'winners', 'losers'].includes(stage.source.selection))) issue(`${sp}.source`, 'A origem deve ser uma fase anterior, com classificados, vencedores ou derrotados.');
      if (stage.source && ['winners', 'losers'].includes(stage.source.selection) && parent?.kind !== 'knockout') issue(`${sp}.source.selection`, 'Vencedores e derrotados exigem uma origem de mata-mata.');
      if (!int(n, 2, 64)) issue(`${sp}.source`, 'A origem deve fornecer de 2 a 64 clubes.');
      if (!['league', 'knockout'].includes(stage.kind)) issue(`${sp}.kind`, 'Escolha liga ou mata-mata.');
      if (!int(stage.groupCount, 1, 32) || n % stage.groupCount || n / stage.groupCount < 2) issue(`${sp}.groupCount`, 'Os grupos precisam ter a mesma quantidade de clubes e pelo menos dois clubes.');
      if (![1, 2].includes(stage.legs)) issue(`${sp}.legs`, 'Escolha um ou dois jogos por confronto.');
      if (!int(stage.roundCount, 1, 128)) issue(`${sp}.roundCount`, 'Informe de 1 a 128 rodadas.');
      if (!['all', 'same-group', 'cross-group', 'authored'].includes(stage.opponents)) issue(`${sp}.opponents`, 'Escolha uma regra de confrontos suportada.');
      if (stage.opponents === 'cross-group' && stage.groupCount < 2) issue(`${sp}.opponents`, 'Esta regra exige pelo menos dois grupos.');
      if (!['seeded', 'draw', 'cross-group'].includes(stage.pairing)) issue(`${sp}.pairing`, 'Escolha campanha, sorteio ou cruzamento de grupos.');
      if (!['seeded', 'first-listed', 'draw', 'neutral'].includes(stage.venue)) issue(`${sp}.venue`, 'Escolha um mando suportado.');
      const points = stage.points || {};
      if (![points.win, points.draw, points.loss].every(v => int(v, 0, 100)) || points.win <= points.draw || points.draw < points.loss) issue(`${sp}.points`, 'Use pontos inteiros: vitória > empate ≥ derrota.');
      const ties = stage.tieBreakers || []; uniqueValues(ties, `${sp}.tieBreakers`);
      if (!ties.length || ties.some(v => !formatTieBreakers.includes(v)) || ties.at(-1) !== 'seeded-draw') issue(`${sp}.tieBreakers`, 'Escolha critérios válidos, sem repetição, terminando em sorteio.');
      if (!['overall', 'per-group', 'winners'].includes(q.mode) || !int(q.count, 1, q.mode === 'per-group' ? n / stage.groupCount : n)) issue(`${sp}.qualification`, 'A quantidade de classificados deve caber nesta fase.');
      const ranking = q.rankingStageIds || []; uniqueValues(ranking, `${sp}.qualification.rankingStageIds`);
      ranking.forEach((id, i) => { if (!ancestry(stages, si).includes(id)) issue(`${sp}.qualification.rankingStageIds[${i}]`, 'Use somente esta fase ou fases de origem deste caminho.'); });
      if (stage.kind === 'league') {
        if (stage.pairing !== 'seeded' || stage.tiedWinner !== 'penalties') issue(sp, 'Os controles exclusivos de mata-mata devem manter os valores padrão em fases de liga.');
        if (q.mode === 'winners') issue(`${sp}.qualification.mode`, 'Vencedores exige mata-mata.');
        if (stage.awayGoals) issue(`${sp}.awayGoals`, 'Gols fora só se aplicam a mata-mata com ida e volta.');
        if (stage.opponents === 'authored' && si !== 0) issue(`${sp}.opponents`, 'Calendário autoral exige participantes conhecidos na primeira fase.');
      } else {
        if (n % 2 || stage.groupCount !== 1 || stage.opponents !== 'all' || stage.roundCount !== stage.legs) issue(sp, 'Cada fase de mata-mata exige número par de clubes, um grupo e uma rodada por jogo de ida/volta.');
        if (q.mode !== 'winners' || q.count !== n / 2) issue(`${sp}.qualification`, 'No mata-mata avançam exatamente os vencedores dos confrontos.');
        if (stage.awayGoals && (stage.legs !== 2 || stage.venue === 'neutral')) issue(`${sp}.awayGoals`, 'Gols fora exigem ida e volta em campos dos clubes.');
        if (stage.pairing === 'cross-group') {
          const previous = parent;
          if (!previous || previous.kind !== 'league' || previous.groupCount < 2 || previous.qualification?.mode !== 'per-group') issue(`${sp}.pairing`, 'O cruzamento exige uma liga anterior com vários grupos e vagas por grupo.');
        }
        if (stage.id === championId && n !== 2) issue(sp, 'A fase que concede o título deve produzir um único campeão.');
      }
      if (!['penalties', 'higher-seed'].includes(stage.tiedWinner)) issue(`${sp}.tiedWinner`, 'Escolha pênaltis ou melhor campanha.');
      if (stage.tiedWinner === 'higher-seed' && stage.pairing !== 'seeded') issue(`${sp}.tiedWinner`, 'Vitória por campanha exige emparelhamento por campanha.');
    });
    unique(format.outcomes || [], `${fp}.outcomes`);
    (format.outcomes || []).forEach((outcome, oi) => {
      const op = `${fp}.outcomes[${oi}]`; name(outcome.label, `${op}.label`); const si = stages.findIndex(s => s.id === outcome.stageId);
      if (si < 0) issue(`${op}.stageId`, 'Selecione uma fase deste formato.'); else checkRanks(outcome, stages[si], counts[si], op);
      if (!['qualification', 'promotion', 'relegation'].includes(outcome.kind)) issue(`${op}.kind`, 'Escolha classificação, acesso ou rebaixamento.');
    });
  });
  competitions.forEach((competition, ci) => {
    const cp = `competitions[${ci}]`; name(competition.name, `${cp}.name`);
    if (competition.defaultFormatId !== undefined && !formatMap.has(competition.defaultFormatId)) issue(`${cp}.defaultFormatId`, 'Selecione um formato cadastrado.');
    for (const field of ['logoUri', 'trophyImageUri', 'trophyModelUri']) if (competition[field] !== undefined && !safeMediaUri(competition[field])) issue(`${cp}.${field}`, 'Use HTTPS ou um caminho relativo de pacote sem navegação entre pastas.');
    const eligibility = competition.eligibility;
    if (eligibility) {
      for (const field of ['countryCodes', 'stateCodes', 'allowedClubIds', 'excludedClubIds']) uniqueValues(eligibility[field] || [], `${cp}.eligibility.${field}`);
      if (eligibility.stateCodes?.length && eligibility.countryCodes?.length !== 1) issue(`${cp}.eligibility.countryCodes`, 'O filtro estadual exige exatamente um país.');
      (eligibility.countryCodes || []).forEach((code, i) => { if (!countries.has(code)) issue(`${cp}.eligibility.countryCodes[${i}]`, 'País não cadastrado.'); });
      for (const field of ['allowedClubIds', 'excludedClubIds']) (eligibility[field] || []).forEach((id, i) => { if (!clubs.has(id)) issue(`${cp}.eligibility.${field}[${i}]`, 'Clube não cadastrado.'); });
    }
    const assignedFormats = [...new Set([competition.defaultFormatId, ...editions.filter(e => e.competitionId === competition.id).map(e => e.formatId)].filter(Boolean))].map(id => formatMap.get(id)).filter(Boolean);
    unique(competition.qualificationRoutes || [], `${cp}.qualificationRoutes`, 'outcomeId');
    (competition.qualificationRoutes || []).forEach((route, ri) => {
      const rp = `${cp}.qualificationRoutes[${ri}]`;
      if (!competitionIds.has(route.targetCompetitionId)) issue(`${rp}.targetCompetitionId`, 'Campeonato de destino não encontrado.');
      if (route.targetCompetitionId === competition.id) issue(`${rp}.targetCompetitionId`, 'Selecione outro campeonato como destino.');
      if (!assignedFormats.length || assignedFormats.some(f => !(f.outcomes || []).some(o => o.id === route.outcomeId))) issue(`${rp}.outcomeId`, 'A vaga deve existir no formato padrão e em todas as edições deste campeonato.');
    });
    const awardedRanks = new Set();
    (competition.prizes?.rankingAwards || []).forEach((award, ai) => {
      const ap = `${cp}.prizes.rankingAwards[${ai}]`;
      for (let rank = award.fromRank; rank <= Math.min(64, award.toRank); rank++) { const key = `${award.stageId}:${award.ranking}:${rank}`; if (awardedRanks.has(key)) issue(ap, 'Os intervalos de premiação de uma fase não podem se sobrepor.'); awardedRanks.add(key); }
      if (!assignedFormats.length) issue(`${ap}.stageId`, 'Vincule um formato para configurar premiação por fase.');
      assignedFormats.forEach(format => { const si = format.stages.findIndex(s => s.id === award.stageId); if (si < 0) issue(`${ap}.stageId`, 'A fase deve existir nos formatos atribuídos.'); else checkRanks(award, format.stages[si], stageParticipantCounts(format)[si], ap); });
    });
  });
  if (legacyValidator) {
    const legacy = editions.map((edition, index) => ({ edition, index })).filter(item => !item.edition.formatId);
    issues.push(...legacyValidator({ ...document, schemaVersion: 5, competitionEditions: legacy.map(item => item.edition) })
      .map(item => ({ ...item, path: item.path.replace(/^competitionEditions\[(\d+)\]/, (_, index) => `competitionEditions[${legacy[Number(index)].index}]`) })));
  }
  editions.forEach((edition, ei) => {
    if (!edition.formatId) {
      const competition = competitions.find(c => c.id === edition.competitionId);
      (edition.participantClubIds || []).forEach((id, pi) => { const club = clubs.get(id); if (club && !eligibleClub(club, competition?.eligibility)) issue(`competitionEditions[${ei}].participantClubIds[${pi}]`, 'Este clube não atende à elegibilidade do campeonato.'); });
      return;
    }
    const ep = `competitionEditions[${ei}]`; name(edition.name, `${ep}.name`);
    const competition = competitions.find(c => c.id === edition.competitionId), format = formatMap.get(edition.formatId);
    if (!competition) issue(`${ep}.competitionId`, 'Campeonato não encontrado.');
    if (!format) { issue(`${ep}.formatId`, 'Formato não encontrado.'); return; }
    const participants = edition.participantClubIds || []; uniqueValues(participants, `${ep}.participantClubIds`);
    if (participants.length !== format.participantCount) issue(`${ep}.participantClubIds`, `O formato exige ${format.participantCount} participantes.`);
    participants.forEach((id, pi) => { const club = clubs.get(id); if (!club) issue(`${ep}.participantClubIds[${pi}]`, 'Clube não encontrado.'); else if (!eligibleClub(club, competition?.eligibility)) issue(`${ep}.participantClubIds[${pi}]`, 'Este clube não atende à elegibilidade do campeonato.'); });
    const schedules = edition.stageSchedules || [], counts = stageParticipantCounts(format), stages = format.stages || []; unique(schedules, `${ep}.stageSchedules`, 'stageId');
    if (schedules.length !== stages.length || schedules.some((s, i) => s.stageId !== stages[i]?.id)) issue(`${ep}.stageSchedules`, 'Informe um calendário por fase, na ordem do formato.');
    for (let left = 0; left < schedules.length; left++) for (let right = left + 1; right < schedules.length; right++) {
      const rightDates = new Set(effectiveDates(schedules[right]));
      if (!effectiveDates(schedules[left]).some(date => rightDates.has(date))) continue;
      const leftPath = knockoutCohort(stages, left), rightPath = knockoutCohort(stages, right);
      if (![...leftPath].some(([id, choice]) => rightPath.has(id) && rightPath.get(id) !== choice))
        issue(`${ep}.stageSchedules[${right}]`, 'Fases que podem compartilhar clubes não podem ter partidas na mesma data.');
    }
    const fixtureIds = new Set(); let totalFixtures = 0;
    schedules.forEach((schedule, si) => {
      const sp = `${ep}.stageSchedules[${si}]`, stage = stages[si]; if (!stage || schedule.stageId !== stage.id) return;
      const dates = schedule.roundDates || [], n = counts[si], parentIndex = sourceIndex(stages, si), parentSchedule = schedules[parentIndex];
      const childStarts = schedules.filter((_, ci) => sourceIndex(stages, ci) === si).map(s => s.roundDates?.[0]).filter(Boolean).sort();
      const nextStart = childStarts[0];
      const parentEnd = parentSchedule ? [...(parentSchedule.roundDates || []), ...(parentSchedule.fixtureDates || []), ...(parentSchedule.authoredFixtures || []).map(f => f.date)].sort().at(-1) : undefined;
      if (dates.length !== stage.roundCount) issue(`${sp}.roundDates`, `Informe ${stage.roundCount} datas, uma por rodada.`);
      dates.forEach((date, di) => { if (!validDate(date)) issue(`${sp}.roundDates[${di}]`, 'Informe uma data real AAAA-MM-DD.'); else if (di && date <= dates[di - 1] || parentEnd && date <= parentEnd) issue(`${sp}.roundDates[${di}]`, 'As datas devem seguir as rodadas e a conclusão da fase de origem.'); });
      const groups = schedule.groups || []; unique(groups, `${sp}.groups`);
      if (stage.kind === 'knockout' && groups.length) issue(`${sp}.groups`, 'Fases de mata-mata não possuem grupos de liga.');
      const groupOf = new Map();
      if (groups.length) {
        if (groups.length !== stage.groupCount) issue(`${sp}.groups`, `Informe exatamente ${stage.groupCount} grupos.`);
        groups.forEach((group, gi) => {
          const ids = si === 0 ? group.clubIds || [] : group.seedRanks || [];
          if (ids.length !== n / stage.groupCount) issue(`${sp}.groups[${gi}]`, 'Divida os participantes igualmente entre os grupos.');
          if (si === 0 && group.seedRanks || si > 0 && group.clubIds) issue(`${sp}.groups[${gi}]`, si === 0 ? 'A primeira fase usa clubes.' : 'Fases posteriores usam posições de entrada.');
          ids.forEach(id => { if (groupOf.has(id)) issue(`${sp}.groups[${gi}]`, 'Um participante não pode estar em dois grupos.'); groupOf.set(id, gi); if (si === 0 ? !participants.includes(id) : !int(id, 1, n)) issue(`${sp}.groups[${gi}]`, 'Participante fora desta fase.'); });
        });
        if (groupOf.size !== n) issue(`${sp}.groups`, 'Os grupos devem conter todos os participantes.');
      } else if (si === 0 && stage.groupCount > 1) issue(`${sp}.groups`, 'Distribua os clubes da primeira fase entre os grupos.');
      if (stage.venue === 'neutral' && !schedule.neutralStadiumId) issue(`${sp}.neutralStadiumId`, 'Escolha o estádio neutro.');
      if (schedule.neutralStadiumId !== undefined && !stadiums.has(schedule.neutralStadiumId)) issue(`${sp}.neutralStadiumId`, 'Estádio não encontrado.');
      if (stage.venue !== 'neutral' && schedule.neutralStadiumId !== undefined) issue(`${sp}.neutralStadiumId`, 'Estádio neutro exige mando neutro no formato.');
      const authored = schedule.authoredFixtures || [], pairs = new Map(), appearances = new Set();
      totalFixtures += stage.kind === 'knockout' ? n / 2 * stage.legs : authored.length ||
        (stage.opponents === 'same-group' ? n * (n / stage.groupCount - 1) / 2 * stage.legs : stage.opponents === 'cross-group' ? n * (n - n / stage.groupCount) / 2 * stage.legs : n * (n - 1) / 2 * stage.legs);
      if (totalFixtures > 4096) issue(`${ep}.stageSchedules`, 'A edição aceita no máximo 4096 partidas.');
      if (authored.length && (si !== 0 || stage.kind !== 'league')) issue(`${sp}.authoredFixtures`, 'Confrontos autorais são aceitos apenas na liga inicial.');
      if (stage.opponents === 'authored' && !authored.length) issue(`${sp}.authoredFixtures`, 'Preencha os confrontos do calendário autoral.');
      if (!authored.length && stage.kind === 'league' && stage.opponents !== 'authored') {
        const schedulingCount = stage.opponents === 'same-group' ? n / stage.groupCount : n;
        const expected = (schedulingCount % 2 ? schedulingCount : schedulingCount - 1) * stage.legs;
        if (stage.roundCount !== expected) issue(`${sp}.roundDates`, `A geração deste calendário exige ${expected} rodadas; use confrontos autorais para outra distribuição.`);
      }
      authored.forEach((fixture, fi) => {
        const ap = `${sp}.authoredFixtures[${fi}]`;
        if (fixtureIds.has(fixture.id)) issue(`${ap}.id`, 'Identificador de confronto repetido na edição.'); fixtureIds.add(fixture.id);
        if (!int(fixture.round, 1, stage.roundCount)) issue(`${ap}.round`, 'Rodada fora da fase.');
        if (fixture.homeClubId === fixture.awayClubId) issue(`${ap}.awayClubId`, 'Escolha dois clubes diferentes.');
        for (const field of ['homeClubId', 'awayClubId']) { const id = fixture[field], key = `${fixture.round}:${id}`; if (!participants.includes(id)) issue(`${ap}.${field}`, 'Clube não participante.'); if (appearances.has(key)) issue(`${ap}.${field}`, 'O clube já tem partida nesta rodada.'); appearances.add(key); }
        const same = groupOf.get(fixture.homeClubId) === groupOf.get(fixture.awayClubId);
        if (stage.opponents === 'same-group' && !same || stage.opponents === 'cross-group' && same) issue(ap, 'O confronto viola a regra entre grupos.');
        const key = [fixture.homeClubId, fixture.awayClubId].sort().join('|'); const previous = pairs.get(key) || []; previous.push(fixture); pairs.set(key, previous);
        if (previous.length > stage.legs || stage.legs === 2 && previous.length === 2 && previous[0].homeClubId === fixture.homeClubId) issue(ap, 'O par deve jogar apenas o número de vezes previsto, com mandos invertidos na volta.');
        const end = dates[fixture.round] || nextStart;
        if (!validDate(fixture.date) || fixture.date < dates[fixture.round - 1] || end && fixture.date >= end) issue(`${ap}.date`, 'A data deve caber na rodada e anteceder a próxima fase.');
        if (fixture.stadiumId !== undefined && !stadiums.has(fixture.stadiumId)) issue(`${ap}.stadiumId`, 'Estádio não encontrado.');
        if (stage.venue === 'neutral' && fixture.stadiumId !== undefined && fixture.stadiumId !== schedule.neutralStadiumId) issue(`${ap}.stadiumId`, 'Use o estádio neutro definido para a fase.');
      });
      if (authored.length && stage.opponents !== 'authored') for (let a = 0; a < participants.length; a++) for (let b = a + 1; b < participants.length; b++) {
        const same = groupOf.get(participants[a]) === groupOf.get(participants[b]);
        const allowed = stage.opponents === 'all' || stage.opponents === 'same-group' && same || stage.opponents === 'cross-group' && !same;
        if (allowed && pairs.get([participants[a], participants[b]].sort().join('|'))?.length !== stage.legs) issue(`${sp}.authoredFixtures`, 'Inclua todos os confrontos exigidos pela regra e pelos turnos.');
      }
      if (schedule.fixtureDates) {
        if (stage.kind !== 'knockout' || schedule.fixtureDates.length !== n / 2 * stage.legs) issue(`${sp}.fixtureDates`, 'Informe uma data para cada jogo do mata-mata.');
        schedule.fixtureDates.forEach((date, di) => { const round = Math.floor(di / (n / 2)), end = dates[round + 1] || nextStart; if (!validDate(date) || date < dates[round] || end && date >= end) issue(`${sp}.fixtureDates[${di}]`, 'A data deve caber na rodada do mata-mata.'); });
      }
    });
  });
  return issues;
}
