import { getDatabase, getOptions, saveDatabase } from './api.js';
import { clone, normalize, compareNames, clubFor, profileFor, setMembership, enableAppearance, deletePlayer, validateDocument } from './model.js';
import { $, escapeHtml as e, announce, field, select, applyFieldErrors, showDialog, downloadJson } from './ui.js';
import { playerForm, preview } from './players.js';
import { clubForm } from './clubs.js';
import { hasHistory, identity, countryName, countryOptions, setRecordField, deletionBlock, playerDisplayName, playerSearchText } from './history-model.js';
import { countryField, referenceStrip, stadiumForm, countryForm, snapshotForm } from './history-ui.js';
import { views, availableViews, validFilters, creationError, addRecord, removeHistoricalRecord, addSource, removeSource } from './records.js';

const PAGE_SIZE = 10;
const initial = new URLSearchParams(location.search);
const state = {
  document: null, original: null, etag: '', options: null, loading: true, saving: false, dirty: false,
  view: Object.hasOwn(views, initial.get('view')) ? initial.get('view') : 'players', query: initial.get('q') || '', country: initial.get('country') || '',
  club: initial.get('club') || '', page: Math.max(1, Number(initial.get('page')) || 1),
  selectedId: initial.get('id') || '', tab: ['data', 'attributes', 'appearance'].includes(initial.get('tab')) ? initial.get('tab') : 'data',
  issues: [], feedback: null,
};

function writeUrl() {
  const params = new URLSearchParams();
  params.set('view', state.view);
  if (state.query) params.set('q', state.query);
  if (state.club && state.view === 'players') params.set('club', state.club);
  if (state.country && ['clubs', 'stadiums'].includes(state.view)) params.set('country', state.country);
  if (state.page > 1) params.set('page', state.page);
  if (state.selectedId) params.set('id', state.selectedId);
  if (state.tab !== 'data' && state.view === 'players') params.set('tab', state.tab);
  history.replaceState(null, '', `${location.pathname}?${params}`);
}

function selectedRecord() {
  return state.view === 'snapshot' ? state.document?.snapshot : state.document?.[state.view]?.find(record => identity(record) === state.selectedId);
}

function records() {
  if (!state.document || state.view === 'snapshot') return [];
  return state.document[state.view].filter(record => {
    const matchesName = normalize(state.view === 'players' ? playerSearchText(record) : record.name).includes(normalize(state.query));
    const matchesClub = state.view !== 'players' || !state.club || (state.club === 'free' ? !clubFor(state.document, record.id) : clubFor(state.document, record.id)?.id === state.club);
    const matchesCountry = !['clubs', 'stadiums'].includes(state.view) || !state.country || record.countryCode === state.country;
    return matchesName && matchesClub && matchesCountry;
  }).sort(state.view === 'players' ? (a, b) => playerDisplayName(a).localeCompare(playerDisplayName(b), 'pt-BR') : compareNames);
}

function ensureSelection() {
  Object.assign(state, validFilters(state.document, state));
  if (!selectedRecord()) state.selectedId = identity(records()[0]);
}

function navigate(view) {
  state.view = view; state.query = ''; state.club = ''; state.country = ''; state.page = 1; state.selectedId = '';
  renderWorkspace(); renderNavigation(); writeUrl();
  $('#detail-title')?.focus({ preventScroll: true });
}

function renderNavigation() {
  $('#navigation').innerHTML = availableViews(state.document).map(view => `<button class="nav-item" data-view="${view}"${state.view === view ? ' aria-current="page"' : ''}${!state.document || state.saving || state.loading ? ' disabled' : ''}><span>${views[view].label}</span>${view !== 'snapshot' ? `<span class="nav-count">${state.document?.[view]?.length ?? '—'}</span>` : ''}</button>`).join('');
  $('#page-title').textContent = views[state.view].label;
  document.title = `${views[state.view].label} · Editor de base · Futebol Brasileiro`;
  $('#navigation').querySelectorAll('button').forEach(button => button.addEventListener('click', () => {
    if (state.view === button.dataset.view) return;
    navigate(button.dataset.view);
  }));
}

