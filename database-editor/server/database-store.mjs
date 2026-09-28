import { readFile, open, rename, mkdir, unlink } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { createHash, randomUUID } from 'node:crypto';
import { EditorError } from './errors.mjs';
import { MAX_BYTES, parseDocument } from './strict-json.mjs';

const hash = data => '"' + createHash('sha256').update(data).digest('hex') + '"';
const decode = data => {
  try { return new TextDecoder('utf-8', { fatal: true }).decode(data); }
  catch { throw new EditorError(422, 'invalid_utf8', 'O arquivo deve usar UTF-8 válido.'); }
};

export class DatabaseStore {
  #queue = Promise.resolve();
  constructor(path, validate) { this.path = path; this.validate = validate; }
  async read() {
    const raw = await readFile(this.path);
    const document = this.validate(parseDocument(decode(raw)));
    return { document, etag: hash(raw) };
  }
  save(document, expected) {
    // Serialize commits in this process; concurrent browser saves cannot both win.
    const operation = this.#queue.then(() => this.#save(document, expected));
    this.#queue = operation.catch(() => {});
    return operation;
  }
  async #save(document, expected) {
    if (!expected) throw new EditorError(428, 'precondition_required', 'Recarregue a base antes de salvar.');
    this.validate(document);
    const original = await readFile(this.path);
    if (hash(original) !== expected) throw new EditorError(409, 'conflict', 'A base mudou fora desta aba. Exporte seu rascunho ou recarregue antes de tentar novamente.');
    const current = this.validate(parseDocument(decode(original)));
    if (document.databaseId !== current.databaseId) throw new EditorError(422, 'immutable_id', 'A identidade da base não pode ser alterada.');
    if (document.databaseRevision !== current.databaseRevision) throw new EditorError(409, 'conflict', 'A revisão do rascunho está desatualizada. Recarregue a base.');
    if (current.databaseRevision >= 2147483647) throw new EditorError(422, 'revision_limit', 'A base atingiu o limite de revisões.');
    const next = { ...document, databaseRevision: current.databaseRevision + 1 };
    const body = Buffer.from(JSON.stringify(next, null, 2) + '\n');
    if (body.length > MAX_BYTES) throw new EditorError(422, 'document_too_large', 'A base formatada deve ter no máximo 1 MiB.');
    const directory = dirname(this.path);
    const temporary = join(directory, '.editor-' + randomUUID() + '.tmp');
    const backupDir = join(directory, '.editor-backups');
    await mkdir(backupDir, { recursive: true });
    try {
      const file = await open(temporary, 'wx');
      try { await file.writeFile(body); await file.sync(); } finally { await file.close(); }
      // A hidden host backup keeps the previous successful content available.
      const backup = await open(join(backupDir, 'previous.database.json'), 'w');
      try { await backup.writeFile(original); await backup.sync(); } finally { await backup.close(); }
      if (hash(await readFile(this.path)) !== expected) throw new EditorError(409, 'conflict', 'O arquivo mudou durante o salvamento. O rascunho foi preservado nesta aba.');
      await rename(temporary, this.path);
      return { document: next, etag: hash(body) };
    } finally { await unlink(temporary).catch(error => { if (error.code !== 'ENOENT') throw error; }); }
  }
}
