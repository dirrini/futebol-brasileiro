import { $, field, showDialog, announce } from './ui.js';
import { createId } from './model.js';
import { formatTieBreakers, stageParticipantCounts } from './declarative-rules.js';
import { enableFormats, duplicateFormat, addStage, stageRemovalBlock, moveStage, changeStageKind, recommendedRounds, assignFormat, syncSchedules, scheduleFor, distributeGroups, assignGroup, generateStageDates, addStageFixture, outcomeRemovalBlock } from './format-model.js';

export function wireDeclarative(record, context) {
  const { state, refresh, openRecord, warn } = context, document = state.document;
  const format = state.view === 'competitionFormats' ? record : document.competitionFormats?.find(f => f.id === record.formatId);
  const stage = format?.stages.find(s => s.id === state.stageId) || format?.stages[0];
  const schedule = stage && scheduleFor(record, stage.id);
  const run = (command, focus) => { try { command(); state.issues = []; refresh(focus); } catch (error) { warn(error.message); } };
  const confirmDraftChange = (title, description, action, command, focus) => showDialog({ title, description, action, danger: true, onAccept: () => { try { command(); state.issues = []; refresh(focus); } catch (error) { return error.message; } } });
  for (const id of ['format-stage-filter', 'edition-stage-filter']) $(`#${id}`)?.addEventListener('change', event => { state.stageId = event.target.value; state.fixtureRound = 1; refresh('#' + id, false); });
  $('#declarative-round-filter')?.addEventListener('change', event => { state.fixtureRound = Number(event.target.value); refresh('#declarative-round-filter', false); });
  $('#edition-format-ref')?.addEventListener('change', event => {
    const requested = event.target.value; event.target.value = record.formatId || '';
    if (!requested || requested === record.formatId) return;
    confirmDraftChange('Trocar o formato desta edição?', `Os calendários de “${record.name}” serão substituídos por fases vazias do formato escolhido. Participantes e ID da edição serão preservados. Temporadas salvas no jogo não mudam.`, 'Trocar formato', () => { assignFormat(document, record, requested); state.stageId = ''; state.fixtureRound = 1; }, '#edition-format-ref');
  });
  documentQuery('[data-change-stage-kind]').forEach(input => input.addEventListener('change', event => {
    const target = format.stages.find(s => s.id === input.dataset.changeStageKind), kind = event.target.value; event.target.value = target.kind;
    confirmDraftChange('Alterar o modelo da fase?', `A fase “${target.name}” usará ${kind === 'league' ? 'liga' : 'mata-mata'}. Regras incompatíveis serão ajustadas; confira as origens, vagas e todos os calendários vinculados antes de salvar.`, 'Alterar modelo', () => changeStageKind(format, target, kind), '#' + input.id);
  }));
  documentQuery('[data-format-ranking]').forEach(input => input.addEventListener('change', () => run(() => {
    const target = format.stages.find(s => s.id === input.dataset.formatRanking);
    const selected = new Set(target.qualification.rankingStageIds || []);
    if (input.checked) selected.add(input.value); else selected.delete(input.value);
    if (selected.size) target.qualification.rankingStageIds = [...selected]; else delete target.qualification.rankingStageIds;
  }, '#format-stage-filter')));
  documentQuery('[data-group-value]').forEach(input => input.addEventListener('change', () => run(() => assignGroup(schedule, input.dataset.groupValue, input.value, format.stages.indexOf(stage) > 0), '#' + input.id)));

  documentQuery('[data-format-action]').forEach(button => button.addEventListener('click', () => {
    if (state.saving) return;
    const type = button.dataset.formatAction, target = format.stages.find(s => s.id === (button.dataset.stage || button.dataset.id)) || stage;
    if (type === 'duplicate') {
      showDialog({ title: 'Duplicar formato', description: 'Cria uma cópia independente, com IDs novos para formato, fases e resultados. Os vínculos existentes continuam no original.', action: 'Duplicar formato', content: field({ id: 'copy-format-name', label: 'Nome da cópia', value: `${format.name} — cópia`.slice(0, 100), maxLength: 100 }), onAccept: dialog => {
        const name = dialog.querySelector('#copy-format-name').value;
        if (!name.trim() || [...name].length > 100) return { field: 'copy-format-name', message: 'Informe um nome de até 100 caracteres.' };
        if (document.competitionFormats.length >= 128) return 'O limite é de 128 formatos.';
        const copy = duplicateFormat(document, format, name); state.stageId = ''; openRecord('competitionFormats', copy.id); refresh('#format-name');
      } }); return;
    }
    if (type === 'remove-stage') {
      const block = stageRemovalBlock(document, format, target); if (block) { warn(block); return; }
      confirmDraftChange('Remover fase?', `“${target.name}” será removida do rascunho.`, 'Remover fase', () => { format.stages = format.stages.filter(s => s.id !== target.id); state.stageId = ''; }, '#format-stage-filter'); return;
    }
    if (type === 'remove-outcome') {
      const block = outcomeRemovalBlock(document, button.dataset.id); if (block) { warn(block); return; }
      confirmDraftChange('Remover resultado esportivo?', 'Esta regra de classificação, acesso ou rebaixamento será removida do rascunho.', 'Remover resultado', () => { format.outcomes = format.outcomes.filter(o => o.id !== button.dataset.id); }, '[data-format-action="add-outcome"]'); return;
    }
    run(() => {
      if (type === 'add-stage') { const added = addStage(format); state.stageId = added.id; }
      else if (type === 'move-stage') moveStage(format, target.id, Number(button.dataset.direction));
      else if (type === 'recommended-rounds') target.roundCount = recommendedRounds(format, target);
      else if (type === 'add-outcome') format.outcomes.push({ id: createId('outcome'), label: 'Nova classificação', stageId: target.id, kind: 'qualification', ranking: 'overall', fromRank: 1, toRank: 1 });
      else if (type === 'add-tie') { const next = formatTieBreakers.find(v => !target.tieBreakers.includes(v)); if (next) target.tieBreakers.splice(target.tieBreakers.length - 1, 0, next); }
      else if (type === 'remove-tie') target.tieBreakers.splice(Number(button.dataset.id), 1);
      else if (type === 'move-tie') { const from = Number(button.dataset.id), to = from + Number(button.dataset.direction); if (to >= 0 && to < target.tieBreakers.length - 1) [target.tieBreakers[from], target.tieBreakers[to]] = [target.tieBreakers[to], target.tieBreakers[from]]; }
    }, '#format-stage-filter');
  }));

  documentQuery('[data-declarative-action]').forEach(button => button.addEventListener('click', () => {
    if (state.saving) return;
    const type = button.dataset.declarativeAction;
    if (type === 'open-format') { openRecord('competitionFormats', button.dataset.id); return; }
    if (type === 'enable-v6') { confirmDraftChange('Ativar cadastros v6?', 'O rascunho passará a aceitar formatos reutilizáveis e metadados de competições. As edições antigas conservarão seus regulamentos. A mudança só será publicada ao salvar.', 'Ativar cadastros', () => enableFormats(document)); return; }
    if (type === 'generate-stage-dates') {
      showDialog({ title: 'Gerar datas da fase', description: `Substitui as ${stage.roundCount} datas de rodada de “${stage.name}”. Datas por confronto precisam ser conferidas depois.`, action: 'Gerar datas', content: field({ id: 'stage-start-date', label: 'Primeira rodada', value: schedule.roundDates[0] || '', maxLength: 10, hint: 'AAAA-MM-DD' }) + field({ id: 'stage-date-interval', label: 'Intervalo em dias', value: 7, type: 'number', min: 1, max: 365 }), onAccept: dialog => {
        try { generateStageDates(stage, schedule, dialog.querySelector('#stage-start-date').value, Number(dialog.querySelector('#stage-date-interval').value)); }
        catch (error) { return { field: 'stage-start-date', message: error.message }; }
        state.issues = []; refresh('#stage-dates');
      } }); return;
    }
    if (type === 'sync-schedules') { confirmDraftChange('Sincronizar fases da edição?', 'Os calendários das fases com IDs existentes serão mantidos. Fases novas recebem calendário vazio; calendários de fases removidas serão excluídos.', 'Sincronizar fases', () => syncSchedules(record, format), '#edition-stage-filter'); return; }
    if (['groups-sequential', 'groups-serpentine'].includes(type)) { confirmDraftChange('Redistribuir os grupos?', 'A composição dos grupos desta fase será substituída. Os jogos autorados continuam preservados e precisarão respeitar a nova divisão.', 'Redistribuir grupos', () => distributeGroups(record, format, stage, type === 'groups-serpentine'), '#stage-groups'); return; }
    const removal = { 'remove-eligibility': 'filtros de elegibilidade', 'remove-prizes': 'premiação', 'remove-route': 'destino de vaga', 'remove-award': 'prêmio por classificação', 'remove-stage-fixture': 'confronto', 'remove-fixture-dates': 'datas por confronto' }[type];
    const mutate = () => {
      if (type === 'enable-eligibility') record.eligibility = { countryCodes: [], stateCodes: [], allowedClubIds: [], excludedClubIds: [] };
      else if (type === 'remove-eligibility') delete record.eligibility;
      else if (type === 'enable-prizes') record.prizes = { currency: 'BRL', participation: 0, win: 0, draw: 0, rankingAwards: [] };
      else if (type === 'remove-prizes') delete record.prizes;
      else if (type === 'add-route') {
        const f = document.competitionFormats.find(f => f.id === record.defaultFormatId);
        const outcome = f?.outcomes.find(o => !record.qualificationRoutes?.some(r => r.outcomeId === o.id));
        if (!outcome) throw new Error('Selecione um formato padrão com resultados esportivos ainda sem destino.');
        (record.qualificationRoutes ??= []).push({ outcomeId: outcome.id, targetCompetitionId: document.competitions.find(c => c.id !== record.id)?.id || '' });
      } else if (type === 'remove-route') record.qualificationRoutes.splice(Number(button.dataset.index), 1);
      else if (type === 'add-award') {
        const f = document.competitionFormats.find(f => f.id === record.defaultFormatId);
        if (!f) throw new Error('Defina o formato padrão antes de criar prêmios por fase.');
        record.prizes.rankingAwards.push({ stageId: f.stages[0].id, ranking: 'overall', fromRank: 1, toRank: 1, amount: 0 });
      } else if (type === 'remove-award') record.prizes.rankingAwards.splice(Number(button.dataset.index), 1);
      else if (type === 'enable-fixture-dates') {
        const count = stageParticipantCounts(format)[format.stages.indexOf(stage)];
        if (!Number.isInteger(count) || count < 2 || count > 64 || count % 2 || ![1, 2].includes(stage.legs)) throw new Error('Corrija a fase: mata-mata exige de 2 a 64 clubes em quantidade par e um ou dois jogos por confronto.');
        schedule.fixtureDates = Array.from({ length: count / 2 * stage.legs }, (_, i) => schedule.roundDates[Math.floor(i / (count / 2))] || '');
      }
      else if (type === 'remove-fixture-dates') delete schedule.fixtureDates;
      else if (type === 'add-stage-fixture') { if (schedule.authoredFixtures.length >= 4096) throw new Error('Limite de confrontos atingido.'); addStageFixture(schedule, Math.min(stage.roundCount, state.fixtureRound)); }
      else if (type === 'remove-stage-fixture') schedule.authoredFixtures = schedule.authoredFixtures.filter(f => f.id !== button.dataset.id);
    };
    if (removal) confirmDraftChange('Remover ' + removal + '?', 'A informação será removida do rascunho. A publicação ocorre somente ao salvar a base.', 'Remover', mutate, '#detail-title');
    else run(mutate, type === 'add-stage-fixture' ? '#declarative-round-filter' : '#detail-title');
  }));
}
function documentQuery(selector) { return globalThis.document.querySelectorAll('#detail-panel ' + selector); }