function renderReference() {
  $('#base-reference').innerHTML = referenceStrip(state.document);
  const open = $('#open-reference');
  if (open) { open.disabled = state.saving || state.loading; open.addEventListener('click', () => navigate('snapshot')); }
}

function renderStatus() {
  const save = $('#save');
  save.disabled = !state.document || !state.dirty || state.saving || state.loading;
  save.textContent = state.saving ? 'Salvando…' : 'Salvar alterações';
  save.setAttribute('aria-busy', String(state.saving));
  $('#export').disabled = !state.document || state.saving;
  $('#save-status').innerHTML = state.document ? `<div><span class="status-dot${state.dirty ? ' status-dirty' : ''}" aria-hidden="true"></span><strong>${state.saving ? 'Salvando a base' : state.dirty ? 'Alterações não salvas' : 'Base sincronizada'}</strong><span class="revision">Revisão ${state.document.databaseRevision}</span></div><div>${state.dirty ? `<button id="discard" class="text-button"${state.saving ? ' disabled' : ''}>Descartar alterações</button>` : '<span class="save-help">Salve aqui. Atualize o jogo. Teste em campo.</span>'}</div>` : '<span>Base local compartilhada com o jogo</span>';
  $('#discard')?.addEventListener('click', () => showDialog({ title: 'Descartar as alterações?', description: 'Todas as edições não salvas desta sessão serão removidas. A última base carregada será restaurada.', action: 'Descartar alterações', danger: true, onAccept: () => { state.document = clone(state.original); state.issues = []; state.feedback = null; state.dirty = false; ensureSelection(); renderAll(); writeUrl(); announce('Alterações descartadas.'); queueMicrotask(() => $('#detail-title')?.focus()); } }));
}

function renderFeedback() {
  const feedback = state.feedback;
  $('#feedback').innerHTML = feedback ? `<div class="notice feedback notice-${feedback.tone || 'info'}" role="${feedback.tone === 'danger' ? 'alert' : 'status'}"><div><strong>${e(feedback.title)}</strong><p>${e(feedback.message)}</p>${state.issues.length ? `<ul class="error-list">${state.issues.slice(0, 5).map(issue => `<li>${e(describeIssue(issue))}</li>`).join('')}</ul>${state.issues.length > 5 ? `<p>E mais ${state.issues.length - 5} campos para corrigir.</p>` : ''}` : ''}</div>${feedback.conflict ? '<button class="button button-outline" id="reload-conflict">Carregar versão atual</button>' : ''}</div>` : '';
  $('#reload-conflict')?.addEventListener('click', () => showDialog({ title: 'Carregar a base atual?', description: 'Seu rascunho será substituído pela base salva. Exporte o JSON antes se quiser guardar suas edições.', action: 'Carregar versão atual', danger: true, onAccept: () => { load(); } }));
}

function describeIssue(issue) {
  const path = issue.path.replace(/^\$\.?/, '');
  const match = /^(players|clubs|countries|stadiums)\[(\d+)\]/.exec(path);
  const record = match && state.document[match[1]][Number(match[2])];
  return record ? `${record.name || 'Sem nome'}: ${issue.message}` : issue.message;
}

