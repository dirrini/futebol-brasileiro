import { validateHistory } from './history-model.js';

export const clone = value => structuredClone(value);
export const createId = prefix => `${prefix}-${crypto.randomUUID().replaceAll('-', '')}`;
export const normalize = value => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');
export const compareNames = (left, right) => left.name.localeCompare(right.name, 'pt-BR');

export function clubFor(document, playerId) {
  const member = document.memberships.find(item => item.playerId === playerId);
  return document.clubs.find(item => item.id === member?.clubId) || null;
}

export function profileFor(document, playerId) {
  return document.visualProfiles.find(item => item.playerId === playerId);
}

export function isBuiltin(profile) {
  return !profile || (profile.skin.skinId === 'builtin-player' && profile.skin.revision === 1 && profile.skin.compatibilityProfile === 'football-player-v1');
}

export function setMembership(document, playerId, clubId) {
  document.memberships = document.memberships.filter(item => item.playerId !== playerId);
  if (clubId) document.memberships.push({ clubId, playerId });
}

export function enableAppearance(document, playerId, defaults) {
  let profile = profileFor(document, playerId);
  if (!isBuiltin(profile)) return null;
  if (!profile) {
    profile = { playerId, skin: { skinId: 'builtin-player', revision: 1, compatibilityProfile: 'football-player-v1' } };
    document.visualProfiles.push(profile);
  }
  if (!profile.appearance) profile.appearance = clone(defaults);
  document.schemaVersion = Math.max(document.schemaVersion, 2);
  return profile.appearance;
}

export function addPlayer(document, name, clubId, options) {
  const player = { id: createId('player'), name, naturalPositions: ['CM'], heightCm: 180, weightKg: 75, attributes: Object.fromEntries(options.attributes.map(item => [item.field, 65])) };
  document.players.push(player);
  setMembership(document, player.id, clubId);
  enableAppearance(document, player.id, options.defaultAppearance);
  return player;
}

export function deletePlayer(document, playerId) {
  document.players = document.players.filter(item => item.id !== playerId);
  document.memberships = document.memberships.filter(item => item.playerId !== playerId);
  document.visualProfiles = document.visualProfiles.filter(item => item.playerId !== playerId);
}

export function validateDocument(document, options) {
  const issues = [];
  const issue = (path, message) => issues.push({ path, message });
  const checkName = (name, path) => {
    if (typeof name !== 'string' || !name.trim()) issue(path, 'Informe um nome.');
    else if ([...name].length > 100) issue(path, 'Use no máximo 100 caracteres.');
  };
  if (!document.clubs.length) issue('clubs', 'A base precisa de pelo menos um clube.');
  if (!document.players.length) issue('players', 'A base precisa de pelo menos um jogador.');
  document.clubs.forEach((club, i) => checkName(club.name, `clubs[${i}].name`));
  document.players.forEach((player, i) => {
    const root = `players[${i}]`;
    checkName(player.name, `${root}.name`);
    if (!Array.isArray(player.naturalPositions) || !player.naturalPositions.length) issue(`${root}.naturalPositions`, 'Selecione pelo menos uma posição natural.');
    for (const [field, label] of [['heightCm', 'altura'], ['weightKg', 'peso']]) {
      const { min, max } = options.limits[field];
      if (!Number.isInteger(player[field]) || player[field] < min || player[field] > max) issue(`${root}.${field}`, `Informe ${label} com valor inteiro entre ${min} e ${max}.`);
    }
    for (const { field, label } of options.attributes) {
      const value = player.attributes?.[field];
      if (!Number.isInteger(value) || value < 0 || value > 100) issue(`${root}.attributes.${field}`, `${label}: informe um valor inteiro de 0 a 100.`);
    }
  });
  return [...issues, ...validateHistory(document)];
}
