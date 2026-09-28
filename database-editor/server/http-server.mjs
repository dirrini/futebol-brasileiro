import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { resolve, sep, extname } from 'node:path';
import { EditorError } from './errors.mjs';
import { MAX_BYTES, parseDocument } from './strict-json.mjs';

const mime = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.svg': 'image/svg+xml', '.json': 'application/json; charset=utf-8' };

export function createEditorServer({ store, options, schema, clientRoot, allowedOrigins }) {
  const origins = new Set(allowedOrigins);
  const send = (response, status, value, headers = {}) => {
    response.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', ...headers });
    response.end(JSON.stringify(value));
  };
  const server = createServer(async (request, response) => {
    response.setHeader('Cache-Control', 'no-store');
    response.setHeader('X-Content-Type-Options', 'nosniff');
    response.setHeader('Referrer-Policy', 'same-origin');
    response.setHeader('Content-Security-Policy', "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; font-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'");
    try {
      const url = new URL(request.url, 'http://editor.internal');
      const path = url.pathname.replace(/^\/editor(?=\/|$)/, '') || '/';
      if (path === '/health' && request.method === 'GET') { send(response, 200, { status: 'ok' }); return; }
      if (path === '/api/database' && request.method === 'GET') {
        const result = await store.read();
        send(response, 200, result, { ETag: result.etag }); return;
      }
      if (path === '/api/options' && request.method === 'GET') { send(response, 200, options); return; }
      if (path === '/api/schema' && request.method === 'GET') { send(response, 200, schema); return; }
      if (path === '/api/database' && request.method === 'PUT') {
        if (!origins.has(request.headers.origin)) throw new EditorError(403, 'origin_rejected', 'Origem de edição não autorizada. Abra o editor pelo endereço local do projeto.');
        if (!/^application\/json(?:\s*;|$)/i.test(request.headers['content-type'] ?? '')) throw new EditorError(415, 'content_type', 'Envie a base como application/json.');
        const chunks = [];
        let size = 0;
        for await (const chunk of request) {
          size += chunk.length;
          if (size > MAX_BYTES) throw new EditorError(413, 'document_too_large', 'A base deve ter no máximo 1 MiB.');
          chunks.push(chunk);
        }
        let text;
        try { text = new TextDecoder('utf-8', { fatal: true }).decode(Buffer.concat(chunks)); }
        catch { throw new EditorError(422, 'invalid_utf8', 'A base deve usar UTF-8 válido.'); }
        const result = await store.save(parseDocument(text), request.headers['if-match']);
        send(response, 200, result, { ETag: result.etag }); return;
      }
      if (path.startsWith('/api/')) throw new EditorError(405, 'method_not_allowed', 'Operação não disponível.');
      if (!['GET', 'HEAD'].includes(request.method)) throw new EditorError(405, 'method_not_allowed', 'Operação não disponível.');
      let relative;
      try { relative = decodeURIComponent(path); } catch { throw new EditorError(400, 'invalid_path', 'Endereço inválido.'); }
      if (relative.includes('\0') || relative.includes('\\') || relative.split('/').some(part => part.startsWith('.')))
        throw new EditorError(404, 'not_found', 'Arquivo não encontrado.');
      const file = resolve(clientRoot, '.' + (relative === '/' ? '/index.html' : relative));
      if (!file.startsWith(resolve(clientRoot) + sep) || !mime[extname(file)]) throw new EditorError(404, 'not_found', 'Arquivo não encontrado.');
      const content = await readFile(file);
      response.writeHead(200, { 'Content-Type': mime[extname(file)] });
      response.end(request.method === 'HEAD' ? undefined : content);
    } catch (error) {
      if (response.headersSent) { response.end(); return; }
      if (error instanceof EditorError) {
        send(response, error.status, { error: { code: error.code, message: error.message, issues: error.issues } });
      } else if (error.code === 'ENOENT') {
        send(response, 404, { error: { code: 'not_found', message: 'Arquivo não encontrado. Confira a base e a configuração do serviço.' } });
      } else {
        console.error('Editor request failed:', error.code ?? error.name, error.message);
        send(response, 500, { error: { code: 'server_error', message: 'Não foi possível concluir a operação. Seu rascunho permanece nesta aba.' } });
      }
    }
  });
  server.requestTimeout = 15000;
  server.headersTimeout = 10000;
  return server;
}