function renderWorkspace() {
  if (!state.document) return;
  if (!availableViews(state.document).includes(state.view)) state.view = 'players';
  ensureSelection();
  const players = state.view === 'players';
  const config = views[state.view];
  $('#workspace').classList.add('workspace-ready');
  $('#workspace').classList.toggle('workspace-reference', state.view === 'snapshot');
  if (state.view === 'snapshot') {
    $('#workspace').innerHTML = '<section id="detail-panel" class="detail-panel reference-panel" aria-label="Referência histórica"></section>';
    renderDetail(); return;
  }
  const filter = players ? select({ id: 'club-filter', label: 'Filtrar por clube', value: state.club, options: [{ value: '', label: 'Todos os clubes' }, ...state.document.clubs.map(club => ({ value: club.id, label: club.name })), { value: 'free', label: 'Sem clube' }] }) : hasHistory(state.document) && ['clubs', 'stadiums'].includes(state.view) ? select({ id: 'country-filter', label: 'Filtrar por país', value: state.country, options: [{ value: '', label: 'Todos os países' }, ...countryOptions(state.document)] }) : '';
  $('#workspace').innerHTML = `<section class="list-panel" aria-label="Lista de ${config.plural}"><div class="list-heading"><div><p class="eyebrow">${config.eyebrow}</p><h2 id="list-heading">${config.heading}</h2></div><button class="icon-button" id="create-record" aria-label="Adicionar ${config.singular}" title="Adicionar ${config.singular}">+</button></div><div class="list-filters"><label class="sr-only" for="search">Buscar ${config.singular}</label><div class="search-wrap"><svg viewBox="0 0 20 20" aria-hidden="true"><circle cx="8" cy="8" r="5" fill="none" stroke="currentColor" stroke-width="1.6"/><path d="m12 12 5 5" stroke="currentColor" stroke-width="1.6"/></svg><input id="search" type="search" autocomplete="off" placeholder="Buscar ${config.singular}…" value="${e(state.query)}"><button class="search-clear" id="clear-search" aria-label="Limpar busca"${state.query ? '' : ' hidden'}>×</button></div>${filter}</div><div id="list-summary" class="list-summary" aria-live="polite"></div><div id="record-list" class="record-list"></div><div id="pagination" class="pagination"></div></section><section id="detail-panel" class="detail-panel" aria-label="Ficha de edição"></section>`;
  $('#create-record').disabled = state.saving;
  $('#create-record').addEventListener('click', createRecord);
  $('#search').addEventListener('input', event => {
    if (event.isComposing) return;
    updateQuery(event.target.value);
  });
  $('#search').addEventListener('compositionend', event => updateQuery(event.target.value));
  $('#clear-search').addEventListener('click', () => { $('#search').value = ''; updateQuery(''); $('#search').focus(); });
  $('#club-filter')?.addEventListener('change', event => { state.club = event.target.value; state.page = 1; renderList(); writeUrl(); });
  $('#country-filter')?.addEventListener('change', event => { state.country = event.target.value; state.page = 1; renderList(); writeUrl(); });
  renderList(); renderDetail();
}

function updateQuery(value) {
  state.query = value; state.page = 1; $('#clear-search').hidden = !value;
  renderList(); writeUrl();
}

function renderList() {
  if (state.view === 'snapshot') return;
  const scrollTop = $('#record-list').scrollTop;
  const filtered = records();
  const pages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  state.page = Math.min(state.page, pages);
  const offset = (state.page - 1) * PAGE_SIZE;
  $('#list-summary').textContent = filtered.length ? `${offset + 1}–${Math.min(offset + PAGE_SIZE, filtered.length)} de ${filtered.length} ${views[state.view].plural}` : 'Nenhum resultado';
  $('#record-list').innerHTML = filtered.length ? filtered.slice(offset, offset + PAGE_SIZE).map(record => {
    const subtitle = state.view === 'players' ? clubFor(state.document, record.id)?.name || 'Sem clube' : state.view === 'clubs' ? `${state.document.memberships.filter(member => member.clubId === record.id).length} jogadores${record.countryCode ? ` · ${countryName(state.document, record.countryCode)}` : ''}` : state.view === 'stadiums' ? `${record.city} · ${countryName(state.document, record.countryCode)}` : `Código ${record.code}`;
    const position = state.view === 'players' ? record.naturalPositions[0] || '—' : state.view === 'countries' ? record.code : state.view === 'stadiums' ? '◎' : 'FC';
    return `<button class="record-row" data-record-id="${e(identity(record))}"${identity(record) === state.selectedId ? ' aria-current="true"' : ''}${state.saving ? ' disabled' : ''}><span class="position-badge${position === 'GK' ? ' position-gk' : ''}">${e(position)}</span><span class="record-info"><strong>${e((state.view === 'players' ? playerDisplayName(record) : record.name) || 'Sem nome')}</strong><small>${e(subtitle)}</small></span><span class="row-arrow" aria-hidden="true">›</span></button>`;
  }).join('') : '<div class="empty-list"><strong>Nenhum cadastro encontrado</strong><p>Adicione um cadastro ou ajuste a busca e os filtros.</p><button class="text-button" id="reset-filters">Limpar filtros</button></div>';
  $('#record-list').scrollTop = scrollTop;
  $('#record-list').querySelectorAll('[data-record-id]').forEach(button => button.addEventListener('click', () => { state.selectedId = button.dataset.recordId; renderList(); renderDetail(); writeUrl(); $('#record-list [aria-current="true"]')?.focus({ preventScroll: true }); }));
  $('#reset-filters')?.addEventListener('click', () => { state.query = ''; state.club = ''; state.country = ''; state.page = 1; renderWorkspace(); writeUrl(); $('#search').focus(); });
  $('#pagination').innerHTML = `<button class="pagination-button" id="previous-page" aria-label="Página anterior"${state.page <= 1 || state.saving ? ' disabled' : ''}>←</button><span>Página ${state.page} de ${pages}</span><button class="pagination-button" id="next-page" aria-label="Próxima página"${state.page >= pages || state.saving ? ' disabled' : ''}>→</button>`;
  $('#previous-page').addEventListener('click', () => { state.page--; $('#record-list').scrollTop = 0; renderList(); writeUrl(); ($('#previous-page').disabled ? $('#next-page') : $('#previous-page')).focus(); });
  $('#next-page').addEventListener('click', () => { state.page++; $('#record-list').scrollTop = 0; renderList(); writeUrl(); ($('#next-page').disabled ? $('#previous-page') : $('#next-page')).focus(); });
}

