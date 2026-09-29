import Ajv from 'ajv';
import { EditorError } from './errors.mjs';

function isCalendarDate(value) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const [year, month, day] = value.split('-').map(Number);
  if (year < 1 || month < 1 || month > 12) return false;
  const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
  const days = [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
  return day >= 1 && day <= days[month - 1];
}

export function createValidator(schemas) {
  const ajv = new Ajv({ allErrors: true, strict: false, coerceTypes: false, useDefaults: false, removeAdditional: false });
  const validators = new Map(schemas.map(schema => [schema.properties.schemaVersion.const, ajv.compile(schema)]));
  return document => {
    const validate = validators.get(document?.schemaVersion);
    if (!validate) throw new EditorError(422, 'unsupported_schema_version', 'A versão da base deve ser 1, 2, 3 ou 4.');
    if (!validate(document)) {
      const issues = validate.errors.slice(0, 40).map(error => ({
        path: error.instancePath + (error.params.missingProperty ? '/' + error.params.missingProperty : ''),
        message: error.keyword === 'required' ? 'Campo obrigatório.' : `Valor incompatível com o contrato (${error.keyword}).`
      }));
      throw new EditorError(422, 'invalid_database', 'Revise os campos indicados antes de salvar.', issues);
    }
    const issues = [];
    const unique = (items, key, collection) => {
      const seen = new Set();
      items.forEach((item, index) => {
        if (seen.has(item[key])) issues.push({ path: `/${collection}/${index}/${key}`, message: 'Identidade repetida.' });
        seen.add(item[key]);
      });
      return seen;
    };
    const clubs = unique(document.clubs, 'id', 'clubs');
    const players = unique(document.players, 'id', 'players');
    unique(document.memberships, 'playerId', 'memberships');
    unique(document.visualProfiles, 'playerId', 'visualProfiles');
    document.memberships.forEach((item, index) => {
      if (!clubs.has(item.clubId)) issues.push({ path: `/memberships/${index}/clubId`, message: 'Clube não encontrado.' });
      if (!players.has(item.playerId)) issues.push({ path: `/memberships/${index}/playerId`, message: 'Jogador não encontrado.' });
    });
    document.visualProfiles.forEach((item, index) => {
      if (!players.has(item.playerId)) issues.push({ path: `/visualProfiles/${index}/playerId`, message: 'Jogador não encontrado.' });
      if (item.appearance && (item.skin.skinId !== 'builtin-player' || item.skin.revision !== 1 || item.skin.compatibilityProfile !== 'football-player-v1'))
        issues.push({ path: `/visualProfiles/${index}/appearance`, message: 'Essas opções exigem o modelo padrão builtin-player@1.' });
    });
    if (document.schemaVersion >= 3) {
      const competitions = unique(document.competitions, 'id', 'competitions');
      unique(document.competitionEditions, 'id', 'competitionEditions');
      document.competitionEditions.forEach((edition, index) => {
        const path = `/competitionEditions/${index}`;
        if (!competitions.has(edition.competitionId)) issues.push({ path: `${path}/competitionId`, message: 'Campeonato não encontrado.' });
        edition.participantClubIds.forEach((id, clubIndex) => {
          if (!clubs.has(id)) issues.push({ path: `${path}/participantClubIds/${clubIndex}`, message: 'Clube participante não encontrado.' });
        });
        const count = edition.participantClubIds.length;
        const expectedRounds = (count % 2 === 0 ? count - 1 : count) * edition.rules.legs;
        if (edition.roundDates.length !== expectedRounds) issues.push({ path: `${path}/roundDates`, message: `Informe ${expectedRounds} datas, uma por rodada.` });
        edition.roundDates.forEach((date, round) => {
          if (!isCalendarDate(date)) issues.push({ path: `${path}/roundDates/${round}`, message: 'Informe uma data válida no formato AAAA-MM-DD.' });
          else if (round > 0 && date <= edition.roundDates[round - 1]) issues.push({ path: `${path}/roundDates/${round}`, message: 'As datas das rodadas devem estar em ordem crescente, sem repetições.' });
        });
        const { win, draw, loss } = edition.rules.points;
        if (win <= draw || draw < loss) issues.push({ path: `${path}/rules/points`, message: 'Vitória deve valer mais que empate, e empate pelo menos o mesmo que derrota.' });
      });
    }
    if (document.schemaVersion === 4) {
      const countries = unique(document.countries, 'code', 'countries');
      const stadiums = unique(document.stadiums, 'id', 'stadiums');
      unique(document.snapshot.sources, 'id', 'snapshot/sources');
      if (!isCalendarDate(document.snapshot.date))
        issues.push({ path: '/snapshot/date', message: 'Informe uma data válida no formato AAAA-MM-DD.' });
      document.stadiums.forEach((stadium, index) => {
        if (!countries.has(stadium.countryCode)) issues.push({ path: `/stadiums/${index}/countryCode`, message: 'País não encontrado.' });
      });
      document.clubs.forEach((club, index) => {
        if (!countries.has(club.countryCode)) issues.push({ path: `/clubs/${index}/countryCode`, message: 'País não encontrado.' });
        if (club.stadiumId !== undefined && !stadiums.has(club.stadiumId))
          issues.push({ path: `/clubs/${index}/stadiumId`, message: 'Estádio não encontrado.' });
      });
      document.players.forEach((player, index) => {
        if (player.nationalityCode !== undefined && !countries.has(player.nationalityCode))
          issues.push({ path: `/players/${index}/nationalityCode`, message: 'País não encontrado.' });
        if (player.birthDate !== undefined) {
          if (!isCalendarDate(player.birthDate)) issues.push({ path: `/players/${index}/birthDate`, message: 'Informe uma data de nascimento válida no formato AAAA-MM-DD.' });
          else if (player.birthDate > document.snapshot.date) issues.push({ path: `/players/${index}/birthDate`, message: 'O nascimento não pode ser posterior à data de referência da base.' });
        }
      });
    }
    if (issues.length) throw new EditorError(422, 'invalid_references', 'A base contém vínculos ou identidades inválidos.', issues.slice(0, 40));
    return document;
  };
}
