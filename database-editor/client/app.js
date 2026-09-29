import { getDatabase, getOptions, saveDatabase } from './api.js';
import { clone, createId, normalize, compareNames, clubFor, profileFor, setMembership, enableAppearance, addPlayer, deletePlayer, validateDocument } from './model.js';
import { $, escapeHtml as e, announce, field, select, applyFieldErrors, showDialog, downloadJson } from './ui.js';
import { playerForm, preview } from './players.js';
import { clubForm } from './clubs.js';

const PAGE_SIZE = 10;
const initial = new URLSearchParams(location.search);
const state = {
  document: null, original: null, etag: '', options: null, loading: true, saving: false, dirty: false,
  view: initial.get('view') === 'clubs' ? 'clubs' : 'players', query: initial.get('q') || '',
  club: initial.get('club') || '', page: Math.max(1, Number(initial.get('page')) || 1),
  selectedId: initial.get('id') || '', tab: ['data', 'attributes', 'appearance'].includes(initial.get('tab')) ? initial.get('tab') : 'data',
  issues: [], feedback: null,
};

function writeUrl() {
  const params = new URLSearchParams();
  params.set('view', state.view);
  if (state.query) params.set('q', state.query);
  if (state.club && state.view === 'players') params.set('club', state.club);
  if (state.page > 1) params.set('page', state.page);
  if (state.selectedId) params.set('id', state.selectedId);
  if (state.tab !== 'data' && state.view === 'players') params.set('tab', state.tab);
  history.replaceState(null, '', `${location.pathname}?${params}`);
}

function selectedRecord() {
  return state.document?.[state.view].find(record => record.id === state.selectedId);
}

function records() {
  if (!state.document) return [];
  return state.document[state.view].filter(record => {
    const matchesName = normalize(record.name).includes(normalize(state.query));
    const matchesClub = state.view !== 'players' || !state.club || (state.club === 'free' ? !clubFor(state.document, record.id) : clubFor(state.document, record.id)?.id === state.club);
    return matchesName && matchesClub;
  }).sort(compareNames);
}

function ensureSelection() {
  if (!selectedRecord()) state.selectedId = records()[0]?.id || '';
}

function renderNavigation() {
  $('#navigation').innerHTML = [['players', 'Jogadores'], ['clubs', 'Clubes']].map(([view, label]) => `<button class="nav-item" data-view="${view}"${state.view === view ? ' aria-current="page"' : ''}${!state.document || state.saving || state.loading ? ' disabled' : ''}><span>${label}</span><span class="nav-count">${state.document?.[view].length ?? '—'}</span></button>`).join('');
  $('#page-title').textContent = state.view === 'players' ? 'Jogadores' : 'Clubes';
  document.title = `${state.view === 'players' ? 'Jogadores' : 'Clubes'} · Editor de base · Futebol Brasileiro`;
  $('#navigation').querySelectorAll('button').forEach(button => button.addEventListener('click', () => {
    if (state.view === button.dataset.view) return;
    state.view = button.dataset.view; state.query = ''; state.club = ''; state.page = 1; state.selectedId = '';
    renderWorkspace(); renderNavigation(); writeUrl();
  }));
}

function renderStatus() {
  const save = $('#save');
  save.disabled = !state.document || !state.dirty || state.saving || state.loading;
  save.textContent = state.saving ? 'Salvando…' : 'Salvar alterações';
  save.setAttribute('aria-busy', String(state.saving));
  $('#export').disabled = !state.document || state.saving;
  $('#save-status').innerHTML = state.document ? `<div><span class="status-dot${state.dirty ? ' status-dirty' : ''}" aria-hidden="true"></span><strong>${state.saving ? 'Salvando a base' : state.dirty ? 'Alterações não salvas' : 'Base sincronizada'}</strong><span class="revision">Revisão ${state.document.databaseRevision}</span></div><div>${state.dirty ? `<button id="discard" class="text-button"${state.saving ? ' disabled' : ''}>Descartar alterações</button>` : '<span class="save-help">Salve aqui. Atualize o jogo. Teste em campo.</span>'}</div>` : '<span>Base local compartilhada com o jogo</span>';
  $('#discard')?.addEventListener('click', () => showDialog({ title: 'Descartar as alterações?', description: 'Todas as edições não salvas desta sessão serão removidas. A última base carregada será restaurada.', action: 'Descartar alterações', danger: true, onAccept: () => { state.document = clone(state.original); state.issues = []; state.feedback = null; state.dirty = false; ensureSelection(); renderAll(); announce('Alterações descartadas.'); queueMicrotask(() => $('#detail-title')?.focus()); } }));
}

