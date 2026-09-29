import { clubFor, profileFor, isBuiltin } from './model.js';
import { escapeHtml as e, field, select } from './ui.js';
import { hasHistory, playerDisplayName } from './history-model.js';
import { playerHistoryFields, notesField } from './history-ui.js';

export function playerHeader(player, document) {
  const club = clubFor(document, player.id);
  return `<div class="detail-heading"><div><p class="eyebrow">FICHA DO JOGADOR</p><h2 id="detail-title" tabindex="-1">${e(playerDisplayName(player) || 'Jogador sem nome')}</h2><p class="detail-subtitle">${e(club?.name || 'Sem clube')} <span aria-hidden="true">·</span> ${e(player.naturalPositions.join(' / ') || 'Sem posição')}</p></div><span class="player-monogram" aria-hidden="true">${e(playerDisplayName(player).split(' ').filter(Boolean).map(word => [...word][0]).slice(0, 2).join(''))}</span></div>`;
}

export function playerForm(player, document, options, tab) {
  const index = document.players.indexOf(player);
  const path = `players[${index}]`;
  const tabs = [['data', 'Ficha'], ['attributes', 'Atributos'], ['appearance', 'Aparência']];
  const content = tab === 'appearance' ? appearanceForm(player, document, options) : tab === 'attributes' ? attributesForm(player, options, path) : dataForm(player, document, options, path);
  return `${playerHeader(player, document)}<div class="detail-tabs" role="group" aria-label="Seções da ficha">${tabs.map(([key, label]) => `<button class="detail-tab" data-player-tab="${key}" aria-pressed="${tab === key}">${label}</button>`).join('')}</div><form id="record-form" novalidate><fieldset id="record-fields"><legend class="sr-only">Editar ${e(player.name)}</legend>${content}</fieldset></form><div class="detail-footer"><details class="record-id"><summary>Identificador permanente</summary><code>${e(player.id)}</code><p>O identificador é preservado ao editar ou transferir o jogador.</p></details><button class="button button-ghost-danger" id="delete-record">Excluir jogador</button></div>`;
}

function dataForm(player, document, options, path) {
  const club = clubFor(document, player.id);
  return `<div class="section-label"><h3>Identificação e elenco</h3><p>Dados usados na escalação e na partida.</p></div><div class="form-grid">${field({ id: 'player-name', label: 'Nome do jogador', value: player.name, path: `${path}.name` })}${select({ id: 'player-club', label: 'Clube', value: club?.id || '', options: [{ value: '', label: 'Sem clube' }, ...document.clubs.map(item => ({ value: item.id, label: item.name }))], hint: 'Trocar o clube transfere o vínculo do jogador.' })}</div>${playerHistoryFields(player, document, path)}<div class="section-label"><h3>Características físicas</h3><p>As medidas ajustam as proporções do personagem.</p></div><div class="form-grid">${field({ id: 'player-height', label: 'Altura', value: player.heightCm, type: 'number', min: 150, max: 210, suffix: 'cm', path: `${path}.heightCm`, hint: 'De 150 a 210 cm.' })}${field({ id: 'player-weight', label: 'Peso', value: player.weightKg, type: 'number', min: 45, max: 100, suffix: 'kg', path: `${path}.weightKg`, hint: 'De 45 a 100 kg.' })}</div><fieldset class="position-field" id="player-positions" data-path="${path}.naturalPositions" aria-describedby="player-positions-hint player-positions-error"><legend>Posições naturais</legend><p id="player-positions-hint">Selecione uma ou mais posições. A formação do time define onde o jogador será escalado.</p><div class="position-grid">${options.positions.map(({ value, label }) => `<label class="position-option"><input type="checkbox" name="position" value="${e(value)}"${player.naturalPositions.includes(value) ? ' checked' : ''}><span><strong>${e(value)}</strong>${e(label)}</span></label>`).join('')}</div><small class="field-error" id="player-positions-error"></small></fieldset>${hasHistory(document) ? notesField(player, 'player', path) : ''}`;
}

function attributesForm(player, options, path) {
  return `<div class="section-label"><h3>Habilidades em campo</h3><p>Valores inteiros de 0 a 100. Edite o número para fazer ajustes precisos.</p></div><div class="attributes-grid">${options.attributes.map(({ field: key, label }) => `<div class="attribute-field">${field({ id: `attribute-${key}`, label, value: player.attributes[key], type: 'number', min: 0, max: 100, path: `${path}.attributes.${key}` })}<meter min="0" max="100" value="${player.attributes[key]}" aria-label="${e(label)}: ${player.attributes[key]} de 100"></meter></div>`).join('')}</div>`;
}