function renderDetail() {
  const record = selectedRecord();
  if (!record) { $('#detail-panel').innerHTML = '<div class="state-panel"><h2>Selecione um cadastro</h2><p>Escolha um item da lista para editar sua ficha.</p></div>'; return; }
  const forms = { players: () => playerForm(record, state.document, state.options, state.tab), clubs: () => clubForm(record, state.document), stadiums: () => stadiumForm(record, state.document), countries: () => countryForm(record, state.document), snapshot: () => snapshotForm(state.document) };
  $('#detail-panel').innerHTML = forms[state.view]();
  $('#record-form').addEventListener('submit', event => { event.preventDefault(); save(); });
  $('#record-fields').disabled = state.saving;
  $('#detail-panel').querySelectorAll('[data-player-tab]').forEach(button => {
    button.disabled = state.saving;
    button.addEventListener('click', () => { state.tab = button.dataset.playerTab; renderDetail(); writeUrl(); $(`[data-player-tab="${state.tab}"]`).focus(); });
  });
  if ($('#delete-record')) { $('#delete-record').disabled = state.saving; $('#delete-record').addEventListener('click', removeRecord); }
  $('#view-roster')?.addEventListener('click', () => { state.club = record.id; state.view = 'players'; state.query = ''; state.page = 1; state.selectedId = ''; renderWorkspace(); renderNavigation(); writeUrl(); });
  $('#enable-appearance')?.addEventListener('click', () => { enableAppearance(state.document, record.id, state.options.defaultAppearance); changed(); renderDetail(); });
  $('#add-source')?.addEventListener('click', () => {
    if (!addSource(state.document)) { state.feedback = { tone: 'warning', title: 'Limite de fontes atingido', message: 'A base aceita até 128 fontes. Edite uma fonte existente.' }; renderFeedback(); return; }
    changed(); renderDetail(); $(`#source-title-${state.document.snapshot.sources.length - 1}`)?.focus();
  });
  $('#detail-panel').querySelectorAll('[data-remove-source]').forEach(button => button.addEventListener('click', () => {
    if (state.document.snapshot.sources.length <= 1) { state.feedback = { tone: 'warning', title: 'Mantenha uma fonte', message: 'A referência histórica precisa de pelo menos uma fonte de pesquisa.' }; renderFeedback(); return; }
    const source = state.document.snapshot.sources.find(item => item.id === button.dataset.removeSource);
    showDialog({ title: 'Remover esta fonte?', description: `A fonte “${source.title || 'Sem título'}” será removida do rascunho. A alteração só será publicada ao salvar.`, action: 'Remover fonte', danger: true, onAccept: () => { removeSource(state.document, source.id); state.issues = []; changed(); renderDetail(); queueMicrotask(() => $('#add-source')?.focus()); } });
  }));
  $('#record-form').querySelectorAll('textarea').forEach(autoGrow);
  $('#record-form').addEventListener('input', event => {
    const target = event.target;
    if (target.tagName === 'SELECT' || target.type === 'checkbox') return;
    updateField(target, record);
    if (target.tagName === 'TEXTAREA') autoGrow(target);
  });
  $('#record-form').addEventListener('change', event => {
    const target = event.target;
    if (target.tagName === 'SELECT' || target.type === 'checkbox') updateField(target, record);
    renderList();
    if (target.id === 'player-name' || target.id === 'club-name' || target.dataset.property === 'name' || target.dataset.property === 'nickname') $('#detail-title').textContent = (state.view === 'players' ? record.nickname ?? record.name : record.name) || 'Sem nome';
  });
  applyFieldErrors(state.issues);
}

