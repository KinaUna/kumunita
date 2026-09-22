/**
 * Tag-suggest (TG, U8) — the composer's tag-input autocomplete.
 *
 * A `tsc`-only ES module (the `client/lib` idiom, RE·3: **no editor /
 * framework dependency** — `package.json` stays `typescript`-only) that binds
 * the composer's free-text tag input to a dropdown of existing tags.
 *
 * **The filter is server-side (C-TG·2 / C-TG·4):** the typed prefix is POSTed
 * to `/api/tags/suggest` (the `SuggestAsync` seam), which applies
 * `starts_with(displayName, prefix) OR starts_with(slug, prefix)` over the
 * actor-readable tag set and caps at 10 (F10). This module only **renders the
 * dropdown and commits the picked / free-typed tag** — it does no tag
 * filtering of its own (the server is the single source of the match set, so
 * the privacy-pin, C-TG·1 / C-TG·3, holds by construction: a tag behind
 * unread content never reaches this module).
 *
 * **CSRF-aware (ADR 0015 §7):** the suggest POST goes through
 * `apiFetch` (`client/lib/api.ts`), which attaches the
 * `RequestVerificationToken` header from the layout's
 * `<meta name="anti-forgery-token">`. No token is read or echoed by this
 * module.
 *
 * **Self-wires at load** (the `insert-image.ts` / `rich-editor.ts`
 * convention): a Razor view only needs the
 * `<script type="module" src="~/js/lib/tag-suggest.js"></script>` line; the
 * self-wire is guarded by `typeof document !== 'undefined'` so the pure bits
 * stay importable in a non-DOM test environment.
 *
 * **Submission shape (the U8 pin):** each committed tag (picked from the
 * dropdown, or free-typed) is written to a hidden
 * `<input type="hidden" name="TagIds">` carrying a **JSON array of the tag
 * labels the author typed**. The `Slug` is derived **server-side** by the
 * write seam (`TagService` `Slug` derivation — lowercase + trim + charset,
 * C-TG·4); the client submits the author's text, never a pre-derivation. This
 * is "typed tags are submitted as the `TagIds` set".
 *
 * **Frozen base (unchanged):** `api.ts`, the `POST /api/tags/suggest`
 * endpoint, the `TagItem` wire projection. This module adds no re-shape of
 * any of them.
 */
import { apiFetch } from './api.js';

// ── Suggest wire shape (the `POST /api/tags/suggest` JSON projection) ──────

/**
 * The `TagItem` row (design doc §2.1), as serialized by the controller
 * (ASP.NET Core's default camelCase `System.Text.Json` policy). Only the two
 * fields this module reads are named — `displayedName` (the label to show and
 * commit) and `tag.slug` (the business key, C-TG·4, used as a fallback label
 * when a suggestion carries no display name).
 */
interface TagSuggestion {
  tag: { slug: string };
  useCount?: number;
  displayedName: string;
}

/** The `TagSuggestViewModel` body (the `suggestions` list, ≤ 10 — F10). */
interface SuggestResponse {
  suggestions: TagSuggestion[];
}

const SUGGEST_ENDPOINT = '/api/tags/suggest';
const DEBOUNCE_MS = 120;

/**
 * Bind one tag-input surface. The argument must be an `<input>` carrying
 * `data-tag-suggest`; this module creates the chip list (committed tags), the
 * dropdown (live suggestions), and the hidden `TagIds` submission field
 * around it. Idempotent per input: a second call on the same node is guarded
 * by a module-flag attribute so the self-wire and a manual bind never double
 * wire.
 */
