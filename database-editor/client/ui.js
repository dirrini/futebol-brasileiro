export const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
export const $ = selector => document.querySelector(selector);
export const announce = message => { $('#announcer').textContent = message; };

const editAttributes = ({ path, property, optional, sourceId }) => `${path ? ` data-path="${escapeHtml(path)}"` : ''}${property ? ` data-property="${escapeHtml(property)}"` : ''}${optional ? ' data-optional="true"' : ''}${sourceId ? ` data-source-id="${escapeHtml(sourceId)}"` : ''}`;

export function field({ id, label, value, type = 'text', min, max, maxLength, suffix, path, property, optional = false, sourceId, hint = '', readOnly = false }) {
  return `<div class="field"><label for="${id}">${escapeHtml(label)}</label><div class="input-wrap"><input id="${id}" name="${id}" type="${type}" value="${escapeHtml(value)}"${min !== undefined ? ` min="${min}"` : ''}${max !== undefined ? ` max="${max}"` : ''}${maxLength ? ` maxlength="${maxLength}"` : ''}${type === 'number' ? ' step="1" inputmode="numeric"' : ''}${readOnly ? ' readonly' : ''}${editAttributes({ path, property, optional, sourceId })} aria-describedby="${id}-hint ${id}-error">${suffix ? `<span class="input-suffix">${escapeHtml(suffix)}</span>` : ''}</div><small id="${id}-hint" class="field-hint">${escapeHtml(hint)}</small><small id="${id}-error" class="field-error"></small></div>`;
}

export function select({ id, label, value, options, path, property, optional = false, hint = '' }) {
  return `<div class="field"><label for="${id}">${escapeHtml(label)}</label><select id="${id}" name="${id}"${editAttributes({ path, property, optional })} aria-describedby="${id}-hint ${id}-error">${options.map(option => `<option value="${escapeHtml(option.value)}"${option.value === value ? ' selected' : ''}>${escapeHtml(option.label)}</option>`).join('')}</select><small id="${id}-hint" class="field-hint">${escapeHtml(hint)}</small><small id="${id}-error" class="field-error"></small></div>`;
}

export function textarea({ id, label, value, path, property, optional = false, hint = '', maxLength = 4000 }) {
  return `<div class="field field-notes"><label for="${id}">${escapeHtml(label)}</label><textarea id="${id}" name="${id}" rows="5" maxlength="${maxLength}"${editAttributes({ path, property, optional })} aria-describedby="${id}-hint ${id}-error">${escapeHtml(value)}</textarea><small id="${id}-hint" class="field-hint">${escapeHtml(hint)}</small><small id="${id}-error" class="field-error"></small></div>`;
}

export function applyFieldErrors(issues) {
  document.querySelectorAll('[data-path]').forEach(input => {
    const match = issues.find(issue => issue.path.replace(/^\$\.?/, '') === input.dataset.path);
    input.setAttribute('aria-invalid', match ? 'true' : 'false');
    const error = document.getElementById(`${input.id}-error`);
    if (error) error.textContent = match?.message || '';
  });
}

export function downloadJson(value) {
  const url = URL.createObjectURL(new Blob([JSON.stringify(value, null, 2) + '\n'], { type: 'application/json;charset=utf-8' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = `futebol-brasileiro-r${value.databaseRevision}.json`;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export function applyDialogError(dialog, error) {
  dialog.querySelectorAll('[aria-invalid="true"]').forEach(input => input.setAttribute('aria-invalid', 'false'));
  dialog.querySelectorAll('.field-error').forEach(message => { message.textContent = ''; });
  if (!error) return;
  const summary = dialog.querySelector('#dialog-error');
  if (typeof error === 'string') { summary.textContent = error; return; }
  const input = dialog.querySelector(`#${error.field}`);
  const message = dialog.querySelector(`#${error.field}-error`);
  if (input && message) {
    input.setAttribute('aria-invalid', 'true');
    message.textContent = error.message;
    summary.textContent = 'Revise o campo indicado antes de continuar.';
    input.focus();
  } else summary.textContent = error.message;
}

export function showDialog({ title, description, content = '', action, danger = false, onAccept }) {
  const dialog = $('#app-dialog');
  const previous = document.activeElement;
  dialog.innerHTML = `<form method="dialog" novalidate><p class="eyebrow">EDITOR DE BASE</p><h2 id="dialog-title">${escapeHtml(title)}</h2><p id="dialog-description">${escapeHtml(description)}</p>${content}<p id="dialog-error" class="field-error" role="alert"></p><div class="dialog-actions"><button type="button" class="button button-outline" id="dialog-cancel" autofocus>Cancelar</button><button type="submit" class="button ${danger ? 'button-danger' : 'button-primary'}" id="dialog-accept">${escapeHtml(action)}</button></div></form>`;
  dialog.addEventListener('close', () => { if (previous?.isConnected) previous.focus(); }, { once: true });
  dialog.querySelector('#dialog-cancel').addEventListener('click', () => dialog.close());
  dialog.querySelector('form').addEventListener('submit', event => {
    event.preventDefault();
    applyDialogError(dialog, null);
    const error = onAccept(dialog);
    if (error) { applyDialogError(dialog, error); return; }
    dialog.close();
  });
  dialog.showModal();
  if (!danger) dialog.querySelector('input')?.focus();
}