function autoGrow(textarea) { textarea.style.height = 'auto'; textarea.style.height = `${Math.max(128, textarea.scrollHeight)}px`; }

function updateField(input, record) {
  if (state.saving) return;
  const numeric = input.value === '' ? null : Number(input.value);
  if (input.dataset.property) {
    const target = input.dataset.sourceId ? state.document.snapshot.sources.find(source => source.id === input.dataset.sourceId) : record;
    setRecordField(target, input.dataset.property, input.value, { optional: input.dataset.optional === 'true', numeric: input.type === 'number' });
    if (state.view === 'snapshot' && !input.dataset.sourceId) renderReference();
  }
  else if (input.id === 'player-name' || input.id === 'club-name') record.name = input.value;
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
  const config = views[state.view];
  const locationFields = state.view === 'stadiums' || (state.view === 'clubs' && hasHistory(state.document));
  const content = `${field({ id: 'new-name', label: 'Nome', value: '', maxLength: 100, hint: 'Até 100 caracteres.' })}${players ? select({ id: 'new-club', label: 'Clube', value: state.club === 'free' ? '' : state.club, options: [{ value: '', label: 'Sem clube' }, ...state.document.clubs.map(club => ({ value: club.id, label: club.name }))] }) : ''}${state.view === 'countries' ? field({ id: 'new-code', label: 'Código do país', value: '', maxLength: 2, hint: 'Código ISO de duas letras maiúsculas, como BR. Não poderá ser alterado.' }) : ''}${locationFields ? `${countryField(state.document, { id: 'new-country', value: state.country })}${field({ id: 'new-city', label: 'Cidade', value: '', maxLength: 100 })}` : ''}`;
  showDialog({ title: `Adicionar ${config.singular}`, description: 'Crie o cadastro e complete a ficha antes de salvar as alterações na base.', action: `Adicionar ${config.singular}`, content, onAccept: dialog => {
    const value = id => dialog.querySelector(`#${id}`)?.value || '';
    const values = { name: value('new-name'), clubId: value('new-club'), code: value('new-code'), countryCode: value('new-country'), city: value('new-city') };
    const error = creationError(state.document, state.view, values);
    if (error) return error;
    const record = addRecord(state.document, state.view, values, state.options);
    state.query = ''; state.club = values.clubId; state.country = ''; state.page = 1; state.selectedId = identity(record); state.tab = 'data';
    const position = records().findIndex(item => identity(item) === identity(record)); state.page = Math.floor(position / PAGE_SIZE) + 1;
    changed(); renderWorkspace(); renderNavigation(); writeUrl(); announce('Cadastro adicionado ao rascunho. Salve as alterações para publicar.');
    queueMicrotask(() => $(`#${config.prefix}-name`)?.focus());
  } });
}