function renderFeedback() {
  const feedback = state.feedback;
  $('#feedback').innerHTML = feedback ? `<div class="notice feedback notice-${feedback.tone || 'info'}" role="${feedback.tone === 'danger' ? 'alert' : 'status'}"><div><strong>${e(feedback.title)}</strong><p>${e(feedback.message)}</p>${state.issues.length ? `<ul class="error-list">${state.issues.slice(0, 5).map(issue => `<li>${e(describeIssue(issue))}</li>`).join('')}</ul>${state.issues.length > 5 ? `<p>E mais ${state.issues.length - 5} campos para corrigir.</p>` : ''}` : ''}</div>${feedback.conflict ? '<button class="button button-outline" id="reload-conflict">Carregar versão atual</button>' : ''}</div>` : '';
  $('#reload-conflict')?.addEventListener('click', () => showDialog({ title: 'Carregar a base atual?', description: 'Seu rascunho será substituído pela base salva. Exporte o JSON antes se quiser guardar suas edições.', action: 'Carregar versão atual', danger: true, onAccept: () => { load(); } }));
}

function describeIssue(issue) {
  const path = issue.path.replace(/^\$\.?/, '');
  const match = /^(players|clubs)\[(\d+)\]/.exec(path);
  const record = match && state.document[match[1]][Number(match[2])];
  return record ? `${record.name || 'Sem nome'}: ${issue.message}` : issue.message;
}

function renderWorkspace() {
  if (!state.document) return;
  ensureSelection();
  const players = state.view === 'players';
  $('#workspace').classList.add('workspace-ready');
  $('#workspace').innerHTML = `<section class="list-panel" aria-label="${players ? 'Lista de jogadores' : 'Lista de clubes'}"><div class="list-heading"><div><p class="eyebrow">${players ? 'ELENCO' : 'CLUBES DA BASE'}</p><h2 id="list-heading">${players ? 'Todos os jogadores' : 'Todos os clubes'}</h2></div><button class="icon-button" id="create-record" aria-label="${players ? 'Adicionar jogador' : 'Adicionar clube'}" title="${players ? 'Adicionar jogador' : 'Adicionar clube'}">+</button></div><div class="list-filters"><label class="sr-only" for="search">${players ? 'Buscar jogador' : 'Buscar clube'}</label><div class="search-wrap"><svg viewBox="0 0 20 20" aria-hidden="true"><circle cx="8" cy="8" r="5" fill="none" stroke="currentColor" stroke-width="1.6"/><path d="m12 12 5 5" stroke="currentColor" stroke-width="1.6"/></svg><input id="search" type="search" autocomplete="off" placeholder="${players ? 'Buscar jogador…' : 'Buscar clube…'}" value="${e(state.query)}"><button class="search-clear" id="clear-search" aria-label="Limpar busca"${state.query ? '' : ' hidden'}>×</button></div>${players ? select({ id: 'club-filter', label: 'Filtrar por clube', value: state.club, options: [{ value: '', label: 'Todos os clubes' }, ...state.document.clubs.map(club => ({ value: club.id, label: club.name })), { value: 'free', label: 'Sem clube' }] }) : ''}</div><div id="list-summary" class="list-summary" aria-live="polite"></div><div id="record-list" class="record-list"></div><div id="pagination" class="pagination"></div></section><section id="detail-panel" class="detail-panel" aria-label="Ficha de edição"></section>`;
  $('#create-record').disabled = state.saving;
  $('#create-record').addEventListener('click', createRecord);
  $('#search').addEventListener('input', event => {
    if (event.isComposing) return;
    updateQuery(event.target.value);
  });
  $('#search').addEventListener('compositionend', event => updateQuery(event.target.value));
  $('#clear-search').addEventListener('click', () => { $('#search').value = ''; updateQuery(''); $('#search').focus(); });
  $('#club-filter')?.addEventListener('change', event => { state.club = event.target.value; state.page = 1; renderList(); writeUrl(); });
  renderList(); renderDetail();
}