export function bindTagSuggest(input: HTMLInputElement): void {
  if (input.dataset.tagSuggestBound === 'true') return;
  input.dataset.tagSuggestBound = 'true';

  // 1. Wrap the input so the dropdown can anchor to a `position:relative` box
  //    (the input's Bootstrap `.card-body` is `static`; anchoring to it would
  //    let the dropdown escape the field).
  const wrap = document.createElement('div');
  wrap.className = 'kw-tag-field';
  wrap.style.position = 'relative';
  input.insertAdjacentElement('beforebegin', wrap);
  wrap.appendChild(input);

  // 2. The committed-tag chips render before the input.
  const chips = document.createElement('div');
  chips.className = 'kw-tag-chips';
  chips.style.display = 'flex';
  chips.style.flexWrap = 'wrap';
  chips.style.gap = '0.25rem';
  input.insertAdjacentElement('beforebegin', chips);

  // 3. The dropdown renders after the input, anchored to the wrap.
  const dropdown = document.createElement('div');
  dropdown.className = 'kw-tag-suggest';
  dropdown.setAttribute('role', 'listbox');
  dropdown.style.cssText =
    'position:absolute; top:100%; left:0; right:0; z-index:1050; ' +
    'background:#fff; border:1px solid #dee2e6; border-radius:0.375rem; ' +
    'margin-top:2px; max-height:220px; overflow:auto; ' +
    'box-shadow:0 4px 8px rgba(0,0,0,0.1); display:none;';
  wrap.appendChild(dropdown);

  // 4. The hidden submission field — the `TagIds` set (a JSON array of the
  //    committed labels; the server derives the `Slug` per label, C-TG·4).
  const hidden = document.createElement('input');
  hidden.type = 'hidden';
  hidden.name = 'TagIds';
  hidden.value = '[]';
  wrap.appendChild(hidden);

  const selected = new Set<string>(); // committed labels (dedup by label)

  // ADR 0044 (TG lane) — the edit lane pre-seeds the post's existing tag
  // slugs as starting chips (each removable; re-saving with fewer chips
  // detaches, the server-side empty-set detach). The attribute carries a JSON
  // array of charset-safe slugs (lowercase letters/digits/hyphen/underscore —
  // never a translated display name, which would throw in the server's
  // Slug derivation on re-save). A new/untagged post has "[]" or no attribute
  // ⇒ nothing to seed (the existing free-text behavior is unchanged).
  // Malformed / missing JSON degrades to no seed (try/catch) — the input still
  // works as before; this is a display affordance, never a gate.
  const initialRaw = input.dataset.tagSuggestInitial;
  if (initialRaw) {
    try {
      const initial = JSON.parse(initialRaw);
      if (Array.isArray(initial)) {
        for (const slug of initial) {
          const s = typeof slug === 'string' ? slug.trim() : '';
          if (s && !selected.has(s)) selected.add(s);
        }
      }
    } catch {
      // ignore — a broken seed attribute is a display no-op, not an error
    }
  }

  function syncHidden() {
    hidden.value = JSON.stringify(Array.from(selected));
  }

  function renderChips() {
    chips.innerHTML = '';
    for (const label of selected) {
      const chip = document.createElement('span');
      chip.className = 'kw-tag-chip badge bg-secondary';
      chip.textContent = label;
      const x = document.createElement('button');
      x.type = 'button';
      x.className = 'btn-close btn-close-white ms-1';
      x.setAttribute('aria-label', 'Remove tag: ' + label);
      x.addEventListener('click', () => {
        selected.delete(label);
        syncHidden();
        renderChips();
      });
      chip.appendChild(x);
      chips.appendChild(chip);
    }
    chips.style.marginBottom = selected.size ? '0.375rem' : '0';
  }

  function commit(label: string) {
    const t = label.trim();
    if (!t || selected.has(t)) return;
    selected.add(t);
    syncHidden();
    renderChips();
  }

  function close() {
    dropdown.style.display = 'none';
    dropdown.innerHTML = '';
  }

  function render(suggestions: TagSuggestion[]) {
    dropdown.innerHTML = '';
    if (suggestions.length === 0) {
      close();
      return;
    }
    for (const s of suggestions) {
      const label = (s.displayedName || s.tag.slug).trim();
      if (!label) continue;
      const item = document.createElement('div');
      item.className = 'kw-tag-suggest-item';
      item.setAttribute('role', 'option');
      item.textContent = label;
      item.style.cssText = 'padding:0.375rem 0.75rem; cursor:pointer;';
      item.addEventListener('mouseenter', () => {
        item.style.background = '#f1f3f5';
      });
      item.addEventListener('mouseleave', () => {
        item.style.background = '';
      });
      item.addEventListener('mousedown', (e) => {
        e.preventDefault(); // keep focus on the input (no premature blur-close)
        commit(label);
        input.value = '';
        close();
      });
      dropdown.appendChild(item);
    }
    dropdown.style.display = 'block';
  }

  let timer: number | undefined;
  input.addEventListener('input', () => {
    const prefix = input.value.trim();
    if (timer !== undefined) window.clearTimeout(timer);
    if (prefix.length === 0) {
      close();
      return;
    }
    // Debounce; the server applies the filter + cap (C-TG·2, F10).
    timer = window.setTimeout(() => {
      void (async () => {
        try {
          const res = await apiFetch<SuggestResponse>(SUGGEST_ENDPOINT, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ prefix }),
          });
          // Stale-guard: only render if the input still holds this prefix
          // (a fast typer has since moved on — the latest call wins).
          if (input.value.trim() === prefix) {
            render(res.suggestions ?? []);
          }
        } catch {
          close(); // endpoint error / 4xx — degrade to a plain free-text input
        }
      })();
    }, DEBOUNCE_MS);
  });

  // Enter / comma commit the free-typed tag; Escape closes the dropdown.
  input.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' || e.key === ',') {
      e.preventDefault();
      if (input.value.trim().length > 0) {
        commit(input.value.trim());
        input.value = '';
      }
      close();
    } else if (e.key === 'Escape') {
      close();
    }
  });

  // A short delay lets a dropdown `mousedown` (which `preventDefault`s the
  // blur) commit before the input loses focus and we close.
  input.addEventListener('blur', () => {
    window.setTimeout(close, 150);
  });

  // Seed the hidden field + render any pre-seeded chips (the edit lane's
  // existing tags, ADR 0044); a new/untagged post seeds nothing.
  syncHidden();
  renderChips();
}

// ── Self-wire at module load (the client/lib convention) ───────────────────
// Guarded by `typeof document !== 'undefined'` so the module stays importable
// in a non-DOM test environment (the `rich-editor.ts` idiom). A view without
// a `data-tag-suggest` input (e.g. the `PageKind.System` composer, C-TG·6)
// simply matches nothing — a no-op, never a crash.
if (typeof document !== 'undefined') {
  for (const el of Array.from(
    document.querySelectorAll<HTMLInputElement>('input[data-tag-suggest]'),
  )) {
    bindTagSuggest(el);
  }
}
