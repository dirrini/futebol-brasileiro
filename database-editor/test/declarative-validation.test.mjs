import assert from 'node:assert/strict';
import test from 'node:test';
import { fixture, isError, temporaryStore, validate } from './helpers.mjs';
import { validateCompetitions } from '../client/competition-rules.js';
import { safeMediaUri } from '../client/declarative-rules.js';
function declarative() {
 const d=fixture(5); d.schemaVersion=6; d.clubs.forEach(c=>c.stateCode='SP');
 const stage={id:'league',name:'Liga',kind:'league',groupCount:1,opponents:'all',legs:1,roundCount:3,points:{win:3,draw:1,loss:0},tieBreakers:['wins','goal-difference','goals-for','seeded-draw'],qualification:{mode:'overall',count:2},pairing:'seeded',venue:'first-listed',awayGoals:false,tiedWinner:'penalties'};
 const final={...structuredClone(stage),id:'final',name:'Final',kind:'knockout',roundCount:2,legs:2,qualification:{mode:'winners',count:1,rankingStageIds:['league','final']},venue:'seeded',awayGoals:true};
 d.competitionFormats=[{id:'format-test',name:'Liga e final',version:1,participantCount:4,matchRules:{maxSubstitutions:5},stages:[stage,final],outcomes:[{id:'relegated',label:'Rebaixados',stageId:'league',kind:'relegation',ranking:'overall',fromRank:4,toRank:4}]}];
 Object.assign(d.competitions[0],{defaultFormatId:'format-test',level:'state',reputation:70,prizeLevel:40,logoUri:'media/league.png',trophyImageUri:'https://example.com/trophy.png',trophyModelUri:'models/trophy.glb',eligibility:{countryCodes:['BR'],stateCodes:['SP'],allowedClubIds:[],excludedClubIds:[]},prizes:{currency:'BRL',participation:100,win:30,draw:10,rankingAwards:[{stageId:'final',ranking:'overall',fromRank:1,toRank:1,amount:500}]}});
 const e=d.competitionEditions[0]; delete e.rules; delete e.roundDates;
 Object.assign(e,{formatId:'format-test',stageSchedules:[{stageId:'league',roundDates:['2026-10-03','2026-10-10','2026-10-17'],groups:[],authoredFixtures:[]},{stageId:'final',roundDates:['2026-10-24','2026-10-31'],groups:[],authoredFixtures:[],fixtureDates:['2026-10-25','2026-10-31']}]}); return d;
}
test('v6 validates reusable formats, schedules and metadata without mutation',()=>{const d=declarative(),before=structuredClone(d); assert.equal(validate(d),d); assert.deepEqual(d,before);assert.deepEqual(validateCompetitions(d),[]);const other=structuredClone(d.competitionEditions[0]);other.id='other';d.competitionEditions.push(other);assert.doesNotThrow(()=>validate(d));});
test('v6 preserves legacy editions and rejects mixed declarative/legacy fields',()=>{const d=fixture(5);d.schemaVersion=6;d.competitionFormats=[];assert.doesNotThrow(()=>validate(d));const mixed=declarative();mixed.competitionEditions[0].rules=fixture(5).competitionEditions[0].rules;assert.throws(()=>validate(mixed),isError(422,'invalid_database'));});
test('v6 mixed editions keep legacy issue paths at their original document indices', () => {
  const d = declarative(), first = structuredClone(fixture(5).competitionEditions[0]);
  first.id = 'legacy-first'; first.rules.points.win = 0;
  const second = structuredClone(first); second.id = 'legacy-second'; second.rules.points.win = 3; second.roundDates[0] = '2026-02-30';
  const declarativeCopy = structuredClone(d.competitionEditions[0]); declarativeCopy.id = 'declarative-copy';
  d.competitionEditions.push(first, declarativeCopy, second);
  const paths = validateCompetitions(d).map(issue => issue.path);
  assert.ok(paths.includes('competitionEditions[1].rules.points'));
  assert.ok(paths.includes('competitionEditions[3].roundDates[0]'));
  assert.ok(!paths.some(path => /^competitionEditions\[(0|2)\]/.test(path)));
  assert.throws(() => validate(d), isError(422, 'invalid_references', '/competitionEditions/1/rules/points'));
});
test('v6 rejects unsupported versions, rules and executable fields',()=>{for(const mutate of [d=>d.competitionFormats[0].version=2,d=>d.competitionFormats[0].stages[0].kind='script',d=>d.competitionFormats[0].stages[0].formula='eval()',d=>d.competitionFormats[0].stages[0].tieBreakers[0]='unknown']){const d=declarative();mutate(d);assert.throws(()=>validate(d),isError(422,'invalid_database'));}});
test('v6 validates unused formats and every dependent edition',()=>{const d=declarative();d.competitionFormats[0].participantCount=6;assert.throws(()=>validate(d),isError(422,'invalid_references','/competitionEditions/0/participantClubIds'));const u=declarative(),f=structuredClone(u.competitionFormats[0]);f.id='unused';f.stages[0].qualification.count=9;u.competitionFormats.push(f);assert.throws(()=>validate(u),isError(422,'invalid_references'));});
test('v6 eligibility rejects foreign or excluded clubs and ambiguous state filters',()=>{const d=declarative();d.clubs[0].stateCode='RJ';assert.throws(()=>validate(d),isError(422,'invalid_references','/competitionEditions/0/participantClubIds/0'));d.clubs[0].stateCode='SP';d.competitions[0].eligibility.excludedClubIds.push(d.clubs[0].id);assert.throws(()=>validate(d),isError(422,'invalid_references'));d.competitions[0].eligibility.excludedClubIds=[];d.competitions[0].eligibility.countryCodes=[];assert.throws(()=>validate(d),isError(422,'invalid_references','/competitions/0/eligibility/countryCodes'));});
test('v6 rejects future ranking references, transition counts and incompatible tied winners',()=>{for(const mutate of [d=>d.competitionFormats[0].stages[0].qualification.rankingStageIds=['final'],d=>d.competitionFormats[0].stages[1].qualification.count=2,d=>d.competitionFormats[0].stages[1].pairing='draw']){const d=declarative();d.competitionFormats[0].stages[1].tiedWinner='higher-seed';mutate(d);assert.throws(()=>validate(d),isError(422,'invalid_references'));}});
test('v6 resolves routes, rejects overlapping awards and media traversal',()=>{const d=declarative();d.competitions[0].qualificationRoutes=[{outcomeId:'relegated',targetCompetitionId:'missing'}];assert.throws(()=>validate(d),isError(422,'invalid_references'));d.competitions.push({id:'second',name:'Segunda'});d.competitions[0].qualificationRoutes[0].targetCompetitionId='second';assert.doesNotThrow(()=>validate(d));d.competitions[0].prizes.rankingAwards.push(structuredClone(d.competitions[0].prizes.rankingAwards[0]));assert.throws(()=>validate(d),isError(422,'invalid_references'));for(const uri of ['../trophy.glb','/local.png','javascript:alert(1)','http://example.com/a.png','https://user:secret@example.com/a.png','https://'])assert.equal(safeMediaUri(uri),false,uri);});
test('v6 cross-group rules reject a forbidden group confrontation',()=>{const d=declarative(),s=d.competitionFormats[0].stages[0],c=d.competitionEditions[0].stageSchedules[0],ids=d.clubs.map(c=>c.id);s.groupCount=2;s.opponents='cross-group';c.groups=[{id:'a',name:'A',clubIds:ids.slice(0,2)},{id:'b',name:'B',clubIds:ids.slice(2)}];c.authoredFixtures=[[0,2,1],[1,3,1],[0,3,2],[1,2,2]].map(([a,b,r],i)=>({id:`m${i}`,round:r,date:c.roundDates[r-1],homeClubId:ids[a],awayClubId:ids[b]}));assert.doesNotThrow(()=>validate(d));c.authoredFixtures[0].awayClubId=ids[1];assert.throws(()=>validate(d),isError(422,'invalid_references','/competitionEditions/0/stageSchedules/0/authoredFixtures/0'));});
test('v6 validates neutral stadiums and individual knockout date windows',()=>{const d=declarative();d.competitionFormats[0].stages[1].venue='neutral';d.competitionFormats[0].stages[1].awayGoals=false;assert.throws(()=>validate(d),isError(422,'invalid_references','/competitionEditions/0/stageSchedules/1/neutralStadiumId'));d.competitionEditions[0].stageSchedules[1].neutralStadiumId='stadium-demo';assert.doesNotThrow(()=>validate(d));d.competitionEditions[0].stageSchedules[1].fixtureDates[0]='2026-10-31';assert.throws(()=>validate(d),isError(422,'invalid_references','/competitionEditions/0/stageSchedules/1/fixtureDates/0'));});
test('v6 saves keep last valid source and stable format references on failure',async t=>{const d=declarative(),{store}=await temporaryStore(t,d),current=await store.read();current.document.competitionFormats[0].stages[0].points.win=4;const saved=await store.save(current.document,current.etag);assert.equal(saved.document.databaseRevision,d.databaseRevision+1);assert.equal(saved.document.competitionEditions[0].formatId,'format-test');saved.document.competitionFormats[0].stages[0].points.win=0;await assert.rejects(store.save(saved.document,saved.etag),isError(422,'invalid_references'));assert.equal((await store.read()).document.competitionFormats[0].stages[0].points.win,4);});
test('v6 branches winners and losers in parallel without giving the consolation branch a trophy',()=>{
 const d=declarative(),f=d.competitionFormats[0],e=d.competitionEditions[0],semi=f.stages[0];
 Object.assign(semi,{kind:'knockout',roundCount:1,qualification:{mode:'winners',count:2},venue:'seeded'});
 const bronze={...structuredClone(f.stages[1]),id:'bronze',name:'Vaga adicional',legs:1,roundCount:1,awayGoals:false,source:{stageId:'league',selection:'losers'},qualification:{mode:'winners',count:1}};
 f.stages.push(bronze);f.championStageId='final';e.stageSchedules[0].roundDates=['2026-10-03'];e.stageSchedules.push({stageId:'bronze',roundDates:['2026-10-24'],groups:[],authoredFixtures:[]});
 assert.doesNotThrow(()=>validate(d));
 bronze.qualification.rankingStageIds=['final'];assert.throws(()=>validate(d),isError(422,'invalid_references','/competitionFormats/0/stages/2/qualification/rankingStageIds/0'));
 delete bronze.qualification.rankingStageIds;bronze.source.stageId='bronze';assert.throws(()=>validate(d),isError(422,'invalid_references'));
});
test('v6 allows a qualification-only tournament with explicit null champion and multiple last-round winners',()=>{
 const d=declarative(),f=d.competitionFormats[0],e=d.competitionEditions[0];f.stages=f.stages.slice(0,1);Object.assign(f.stages[0],{kind:'knockout',roundCount:1,qualification:{mode:'winners',count:2},venue:'seeded'});f.championStageId=null;e.stageSchedules=e.stageSchedules.slice(0,1);e.stageSchedules[0].roundDates=['2026-10-03'];d.competitions[0].prizes.rankingAwards=[];assert.doesNotThrow(()=>validate(d));delete f.championStageId;assert.throws(()=>validate(d),isError(422,'invalid_references'));
});

test('v6 rejects concurrent branches that may share clubs and accepts disjoint loser branches', () => {
  const d = declarative(), f = d.competitionFormats[0], e = d.competitionEditions[0];
  Object.assign(f.stages[0], { kind: 'knockout', roundCount: 1, qualification: { mode: 'winners', count: 2 }, venue: 'seeded' });
  const playoff = { ...structuredClone(f.stages[1]), id: 'playoff', name: 'Vaga', legs: 1, roundCount: 1, awayGoals: false,
    source: { stageId: 'league', selection: 'winners' }, qualification: { mode: 'winners', count: 1 } };
  f.stages.push(playoff); f.championStageId = 'final';
  e.stageSchedules[0].roundDates = ['2026-10-03'];
  e.stageSchedules.push({ stageId: 'playoff', roundDates: ['2026-10-25'], groups: [], authoredFixtures: [] });
  assert.throws(() => validate(d), isError(422, 'invalid_references', '/competitionEditions/0/stageSchedules/2'));
  playoff.source.selection = 'losers';
  assert.doesNotThrow(() => validate(d));
  playoff.source.selection = 'winners';
  e.stageSchedules[2].roundDates = ['2026-10-26'];
  assert.doesNotThrow(() => validate(d));
});