function updateQuery(value) {
  state.query = value; state.page = 1; $('#clear-search').hidden = !value;
  renderList(); writeUrl();
}

function renderList() {
  const scrollTop = $('#record-list').scrollTop;
  const filtered = records();
  const pages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  state.page = Math.min(state.page, pages);
  const offset = (state.page - 1) * PAGE_SIZE;
  $('#list-summary').textContent = filtered.length ? `${offset + 1}–${Math.min(offset + PAGE_SIZE, filtered.length)} de ${filtered.length} ${state.view === 'players' ? 'jogadores' : 'clubes'}` : 'Nenhum resultado';
  $('#record-list').innerHTML = filtered.length ? filtered.slice(offset, offset + PAGE_SIZE).map(record => {
    const subtitle = state.view === 'players' ? clubFor(state.document, record.id)?.name || 'Sem clube' : `${state.document.memberships.filter(member => member.clubId === record.id).length} jogadores`;
    const position = state.view === 'players' ? record.naturalPositions[0] || '—' : 'FC';
    return `<button class="record-row" data-record-id="${e(record.id)}"${record.id === state.selectedId ? ' aria-current="true"' : ''}${state.saving ? ' disabled' : ''}><span class="position-badge${position === 'GK' ? ' position-gk' : ''}">${e(position)}</span><span class="record-info"><strong>${e(record.name || 'Sem nome')}</strong><small>${e(subtitle)}</small></span><span class="row-arrow" aria-hidden="true">›</span></button>`;
  }).join('') : '<div class="empty-list"><strong>Nenhum cadastro encontrado</strong><p>Experimente outro nome ou remova o filtro de clube.</p><button class="text-button" id="reset-filters">Limpar filtros</button></div>';
  $('#record-list').scrollTop = scrollTop;
  $('#record-list').querySelectorAll('[data-record-id]').forEach(button => button.addEventListener('click', () => { state.selectedId = button.dataset.recordId; renderList(); renderDetail(); writeUrl(); $('#record-list [aria-current="true"]')?.focus({ preventScroll: true }); }));
  $('#reset-filters')?.addEventListener('click', () => { state.query = ''; state.club = ''; state.page = 1; renderWorkspace(); writeUrl(); $('#search').focus(); });
  $('#pagination').innerHTML = `<button class="pagination-button" id="previous-page" aria-label="Página anterior"${state.page <= 1 || state.saving ? ' disabled' : ''}>←</button><span>Página ${state.page} de ${pages}</span><button class="pagination-button" id="next-page" aria-label="Próxima página"${state.page >= pages || state.saving ? ' disabled' : ''}>→</button>`;
  $('#previous-page').addEventListener('click', () => { state.page--; $('#record-list').scrollTop = 0; renderList(); writeUrl(); ($('#previous-page').disabled ? $('#next-page') : $('#previous-page')).focus(); });
  $('#next-page').addEventListener('click', () => { state.page++; $('#record-list').scrollTop = 0; renderList(); writeUrl(); ($('#next-page').disabled ? $('#previous-page') : $('#next-page')).focus(); });
}

