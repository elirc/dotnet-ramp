// Thin fetch wrapper: attaches the bearer token, parses JSON, and surfaces
// ProblemDetails error messages from the API in a consistent shape.

const TOKEN_KEY = 'ems.token';

export const tokenStore = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (t) => localStorage.setItem(TOKEN_KEY, t),
  clear: () => localStorage.removeItem(TOKEN_KEY)
};

export class ApiError extends Error {
  constructor(message, status, details) {
    super(message);
    this.status = status;
    this.details = details || [];
  }
}

async function request(method, url, body, isForm = false) {
  const headers = {};
  const token = tokenStore.get();
  if (token) headers['Authorization'] = `Bearer ${token}`;

  let payload;
  if (isForm) {
    payload = body; // FormData; let the browser set the multipart boundary
  } else if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
    payload = JSON.stringify(body);
  }

  const res = await fetch(url, { method, headers, body: payload });

  if (res.status === 204) return null;

  const text = await res.text();
  const data = text ? JSON.parse(text) : null;

  if (!res.ok) {
    const message = data?.detail || data?.title || data?.message || `Request failed (${res.status})`;
    throw new ApiError(message, res.status, data?.details);
  }
  return data;
}

export const api = {
  get: (url) => request('GET', url),
  post: (url, body) => request('POST', url, body),
  put: (url, body) => request('PUT', url, body),
  del: (url) => request('DELETE', url),
  upload: (url, formData) => request('POST', url, formData, true)
};
