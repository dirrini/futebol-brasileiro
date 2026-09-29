import { escapeHtml as e, field, select } from './ui.js';
export const section = (title, hint = '') => `<div class="section-label section-separated"><h3>${e(title)}</h3>${hint ? `<p>${e(hint)}</p>` : ''}</div>`;
export const options = items => items.map(([value, label]) => ({ value: String(value), label }));
export function portable(html, path, mode = 'text', optional = false, refresh = false) {
  return html.replace(/<(input|select) /, `<$1 data-portable-path="${e(path)}" data-value-mode="${mode}"${optional ? ' data-optional="true"' : ''}${refresh ? ' data-refresh="true"' : ''} `);
}
export function pField(root, path, label, value, extra = {}) {
  const { optional, mode, refresh, ...rest } = extra;
  return portable(field({ id: 'portable-' + (root + '.' + path).replace(/[^a-z0-9]/gi, '-'), label, value, path: `${root}.${path}`, ...rest }), path, mode || (extra.type === 'number' ? 'number' : 'text'), optional, refresh);
}
export function pSelect(root, path, label, value, list, extra = {}) {
  return portable(select({ id: 'portable-' + (root + '.' + path).replace(/[^a-z0-9]/gi, '-'), label, value: value === null || value === undefined ? '' : String(value), path: `${root}.${path}`, options: list, hint: extra.hint || '' }), path, extra.mode || 'text', extra.optional, extra.refresh);
}
export function multi(root, path, label, values, list, hint = '') {
  const id = 'multi-' + (root + '.' + path).replace(/[^a-z0-9]/gi, '-');
  return `<fieldset class="participant-field" id="${id}" data-path="${e(root + '.' + path)}" tabindex="-1" aria-describedby="${id}-hint ${id}-error"><legend>${e(label)}</legend><div class="participant-options">${list.map(item => `<label class="participant-option"><input type="checkbox" data-portable-array="${e(path)}" value="${e(item.value)}"${values.includes(item.value) ? ' checked' : ''}><span>${e(item.label)}</span></label>`).join('')}</div><small id="${id}-hint" class="field-hint">${e(hint)}</small><small id="${id}-error" class="field-error"></small></fieldset>`;
}
export const action = (name, label, extra = '') => `<button type="button" class="button button-outline" data-declarative-action="${name}" ${extra}>${e(label)}</button>`;
export const footer = (record, label) => `<div class="detail-footer"><details class="record-id"><summary>Identificador permanente</summary><code>${e(record.id)}</code><p>Renomear preserva os vínculos e os saves existentes.</p></details><button class="button button-ghost-danger" id="delete-record">Excluir ${e(label)}</button></div>`;
