import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createValidator } from '../server/validation.mjs';
import { DatabaseStore } from '../server/database-store.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const legacyPath = join(root, 'Assets/FootballSimulator/Code/FootballWorld/Tests/Fixtures/legacy-four-clubs.database.json');
export const schemas = await Promise.all([1, 2, 3, 4, 5].map(async version => JSON.parse(await readFile(
  join(root, `Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v${version}.schema.json`), 'utf8'))));
const legacy = JSON.parse(await readFile(legacyPath, 'utf8'));
export const validate = createValidator(schemas);
export const appearance = () => ({ skinTone: 'tone-3', hairStyle: 'short', hairColor: 'black',
  beardStyle: 'goatee', beardColor: 'brown', bootsColor: 'cyan', sockAccessoryColor: 'white' });
export function fixture(version = 2) {
  const document = structuredClone(legacy);
  if (version >= 2) {
    document.schemaVersion = version;
    for (const profile of document.visualProfiles) profile.appearance = appearance();
  }
  if (version >= 3) {
    document.competitions = [{ id: 'competition-demo', name: 'Liga de demonstração' }];
    document.competitionEditions = [{ id: 'edition-demo-2026', competitionId: 'competition-demo', name: 'Edição de teste 2026',
      participantClubIds: document.clubs.map(club => club.id), roundDates: ['2026-10-03', '2026-10-10', '2026-10-17'],
      rules: { type: 'round-robin', version: 1, legs: 1, points: { win: 3, draw: 1, loss: 0 }, tieBreakers: ['wins', 'goal-difference', 'goals-for'] } }];
  }
  if (version >= 4) {
    document.countries = [{ code: 'BR', name: 'Brasil' }];
    document.stadiums = [{ id: 'stadium-demo', name: 'Estádio de teste', countryCode: 'BR', city: 'São Paulo', capacity: 12000 }];
    document.snapshot = { date: '2026-01-11', label: 'Recorte de teste', rosterScope: 'matchday-squads', notes: '',
      sources: [{ id: 'source-demo', title: 'Documento de teste', url: 'https://example.com/match-report.pdf' }] };
    for (const club of document.clubs) Object.assign(club, { countryCode: 'BR', city: 'São Paulo' });
  }
  return document;
}
export function paulistaFixture() {
  const document = fixture(5);
  document.clubs = Array.from({ length: 16 }, (_, index) => ({ id: `club-${index}`, name: `Clube ${index}`, countryCode: 'BR', city: 'São Paulo' }));
  document.memberships = [];
  const edition = document.competitionEditions[0];
  edition.participantClubIds = document.clubs.map(club => club.id);
  edition.roundDates = ['2026-01-10', '2026-01-14', '2026-01-17', '2026-01-20', '2026-01-24', '2026-01-28', '2026-02-07', '2026-02-15'];
  edition.playoffDates = ['2026-02-22', '2026-02-21', '2026-02-21', '2026-02-22', '2026-02-28', '2026-03-01', '2026-03-04', '2026-03-08'];
  edition.rules.type = 'paulista-2026';
  edition.rules.tieBreakers.push('red-cards', 'yellow-cards', 'drawing-lots');
  edition.authoredFixtures = [];
  for (let round = 1; round <= 8; round++) for (let i = 0; i < 8; i++) {
    const first = `club-${i}`, second = `club-${8 + (i + round - 1) % 8}`;
    edition.authoredFixtures.push({ id: `fixture-${round}-${i}`, round, date: edition.roundDates[round - 1], homeClubId: round % 2 ? first : second, awayClubId: round % 2 ? second : first });
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