function renderDetail() {
  const record = selectedRecord();
  if (!record) { $('#detail-panel').innerHTML = '<div class="state-panel"><h2>Selecione um cadastro</h2><p>Escolha um item da lista para editar sua ficha.</p></div>'; return; }
  $('#detail-panel').innerHTML = state.view === 'players' ? playerForm(record, state.document, state.options, state.tab) : clubForm(record, state.document);
  $('#record-form').addEventListener('submit', event => { event.preventDefault(); save(); });
  $('#record-fields').disabled = state.saving;
  $('#detail-panel').querySelectorAll('[data-player-tab]').forEach(button => {
    button.disabled = state.saving;
    button.addEventListener('click', () => { state.tab = button.dataset.playerTab; renderDetail(); writeUrl(); $(`[data-player-tab="${state.tab}"]`).focus(); });
  });
  $('#delete-record').disabled = state.saving;
  $('#delete-record').addEventListener('click', removeRecord);
  $('#view-roster')?.addEventListener('click', () => { state.club = record.id; state.view = 'players'; state.query = ''; state.page = 1; state.selectedId = ''; renderWorkspace(); renderNavigation(); writeUrl(); });
  $('#enable-appearance')?.addEventListener('click', () => { enableAppearance(state.document, record.id, state.options.defaultAppearance); changed(); renderDetail(); });
  $('#record-form').addEventListener('input', event => {
    const target = event.target;
    if (target.tagName === 'SELECT' || target.type === 'checkbox') return;
    updateField(target, record);
  });
  $('#record-form').addEventListener('change', event => {
    const target = event.target;
    if (target.tagName === 'SELECT' || target.type === 'checkbox') updateField(target, record);
    renderList();
    if (target.id === 'player-name' || target.id === 'club-name') $('#detail-title').textContent = record.name || 'Sem nome';
  });
  applyFieldErrors(state.issues);
}

function updateField(input, record) {
  if (state.saving) return;
  const numeric = input.value === '' ? null : Number(input.value);
  if (input.id === 'player-name' || input.id === 'club-name') record.name = input.value;
  else if (input.id === 'player-height') record.heightCm = numeric;
  else if (input.id === 'player-weight') record.weightKg = numeric;
  else if (input.id === 'player-club') setMembership(state.document, record.id, input.value);
  else if (input.name === 'position') record.naturalPositions = [...document.querySelectorAll('input[name="position"]:checked')].map(item => item.value);
  else if (input.id.startsWith('attribute-')) {
    const key = input.id.slice('attribute-'.length); record.attributes[key] = numeric;
    const meter = input.closest('.attribute-field').querySelector('meter'); meter.value = numeric || 0;
    meter.setAttribute('aria-label', `${input.closest('.field').querySelector('label').textContent}: ${numeric ?? 'não informado'} de 100`);
  } else if (input.id.startsWith('appearance-')) {
    profileFor(state.document, record.id).appearance[input.id.slice('appearance-'.length)] = input.value;
    $('#appearance-preview').innerHTML = preview(profileFor(state.document, record.id).appearance, state.options);
  } else return;
  if (input.dataset.path) state.issues = state.issues.filter(issue => issue.path.replace(/^\$\.?/, '') !== input.dataset.path);
  if (input.name === 'position') state.issues = state.issues.filter(issue => !issue.path.endsWith('.naturalPositions'));
  changed(); applyFieldErrors(state.issues);
}

function changed() {
  state.dirty = JSON.stringify(state.document) !== JSON.stringify(state.original);
  if (state.feedback?.tone === 'success') { state.feedback = null; renderFeedback(); }
  renderStatus();
}

