/**
 * CSRF-aware fetch for Kumunita page modules (ARCHITECTURE.md §7).
 * Every mutating request carries the anti-forgery token rendered by the
 * layout as `<meta name="anti-forgery-token">` (wired in M1, together with
 * ASP.NET Identity). Page modules live one file per page (e.g. posts.ts).
 */

export function getAntiForgeryToken(): string {
  const meta = document.querySelector<HTMLMetaElement>(
    'meta[name="anti-forgery-token"]',
  );
  if (!meta?.content) {
    throw new Error('Anti-forgery token not found in page layout');
  }
  return meta.content;
}

export async function apiFetch<T = unknown>(
  url: string,
  init: RequestInit = {},
): Promise<T> {
  const headers = new Headers(init.headers);
  const method = (init.method ?? 'GET').toUpperCase();
  if (method !== 'GET' && method !== 'HEAD') {
    headers.set('RequestVerificationToken', getAntiForgeryToken());
  }

  const res = await fetch(url, { ...init, headers, credentials: 'same-origin' });
  if (!res.ok) {
    throw new Error(`Request to ${url} failed: ${res.status} ${res.statusText}`);
  }
  return (res.status === 204 ? undefined : await res.json()) as T;
}
