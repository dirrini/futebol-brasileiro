import assert from 'node:assert/strict';
import test from 'node:test';
import { fixture, isError, temporaryStore, validate } from './helpers.mjs';

test('v3 rejects duplicate competition identities and dangling edition references', () => {
  for (const collection of ['competitions', 'competitionEditions']) {
    const document = fixture(3);
    document[collection].push(structuredClone(document[collection][0]));
    assert.throws(() => validate(document), isError(422, 'invalid_references', `/${collection}/1/id`));
  }
  const document = fixture(3);
  document.competitionEditions[0].competitionId = 'competition-missing';
  document.competitionEditions[0].participantClubIds[1] = 'club-missing';
  assert.throws(() => validate(document), isError(422, 'invalid_references', '/competitionEditions/0/competitionId'));
  document.competitionEditions[0].competitionId = document.competitions[0].id;
  assert.throws(() => validate(document), isError(422, 'invalid_references', '/competitionEditions/0/participantClubIds/1'));
});

test('v3 validates calendar dates, leap years, chronological order and round count', () => {
  for (const date of ['2026-02-29', '2026-04-31', '0000-01-01', '1900-02-29']) {
    const document = fixture(3);
    document.competitionEditions[0].roundDates[0] = date;
    assert.throws(() => validate(document), isError(422, date.startsWith('0000') ? 'invalid_database' : 'invalid_references', '/competitionEditions/0/roundDates/0'));
  }
  const valid = fixture(3);
  valid.competitionEditions[0].roundDates = ['2000-02-29', '2000-03-07', '2000-03-14'];
  assert.doesNotThrow(() => validate(valid));
  for (const dates of [['2026-10-03', '2026-10-03', '2026-10-17'], ['2026-10-10', '2026-10-03', '2026-10-17']]) {
    const document = fixture(3);
    document.competitionEditions[0].roundDates = dates;
    assert.throws(() => validate(document), dates[0] === dates[1]
      ? isError(422, 'invalid_database', '/competitionEditions/0/roundDates')
      : isError(422, 'invalid_references', '/competitionEditions/0/roundDates/1'));
  }
  valid.competitionEditions[0].roundDates.pop();
  assert.throws(() => validate(valid), isError(422, 'invalid_references', '/competitionEditions/0/roundDates'));
});

test('v3 supports two legs and odd club counts without accepting unsupported rules', () => {
  const document = fixture(3);
  const edition = document.competitionEditions[0];
  edition.rules.legs = 2;
  edition.roundDates.push('2026-10-24', '2026-10-31', '2026-11-07');
  assert.doesNotThrow(() => validate(document));
  edition.participantClubIds.pop();
  assert.doesNotThrow(() => validate(document));
  edition.rules.type = 'knockout';
  assert.throws(() => validate(document), isError(422, 'invalid_database'));
});

test('v3 rejects invalid point ordering and repeated participants', () => {
  const document = fixture(3);
  document.competitionEditions[0].rules.points = { win: 1, draw: 1, loss: 0 };
  assert.throws(() => validate(document), isError(422, 'invalid_references', '/competitionEditions/0/rules/points'));
  document.competitionEditions[0].rules.points = { win: 3, draw: 0, loss: 1 };
  assert.throws(() => validate(document), isError(422, 'invalid_references'));
  document.competitionEditions[0].rules.points = { win: 3, draw: 1, loss: 0 };
  document.competitionEditions[0].participantClubIds[1] = document.competitionEditions[0].participantClubIds[0];
  assert.throws(() => validate(document), isError(422, 'invalid_database'));
});

test('editor saves preserve all competition definitions while changing a player', async t => {
  const document = fixture(3);
  const { store } = await temporaryStore(t, document);
  const current = await store.read();
  current.document.players[0].name = 'Nome editado';
  const saved = await store.save(current.document, current.etag);
  assert.equal(saved.document.schemaVersion, 3);
  assert.deepEqual(saved.document.competitions, document.competitions);
  assert.deepEqual(saved.document.competitionEditions, document.competitionEditions);
  assert.equal(saved.document.players[0].name, 'Nome editado');
});