function createRecord() {
  const players = state.view === 'players';
  const nameId = 'new-name';
  showDialog({ title: players ? 'Adicionar jogador' : 'Adicionar clube', description: players ? 'Crie o cadastro e complete a ficha antes de salvar as alterações na base.' : 'Crie o clube e depois vincule jogadores ao elenco.', action: players ? 'Adicionar jogador' : 'Adicionar clube', content: `${field({ id: nameId, label: 'Nome', value: '', hint: 'Até 100 caracteres.' })}${players ? select({ id: 'new-club', label: 'Clube', value: state.club === 'free' ? '' : state.club, options: [{ value: '', label: 'Sem clube' }, ...state.document.clubs.map(club => ({ value: club.id, label: club.name }))] }) : ''}`, onAccept: dialog => {
    const input = dialog.querySelector(`#${nameId}`);
    const name = input.value;
    if (!name.trim() || [...name].length > 100) { input.setAttribute('aria-invalid', 'true'); dialog.querySelector('#new-name-error').textContent = 'Informe um nome de até 100 caracteres.'; input.focus(); return 'Informe um nome de até 100 caracteres.'; }
    const clubId = dialog.querySelector('#new-club')?.value || '';
    const record = players ? addPlayer(state.document, name, clubId, state.options) : { id: createId('club'), name };
    if (!players) state.document.clubs.push(record);
    state.query = ''; state.club = clubId; state.page = 1; state.selectedId = record.id; state.tab = 'data';
    const position = records().findIndex(item => item.id === record.id); state.page = Math.floor(position / PAGE_SIZE) + 1;
    changed(); renderWorkspace(); renderNavigation(); writeUrl(); announce(`${players ? 'Jogador adicionado' : 'Clube adicionado'} ao rascunho. Salve as alterações para publicar.`);
    queueMicrotask(() => $(`#${players ? 'player-name' : 'club-name'}`)?.focus());
  } });
}

function removeRecord() {
  const record = selectedRecord();
  const players = state.view === 'players';
  const members = !players && state.document.memberships.filter(member => member.clubId === record.id);
  if (members?.length) { state.feedback = { tone: 'warning', title: 'Este clube ainda tem jogadores', message: 'Transfira os jogadores para outro clube ou deixe-os sem clube antes de excluir o cadastro.' }; renderFeedback(); return; }
  if (!players && state.document.competitionEditions?.some(edition => edition.participantClubIds.includes(record.id))) { state.feedback = { tone: 'warning', title: 'Este clube participa de um campeonato', message: 'Remova a participação na edição do campeonato antes de excluir o clube. Por enquanto, os campeonatos são editados no JSON da base.' }; renderFeedback(); return; }
  if (state.document[state.view].length === 1) { state.feedback = { tone: 'warning', title: 'Mantenha ao menos um cadastro', message: `A base precisa de pelo menos um ${players ? 'jogador' : 'clube'}.` }; renderFeedback(); return; }
  showDialog({ title: `Excluir ${record.name}?`, description: players ? 'O jogador, seu vínculo com o clube e sua aparência serão removidos do rascunho. A exclusão só será publicada ao salvar as alterações.' : 'O clube será removido do rascunho. A exclusão só será publicada ao salvar as alterações.', action: players ? 'Excluir jogador' : 'Excluir clube', danger: true, onAccept: () => {
    if (players) deletePlayer(state.document, record.id); else state.document.clubs = state.document.clubs.filter(club => club.id !== record.id);
    state.selectedId = ''; state.issues = []; state.feedback = null; changed(); renderWorkspace(); renderNavigation(); renderFeedback(); writeUrl(); announce('Cadastro excluído do rascunho. Salve as alterações para publicar.'); queueMicrotask(() => $('#create-record')?.focus());
  } });
}

