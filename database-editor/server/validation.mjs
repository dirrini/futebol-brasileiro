import Ajv from 'ajv';
import { EditorError } from './errors.mjs';

export function createValidator(schemas) {
  const ajv = new Ajv({ allErrors: true, strict: false, coerceTypes: false, useDefaults: false, removeAdditional: false });
  const validators = new Map(schemas.map(schema => [schema.properties.schemaVersion.const, ajv.compile(schema)]));
  return document => {
    const validate = validators.get(document?.schemaVersion);
    if (!validate) throw new EditorError(422, 'unsupported_schema_version', 'A versão da base deve ser 1 ou 2.');
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
    if (issues.length) throw new EditorError(422, 'invalid_references', 'A base contém vínculos ou identidades inválidos.', issues.slice(0, 40));
    return document;
  };
}
