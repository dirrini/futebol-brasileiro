import assert from 'node:assert/strict';
import test from 'node:test';
import { paulistaFixture, fixture, validate, isError, temporaryStore } from './helpers.mjs';

test('v5 accepts a balanced authored eight-round calendar and same-phase dates in bracket order', () => {
  const document = paulistaFixture(); const before = structuredClone(document);
  document.competitionEditions[0].authoredFixtures[0].stadiumId = 'stadium-demo';
  assert.doesNotThrow(() => validate(document));
  delete document.competitionEditions[0].authoredFixtures[0].stadiumId;
  assert.deepEqual(document, before);
  assert.doesNotThrow(() => validate(fixture(5)));
});

test('v5 rejects silently ignored playoff fields on leagues and unsupported historical rule versions', () => {
  const league = fixture(5); league.competitionEditions[0].playoffDates = paulistaFixture().competitionEditions[0].playoffDates;
  assert.throws(() => validate(league), isError(422, 'invalid_database'));
  for (const edit of [document => document.schemaVersion = 4, document => document.competitionEditions[0].rules.version = 2,
    document => document.competitionEditions[0].rules.points.win = 2,
    document => document.competitionEditions[0].rules.tieBreakers.reverse()]) {
    const document = paulistaFixture(); edit(document); assert.throws(() => validate(document), isError(422, 'invalid_database'));
  }
});

test('v5 rejects missing fixtures, impossible dates, wrong phase order and cross-edition fixture participants', () => {
  for (const [edit, path] of [
    [edition => edition.authoredFixtures[0].date = '2026-02-30', '/competitionEditions/0/authoredFixtures/0/date'],
    [edition => edition.authoredFixtures[0].date = edition.roundDates[1], '/competitionEditions/0/authoredFixtures/0/date'],
    [edition => edition.playoffDates[4] = '2026-02-21', '/competitionEditions/0/playoffDates/4'],
    [edition => edition.playoffDates[7] = edition.playoffDates[6], '/competitionEditions/0/playoffDates/7'],
    [edition => edition.authoredFixtures[0].homeClubId = 'club-missing', '/competitionEditions/0/authoredFixtures/0/homeClubId'],
    [edition => edition.authoredFixtures[0].stadiumId = 'stadium-missing', '/competitionEditions/0/authoredFixtures/0/stadiumId'],
    [edition => edition.authoredFixtures[1].id = edition.authoredFixtures[0].id, '/competitionEditions/0/authoredFixtures/1/id'],
  ]) {
    const document = paulistaFixture(); edit(document.competitionEditions[0]); assert.throws(() => validate(document), isError(422, 'invalid_references', path));
  }
  const missing = paulistaFixture(); missing.competitionEditions[0].authoredFixtures.pop();
  assert.throws(() => validate(missing), isError(422, 'invalid_database'));
});

test('v5 rejects repeat opponents, clubs playing twice in a round and unbalanced home games', () => {
  const document = paulistaFixture(); const edition = document.competitionEditions[0];
  const first = edition.authoredFixtures[0];
  [first.homeClubId, first.awayClubId] = [first.awayClubId, first.homeClubId];
  assert.throws(() => validate(document), isError(422, 'invalid_references', '/competitionEditions/0/authoredFixtures'));
  const repeated = paulistaFixture(); repeated.competitionEditions[0].authoredFixtures[8].awayClubId = 'club-8';
  assert.throws(() => validate(repeated), isError(422, 'invalid_references'));
});

test('editing v5 competitions preserves identities and backs up the last valid authored calendar', async t => {
  const document = paulistaFixture(); const { store } = await temporaryStore(t, document);
  const current = await store.read(); const id = current.document.competitionEditions[0].authoredFixtures[0].id;
  current.document.competitions[0].name = 'Paulista editado';
  current.document.competitionEditions[0].authoredFixtures[0].date = '2026-01-11';
  const saved = await store.save(current.document, current.etag);
  assert.equal(saved.document.databaseRevision, document.databaseRevision + 1);
  assert.equal(saved.document.competitionEditions[0].authoredFixtures[0].id, id);
  saved.document.competitionEditions[0].playoffDates[0] = '2026-01-01';
  await assert.rejects(store.save(saved.document, saved.etag), isError(422, 'invalid_references'));
  assert.equal((await store.read()).document.competitionEditions[0].playoffDates[0], '2026-02-22');
});
