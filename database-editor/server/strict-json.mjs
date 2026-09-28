import { EditorError } from './errors.mjs';

export const MAX_BYTES = 1024 * 1024;

// Keep the portable import boundary: JSON.parse alone accepts duplicate keys,
// unpaired surrogates and numeric spellings rejected by the Unity importer.
export function parseDocument(text) {
  const fail = message => { throw new EditorError(422, 'invalid_json', message); };
  if (Buffer.byteLength(text, 'utf8') > MAX_BYTES) fail('A base deve ter no máximo 1 MiB.');
  let at = 0;
  const whitespace = () => { while (/[\x20\t\r\n]/.test(text[at] ?? '') && at < text.length) at++; };
  function string() {
    const start = at++;
    while (at < text.length) {
      const char = text[at++];
      if (char === '\\') { at++; continue; }
      if (char === '"') {
        let value;
        try { value = JSON.parse(text.slice(start, at)); } catch { fail('String JSON inválida.'); }
        for (let i = 0; i < value.length; i++) {
          const code = value.charCodeAt(i);
          if (code >= 0xd800 && code <= 0xdbff) {
            const next = value.charCodeAt(++i);
            if (!(next >= 0xdc00 && next <= 0xdfff)) fail('Unicode inválido.');
          } else if (code >= 0xdc00 && code <= 0xdfff) fail('Unicode inválido.');
        }
        return value;
      }
    }
    fail('String JSON não terminada.');
  }
  function value(depth) {
    whitespace();
    if (depth > 32) fail('A base ultrapassa a profundidade máxima de 32 níveis.');
    if (text[at] === '"') { string(); return; }
    if (text[at] === '{' || text[at] === '[') {
      if (depth >= 32) fail('A base ultrapassa a profundidade máxima de 32 níveis.');
      const object = text[at++] === '{';
      const end = object ? '}' : ']';
      const keys = new Set();
      whitespace();
      if (text[at] === end) { at++; return; }
      while (at < text.length) {
        if (object) {
          whitespace();
          if (text[at] !== '"') fail('Uma propriedade deve estar entre aspas duplas.');
          const key = string();
          if (keys.has(key)) fail(`Propriedade repetida: ${key}.`);
          keys.add(key);
          whitespace();
          if (text[at++] !== ':') fail('Separador de propriedade inválido.');
        }
        value(depth + 1);
        whitespace();
        if (text[at] === end) { at++; return; }
        if (text[at++] !== ',') fail('Separador JSON inválido.');
      }
      fail('Objeto ou lista JSON não terminado.');
    }
    const token = /^(?:true|false|null|-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)/.exec(text.slice(at));
    if (!token) fail('Conteúdo JSON inválido.');
    if (/^[\d-]/.test(token[0]) && /[.eE]/.test(token[0])) fail('Use números inteiros, sem decimais ou expoentes.');
    at += token[0].length;
  }
  value(0);
  whitespace();
  if (at !== text.length) fail('Conteúdo adicional após o JSON.');
  try { return JSON.parse(text); } catch { fail('Conteúdo JSON inválido.'); }
}
