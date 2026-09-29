import { createId, addPlayer } from './model.js';
import { hasHistory, identity } from './history-model.js';

export const views = {
  players: { label: 'Jogadores', singular: 'jogador', plural: 'jogadores', heading: 'Todos os jogadores', eyebrow: 'ELENCO', prefix: 'player' },
  clubs: { label: 'Clubes', singular: 'clube', plural: 'clubes', heading: 'Todos os clubes', eyebrow: 'CLUBES DA BASE', prefix: 'club' },
  stadiums: { label: 'Estádios', singular: 'estádio', plural: 'estádios', heading: 'Todos os estádios', eyebrow: 'PALCOS DO FUTEBOL', prefix: 'stadium' },
  countries: { label: 'Países', singular: 'país', plural: 'países', heading: 'Todos os países', eyebrow: 'REFERÊNCIAS GEOGRÁFICAS', prefix: 'country' },
  snapshot: { label: 'Referência da base' },
};

export function availableViews(document) {
  return hasHistory(document) ? Object.keys(views) : ['players', 'clubs'];
}

export function validFilters(document, filters) {
  return {
    club: filters.club === 'free' || document.clubs.some(club => club.id === filters.club) ? filters.club : '',
    country: document.countries?.some(country => country.code === filters.country) ? filters.country : '',
  };
}

export function creationError(document, view, values) {
  if (typeof values.name !== 'string' || !values.name.trim() || [...values.name].length > 100) return { field: 'new-name', message: 'Informe um nome de até 100 caracteres.' };
  if (view === 'countries') {
    if (!/^[A-Z]{2}$/.test(values.code || '')) return { field: 'new-code', message: 'Informe o código com duas letras maiúsculas, como BR.' };
    if (document.countries.some(country => country.code === values.code)) return { field: 'new-code', message: 'Este código de país já está cadastrado.' };
    if (document.countries.length >= 300) return { field: 'new-code', message: 'A base aceita até 300 países.' };
  }
  if ((view === 'clubs' && hasHistory(document)) || view === 'stadiums') {
    if (!document.countries.some(country => country.code === values.countryCode)) return { field: 'new-country', message: 'Selecione um país cadastrado.' };
    if (typeof values.city !== 'string' || !values.city.trim() || [...values.city].length > 100) return { field: 'new-city', message: 'Informe uma cidade de até 100 caracteres.' };
  }
  if (view === 'stadiums' && document.stadiums.length >= 1024) return { field: 'new-name', message: 'A base aceita até 1.024 estádios.' };
  return null;
}

export function addRecord(document, view, values, options) {
  const error = creationError(document, view, values);
  if (error) throw new Error(error.message);
  if (view === 'players') return addPlayer(document, values.name, values.clubId || '', options);
  const record = view === 'countries' ? { code: values.code, name: values.name } : { id: createId(views[view].prefix), name: values.name };
  if (view === 'stadiums' || (view === 'clubs' && hasHistory(document))) Object.assign(record, { countryCode: values.countryCode, city: values.city });
  document[view].push(record);
  return record;
}

export function removeHistoricalRecord(document, view, record) {
  document[view] = document[view].filter(item => identity(item) !== identity(record));
}

export function addSource(document) {
  if (document.snapshot.sources.length >= 128) return null;
  const source = { id: createId('source'), title: '', url: '' };
  document.snapshot.sources.push(source);
  return source;
}

export function removeSource(document, id) {
  if (document.snapshot.sources.length <= 1) return false;
  document.snapshot.sources = document.snapshot.sources.filter(source => source.id !== id);
  return true;
}
