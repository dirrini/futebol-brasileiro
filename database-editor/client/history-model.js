export const hasHistory = document => document?.schemaVersion >= 4;
export const identity = record => record?.id ?? record?.code ?? '';
export const playerDisplayName = player => player.nickname ?? player.name;
export const playerSearchText = player => [player.name, player.nickname, player.fullName].filter(value => typeof value === 'string').join(' ');
export const countryName = (document, code) => document.countries?.find(country => country.code === code)?.name || code || 'País não informado';
export const countryOptions = document => [...(document.countries || [])].sort((a, b) => a.name.localeCompare(b.name, 'pt-BR')).map(country => ({ value: country.code, label: `${country.name} (${country.code})` }));
export const rosterScopeLabel = scope => scope === 'full-squads' ? 'Elencos completos' : scope === 'matchday-squads' ? 'Relacionados para partidas' : 'Alcance não informado';
export const displayDate = value => validDate(value) ? `${value.slice(8, 10)}/${value.slice(5, 7)}/${value.slice(0, 4)}` : 'Data não informada';

export function validDate(value) {
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const [year, month, day] = value.split('-').map(Number);
  const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
  return year >= 1 && month >= 1 && month <= 12 && day >= 1 && day <= [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][month - 1];
}

// An empty optional input means unknown. Never turn an unknown number into zero or JSON null.
export function setRecordField(record, property, value, { optional = false, numeric = false } = {}) {
  if (optional && value === '') delete record[property];
  else record[property] = numeric ? (value === '' ? null : Number(value)) : value;
}

export function deletionBlock(document, view, record) {
  if (view === 'stadiums' && document.clubs.some(club => club.stadiumId === record.id)) return 'Remova o vínculo com este estádio nas fichas dos clubes antes de excluir o cadastro.';
  if (view === 'countries') {
    if (document.countries.length <= 1) return 'A base precisa de pelo menos um país.';
    if (document.clubs.some(club => club.countryCode === record.code) || document.stadiums.some(stadium => stadium.countryCode === record.code) || document.players.some(player => player.nationalityCode === record.code)) return 'Altere os países dos clubes, estádios e jogadores vinculados antes de excluir este cadastro.';
  }
  return '';
}