function focusIssue() {
  const issue = state.issues[0];
  if (!issue) return;
  const path = issue.path.replace(/^\$\.?/, '');
  const match = /^(players|clubs|visualProfiles)\[(\d+)\]/.exec(path);
  if (match) {
    state.view = match[1] === 'clubs' ? 'clubs' : 'players';
    const record = state.document[match[1]][Number(match[2])];
    state.selectedId = match[1] === 'visualProfiles' ? record.playerId : record.id;
    state.tab = path.includes('.attributes.') ? 'attributes' : match[1] === 'visualProfiles' ? 'appearance' : 'data';
    state.query = ''; state.club = ''; state.page = Math.floor(records().findIndex(item => item.id === state.selectedId) / PAGE_SIZE) + 1;
    renderWorkspace(); renderNavigation(); writeUrl();
  }
  const invalid = document.querySelector('[aria-invalid="true"]');
  (invalid?.querySelector('input') || invalid)?.focus();
  invalid?.scrollIntoView({ block: 'center', behavior: 'instant' });
}

async function save() {
  if (!state.document || !state.dirty || state.saving || state.loading) return;
  state.issues = validateDocument(state.document, state.options);
  if (state.issues.length) { state.feedback = { tone: 'danger', title: 'Revise os campos antes de salvar', message: 'As alterações continuam no rascunho. Corrija os campos indicados e salve novamente.' }; renderFeedback(); focusIssue(); return; }
  state.saving = true; state.feedback = null; renderStatus(); renderFeedback(); renderNavigation(); renderWorkspace();
  try {
    const result = await saveDatabase(state.document, state.etag);
    state.document = result.document; state.original = clone(result.document); state.etag = result.etag; state.dirty = false;
    state.feedback = { tone: 'success', title: 'Alterações salvas', message: `Revisão ${result.document.databaseRevision} disponível. Atualize a página do jogo para testar a base.` };
  } catch (error) {
    state.issues = error.issues || [];
    state.feedback = error.status === 409 ? { tone: 'warning', title: 'A base mudou em outra sessão', message: 'Seu rascunho foi preservado. Exporte suas alterações antes de carregar a versão atual e reaplicá-las.', conflict: true } : { tone: 'danger', title: 'Não foi possível confirmar o salvamento', message: error.message };
  } finally {
    state.saving = false; renderAll();
    if (state.issues.length) focusIssue();
  }
}

function renderAll() { renderNavigation(); renderStatus(); renderFeedback(); renderWorkspace(); }

async function load() {
  if (state.loading && state.document) return;
  state.loading = true;
  $('#workspace').classList.remove('workspace-ready');
  $('#workspace').innerHTML = '<div class="state-panel"><span class="spinner" aria-hidden="true"></span><h2>Carregando a base</h2><p>Buscando clubes, jogadores e opções de aparência.</p></div>';
  $('#workspace').setAttribute('aria-busy', 'true'); renderStatus(); renderNavigation();
  try {
    const [database, options] = await Promise.all([getDatabase(), getOptions()]);
    state.document = database.document; state.original = clone(database.document); state.etag = database.etag; state.options = options;
    state.dirty = false; state.issues = []; state.feedback = null;
    if (state.club && state.club !== 'free' && !state.document.clubs.some(club => club.id === state.club)) state.club = '';
    state.loading = false; renderAll(); writeUrl();
  } catch (error) {
    state.loading = false;
    $('#workspace').innerHTML = `<div class="state-panel"><div class="error-symbol" aria-hidden="true">!</div><h2>Não foi possível carregar a base</h2><p>${e(error.message)}</p><button id="retry-load" class="button button-primary">Tentar novamente</button>${state.document ? '<button class="button button-outline" id="return-draft">Voltar ao rascunho</button>' : ''}</div>`;
    $('#retry-load').addEventListener('click', load);
    $('#return-draft')?.addEventListener('click', renderAll);
    renderStatus(); renderNavigation();
  } finally { $('#workspace').setAttribute('aria-busy', 'false'); }
}

$('#save').addEventListener('click', save);
$('#export').addEventListener('click', () => { downloadJson(state.document); announce(state.dirty ? 'Rascunho exportado. As alterações ainda não foram salvas na base.' : 'Base exportada em JSON.'); });
window.addEventListener('beforeunload', event => { if (state.dirty) { event.preventDefault(); event.returnValue = ''; } });
renderNavigation(); load();
