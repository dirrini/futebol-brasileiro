const base = new URL('./api/', import.meta.url);

export class ApiError extends Error {
  constructor(message, status = 0, details = {}) {
    super(message);
    this.status = status;
    this.code = details.code;
    this.issues = details.issues || [];
  }
}

async function request(path, options = {}) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 30000);
  try {
    const response = await fetch(new URL(path, base), {
      cache: 'no-store',
      ...options,
      signal: controller.signal,
      headers: { Accept: 'application/json', ...options.headers },
    });
    const result = await response.json().catch(() => null);
    if (!response.ok) {
      const details = result?.error || {};
      throw new ApiError(details.message || 'Não foi possível concluir a operação. Tente novamente.', response.status, details);
    }
    if (!result) throw new ApiError('O servidor devolveu uma resposta inválida. Tente carregar a base novamente.');
    return result;
  } catch (error) {
    if (error instanceof ApiError) throw error;
    if (error.name === 'AbortError') throw new ApiError('O servidor demorou para responder. Seu rascunho foi mantido; verifique a conexão antes de tentar novamente.');
    throw new ApiError('Não foi possível acessar o editor. Verifique se o Docker está em execução e tente novamente.');
  } finally {
    clearTimeout(timer);
  }
}

export const getDatabase = () => request('database');
export const getOptions = () => request('options');
export const saveDatabase = (document, etag) => request('database', {
  method: 'PUT',
  headers: { 'Content-Type': 'application/json', 'If-Match': etag },
  body: JSON.stringify(document),
});