function removeRecord() {
  const record = selectedRecord();
  const players = state.view === 'players';
  const members = state.view === 'clubs' && state.document.memberships.filter(member => member.clubId === record.id);
  if (members?.length) { state.feedback = { tone: 'warning', title: 'Este clube ainda tem jogadores', message: 'Transfira os jogadores para outro clube ou deixe-os sem clube antes de excluir o cadastro.' }; renderFeedback(); return; }
  if (state.view === 'clubs' && state.document.competitionEditions?.some(edition => edition.participantClubIds.includes(record.id))) { state.feedback = { tone: 'warning', title: 'Este clube participa de um campeonato', message: 'Remova a participação na edição do campeonato antes de excluir o clube. Por enquanto, os campeonatos são editados no JSON da base.' }; renderFeedback(); return; }
  const blocked = deletionBlock(state.document, state.view, record);
  if (blocked) { state.feedback = { tone: 'warning', title: 'Este cadastro precisa ser preservado', message: blocked }; renderFeedback(); return; }
  if (['players', 'clubs'].includes(state.view) && state.document[state.view].length === 1) { state.feedback = { tone: 'warning', title: 'Mantenha ao menos um cadastro', message: `A base precisa de pelo menos um ${views[state.view].singular}.` }; renderFeedback(); return; }
  showDialog({ title: `Excluir ${record.name}?`, description: players ? 'O jogador, seu vínculo com o clube e sua aparência serão removidos do rascunho. A exclusão só será publicada ao salvar as alterações.' : 'O cadastro será removido do rascunho. A exclusão só será publicada ao salvar as alterações.', action: `Excluir ${views[state.view].singular}`, danger: true, onAccept: () => {
    if (players) deletePlayer(state.document, record.id); else removeHistoricalRecord(state.document, state.view, record);
    state.selectedId = ''; state.issues = []; state.feedback = null; changed(); renderWorkspace(); renderNavigation(); renderFeedback(); writeUrl(); announce('Cadastro excluído do rascunho. Salve as alterações para publicar.'); queueMicrotask(() => $('#create-record')?.focus());
  } });
}

function focusIssue() {
  const issue = state.issues[0];
  if (!issue) return;
  const path = issue.path.replace(/^\$\.?/, '');
  const match = /^(players|clubs|stadiums|countries|visualProfiles)\[(\d+)\]/.exec(path);
  if (match) {
    state.view = match[1] === 'visualProfiles' ? 'players' : match[1];
    const record = state.document[match[1]][Number(match[2])];
    state.selectedId = match[1] === 'visualProfiles' ? record.playerId : identity(record);
    state.tab = path.includes('.attributes.') ? 'attributes' : match[1] === 'visualProfiles' ? 'appearance' : 'data';
    state.query = ''; state.club = ''; state.country = ''; state.page = Math.max(1, Math.floor(records().findIndex(item => identity(item) === state.selectedId) / PAGE_SIZE) + 1);
    renderWorkspace(); renderNavigation(); writeUrl();
  } else if (path.startsWith('snapshot')) navigate('snapshot');
  const invalid = document.querySelector('[aria-invalid="true"]');
  (invalid?.querySelector('input') || invalid)?.focus();
  invalid?.scrollIntoView({ block: 'center', behavior: 'instant' });
}

async function save() {
  if (!state.document || !state.dirty || state.saving || state.loading) return;
  state.issues = validateDocument(state.document, state.options);
  if (state.issues.length) { state.feedback = { tone: 'danger', title: 'Revise os campos antes de salvar', message: 'As alterações continuam no rascunho. Corrija os campos indicados e salve novamente.' }; renderFeedback(); focusIssue(); return; }
  state.saving = true; state.feedback = null; renderStatus(); renderFeedback(); renderNavigation(); renderReference(); renderWorkspace();
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

function renderAll() { if (state.document && !availableViews(state.document).includes(state.view)) state.view = 'players'; renderNavigation(); renderStatus(); renderReference(); renderFeedback(); renderWorkspace(); }

async function load() {
  if (state.loading && state.document) return;
  state.loading = true;
  $('#workspace').classList.remove('workspace-ready');
  $('#workspace').classList.remove('workspace-reference');
  $('#workspace').innerHTML = '<div class="state-panel"><span class="spinner" aria-hidden="true"></span><h2>Carregando a base</h2><p>Buscando clubes, jogadores e opções de aparência.</p></div>';
  $('#workspace').setAttribute('aria-busy', 'true'); renderStatus(); renderNavigation(); renderReference();
  try {
    const [database, options] = await Promise.all([getDatabase(), getOptions()]);
    state.document = database.document; state.original = clone(database.document); state.etag = database.etag; state.options = options;
    state.dirty = false; state.issues = []; state.feedback = null;
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