function appearanceForm(player, document, options) {
  const profile = profileFor(document, player.id);
  if (!isBuiltin(profile)) return `<div class="notice notice-warning"><strong>Skin personalizada vinculada</strong><p>Este perfil usa ${e(profile.skin.skinId)}, revisão ${profile.skin.revision}. Os controles da aparência padrão não substituem essa skin. A importação e a preparação de modelos da comunidade serão uma etapa própria.</p></div>`;
  if (!profile?.appearance) return `<div class="notice"><strong>Aparência definida no jogo</strong><p>Este jogador ainda usa o visual configurado no Unity. Para editar aqui, ative uma aparência padrão. O jogo passará a usar os valores definidos nesta ficha.</p><button type="button" class="button button-primary" id="enable-appearance">Definir aparência na base</button></div>`;
  const profileIndex = document.visualProfiles.indexOf(profile);
  return `<div class="section-label"><h3>Aparência do personagem</h3><p>Personalize o modelo padrão. O uniforme principal acompanha o clube.</p></div><div class="appearance-layout"><div id="appearance-preview">${preview(profile.appearance, options)}</div><div class="appearance-fields">${options.appearance.map(group => select({ id: `appearance-${group.field}`, label: group.label, value: profile.appearance[group.field], options: group.options, path: `visualProfiles[${profileIndex}].appearance.${group.field}`, hint: group.field === 'sockAccessoryColor' ? 'Cor da faixa/acessório. A meia principal pertence ao uniforme.' : '' })).join('')}</div></div><p class="form-note">Rostos e modelos próprios da comunidade ainda precisam do fluxo de importação de skins. Estes controles alteram somente as opções do personagem padrão.</p>`;
}

export function preview(appearance, options) {
  const color = (field, fallback) => {
    const value = options.appearance.find(group => group.field === field)?.options.find(option => option.value === appearance[field])?.color;
    return /^#[0-9a-f]{6}$/i.test(value || '') ? value : fallback;
  };
  const skin = color('skinTone', '#bc8968'), hair = color('hairColor', '#34312f'), beard = color('beardColor', '#34312f'), boots = color('bootsColor', '#222c35'), socks = color('sockAccessoryColor', '#eeeeee');
  const hairPaths = {
    short: '<path d="M75 57q0-25 25-25t25 25l-8-12H83Z"/>',
    styled: '<path d="M75 60V42l10-12 32-3 11 16-5 15-7-14-31 5Z"/>',
    'styled-alt': '<path d="m74 60-2-19 10-17 41 7 6 20-9 10-4-18-30-1Z"/>',
    mohawk: '<path d="m91 42 2-19 7-7 8 10 1 16Z"/>',
    locs: '<path d="m75 62-7-15 8-16 18-8 24 5 15 20-10 29-6-27-8-10-27 12-2 19Z"/>',
    'short-parted': '<path d="M75 58q-4-28 25-28 28 0 25 28l-10-15-12-1-3-8-5 8-11 3Z"/>',
  };
  const beards = {
    mustache: '<path d="m89 75 11-4 11 4-1 4-10-3-10 3Z"/>',
    goatee: '<path d="m93 81 7 3 7-3-1 11h-12Z"/><path d="m89 75 11-4 11 4-1 4-10-3-10 3Z"/>',
    full: '<path d="m78 66 7 11 15 5 15-5 7-11-3 23-19 10-19-10Z"/>',
  };
  return `<figure class="player-preview"><div class="preview-label">MODELO PADRÃO</div><svg viewBox="0 0 200 340" role="img" aria-label="Prévia ilustrativa das cores e opções selecionadas"><ellipse cx="100" cy="318" rx="61" ry="9" fill="#ccded9"/><path d="M55 129 39 195l16 7 25-70M145 129l16 66-16 7-25-70" fill="${skin}" stroke="#183d4720"/><path d="m69 111-22 19 25 22 4 61h48l4-61 25-22-22-19-16-6H85Z" fill="#f6faf8" stroke="#9aafb0"/><path d="M76 157h48v7H76Z" fill="#183d47"/><path d="M76 168h48v5H76Z" fill="#529c80"/><path d="M88 90h24v24q-12 10-24 0Z" fill="${skin}"/><rect x="75" y="37" width="50" height="60" rx="24" fill="${skin}"/><g fill="${hair}">${hairPaths[appearance.hairStyle] || ''}</g><g fill="${beard}">${beards[appearance.beardStyle] || ''}</g><path d="M72 213h56l-5 35-22-2-1-17-1 17-22 2Z" fill="#254f59"/><path d="m78 247 19-1-1 42H80M103 246l19 1-2 41h-16" fill="${skin}"/><path d="M79 271h18v34H78M103 271h18l2 34h-19" fill="#f3f7f4" stroke="#9aafb0"/><path d="M79 277h18v7H79M103 277h18v7h-18" fill="${socks}"/><path d="M78 302h18v14H64q-3-9 14-14M104 302h18q17 5 14 14h-32Z" fill="${boots}"/><path d="M87 61h5M108 61h5" stroke="#24333a" stroke-width="3" stroke-linecap="round"/></svg><figcaption>Prévia ilustrativa<br><span>Confira o resultado 3D no jogo.</span></figcaption></figure>`;
}
