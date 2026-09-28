import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createValidator } from '../server/validation.mjs';
import { DatabaseStore } from '../server/database-store.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const legacyPath = join(root, 'Assets/FootballSimulator/Code/FootballWorld/Tests/Fixtures/legacy-four-clubs.database.json');
export const schemas = await Promise.all([1, 2].map(async version => JSON.parse(await readFile(
  join(root, `Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v${version}.schema.json`), 'utf8'))));
const legacy = JSON.parse(await readFile(legacyPath, 'utf8'));
export const validate = createValidator(schemas);
export const appearance = () => ({ skinTone: 'tone-3', hairStyle: 'short', hairColor: 'black',
  beardStyle: 'goatee', beardColor: 'brown', bootsColor: 'cyan', sockAccessoryColor: 'white' });
export function fixture(version = 2) {
  const document = structuredClone(legacy);
  if (version === 2) {
    document.schemaVersion = 2;
    for (const profile of document.visualProfiles) profile.appearance = appearance();
  }
  return document;
}
export function isError(status, code, issuePath) {
  return error => {
    assert.equal(error.status, status);
    assert.equal(error.code, code);
    if (issuePath) assert.ok(error.issues.some(issue => issue.path === issuePath),
      `Expected issue ${issuePath}; received ${JSON.stringify(error.issues)}`);
    return true;
  };
}
export async function temporaryDirectory(t) {
  const directory = await mkdtemp(join(tmpdir(), 'football-editor-test-'));
  t.after(async () => {
    const fullPath = resolve(directory);
    // Delete only this test's unique directory directly below the OS temporary directory.
    assert.equal(dirname(fullPath), resolve(tmpdir()));
    assert.ok(basename(fullPath).startsWith('football-editor-test-'));
    await rm(fullPath, { recursive: true, force: true });
  });
  return directory;
}
export async function temporaryStore(t, document = fixture()) {
  const directory = await temporaryDirectory(t);
  const path = join(directory, 'database.json');
  const original = JSON.stringify(document, null, 2) + '\n';
  await writeFile(path, original);
  return { store: new DatabaseStore(path, validate), path, directory, original };
}