export function validateHistory(document) {
  if (!hasHistory(document)) return [];
  const issues = [];
  const issue = (path, message) => issues.push({ path, message });
  const text = (value, path, max, optional = false, allowEmpty = false) => {
    if (value === undefined && optional) return;
    if (typeof value !== 'string' || (!allowEmpty && !value.trim()) || [...value].length > max) issue(path, `Informe ${allowEmpty ? 'um texto' : 'um valor não vazio'} de até ${max} caracteres.`);
  };
  const integer = (value, path, min, max) => {
    if (value === undefined) return;
    if (!Number.isInteger(value) || value < min || value > max) issue(path, `Informe um número inteiro entre ${min.toLocaleString('pt-BR')} e ${max.toLocaleString('pt-BR')}, ou deixe vazio.`);
  };
  const countries = Array.isArray(document.countries) ? document.countries : [];
  const stadiums = Array.isArray(document.stadiums) ? document.stadiums : [];
  if (!countries.length || countries.length > 300) issue('countries', 'Cadastre de 1 a 300 países.');
  if (!Array.isArray(document.stadiums) || stadiums.length > 1024) issue('stadiums', 'A base aceita até 1.024 estádios.');
  const codes = new Set();
  countries.forEach((country, i) => {
    const path = `countries[${i}]`;
    text(country?.name, `${path}.name`, 100);
    if (!/^[A-Z]{2}$/.test(country?.code || '')) issue(`${path}.code`, 'Use duas letras maiúsculas, como BR.');
    else if (codes.has(country.code)) issue(`${path}.code`, 'Este código de país já está cadastrado.');
    codes.add(country?.code);
  });
  const countryRef = (value, path, optional = false) => {
    if (value === undefined && optional) return;
    if (!codes.has(value) || typeof value !== 'string' || !/^[A-Z]{2}$/.test(value)) issue(path, 'Selecione um país cadastrado.');
  };
  const stadiumIds = new Set(stadiums.map(stadium => stadium?.id));
  stadiums.forEach((stadium, i) => {
    const path = `stadiums[${i}]`;
    text(stadium?.name, `${path}.name`, 100); text(stadium?.city, `${path}.city`, 100);
    countryRef(stadium?.countryCode, `${path}.countryCode`);
    integer(stadium?.capacity, `${path}.capacity`, 1, 1000000);
  });
  document.clubs.forEach((club, i) => {
    const path = `clubs[${i}]`;
    countryRef(club.countryCode, `${path}.countryCode`); text(club.city, `${path}.city`, 100);
    for (const [key, max] of [['officialName', 200], ['shortName', 100], ['sponsorship', 200]]) text(club[key], `${path}.${key}`, max, true);
    text(club.notes, `${path}.notes`, 4000, true, true);
    if (club.stadiumId !== undefined && !stadiumIds.has(club.stadiumId)) issue(`${path}.stadiumId`, 'Selecione um estádio cadastrado ou deixe sem vínculo.');
    integer(club.reputation, `${path}.reputation`, 0, 100);
    for (const key of ['supporterCount', 'transferBudget', 'monthlyWageBudget']) integer(club[key], `${path}.${key}`, 0, 2147483647);
    if ((club.currency !== undefined || club.transferBudget !== undefined || club.monthlyWageBudget !== undefined) && !/^[A-Z]{3}$/.test(club.currency || '')) issue(`${path}.currency`, 'Informe a moeda com três letras maiúsculas, como BRL, ao definir um orçamento.');
  });
  const snapshot = document.snapshot;
  if (!snapshot || typeof snapshot !== 'object' || Array.isArray(snapshot)) issue('snapshot', 'Informe a referência histórica da base.');
  else {
    if (!validDate(snapshot.date)) issue('snapshot.date', 'Informe uma data real no formato AAAA-MM-DD.');
    text(snapshot.label, 'snapshot.label', 100); text(snapshot.notes, 'snapshot.notes', 4000, false, true);
    if (!['matchday-squads', 'full-squads'].includes(snapshot.rosterScope)) issue('snapshot.rosterScope', 'Selecione o alcance dos elencos.');
    const sources = Array.isArray(snapshot.sources) ? snapshot.sources : [];
    if (!sources.length || sources.length > 128) issue('snapshot.sources', 'Mantenha de 1 a 128 fontes para a base.');
    const ids = new Set();
    sources.forEach((source, i) => {
      const path = `snapshot.sources[${i}]`;
      if (!source?.id || ids.has(source.id)) issue(`${path}.id`, 'Cada fonte precisa de um identificador único.');
      ids.add(source?.id); text(source?.title, `${path}.title`, 200);
      let validUrl = false;
      const rawUrl = source?.url;
      if (typeof rawUrl === 'string' && /^https?:\/\/[^/?#]/.test(rawUrl) && !/[\s\u0085\uFEFF]/u.test(rawUrl) && [...rawUrl].length <= 2048) {
        try { const url = new URL(rawUrl); validUrl = ['http:', 'https:'].includes(url.protocol); } catch { /* Invalid source remains in the draft for correction. */ }
      }
      if (!validUrl) issue(`${path}.url`, 'Informe o endereço completo da fonte, começando com http:// ou https://.');
    });
  }
  document.players.forEach((player, i) => {
    const path = `players[${i}]`;
    text(player.fullName, `${path}.fullName`, 200, true); text(player.nickname, `${path}.nickname`, 100, true); text(player.notes, `${path}.notes`, 4000, true, true);
    countryRef(player.nationalityCode, `${path}.nationalityCode`, true);
    if (player.preferredFoot !== undefined && !['right', 'left', 'both'].includes(player.preferredFoot)) issue(`${path}.preferredFoot`, 'Selecione o pé preferido ou deixe não informado.');
    if (player.birthDate !== undefined && (!validDate(player.birthDate) || (validDate(snapshot?.date) && player.birthDate > snapshot.date))) issue(`${path}.birthDate`, 'Informe uma data real no formato AAAA-MM-DD, até a data de referência da base, ou deixe vazio.');
  });
  return issues;
}
