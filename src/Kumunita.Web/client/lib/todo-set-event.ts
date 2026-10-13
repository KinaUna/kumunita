/// <summary>
/// ADR 0115 D3 (M14 interlock) — the "create a new event" affordance
/// on the to-do detail's event picker (the set-event select's
/// "Create a new event…" sentinel option → the Bootstrap modal).
///
/// A single delegated listener on the set-event form intercepts its
/// submit when the sentinel value `__new__` is selected, and instead
/// opens the Bootstrap modal. When the modal is closed without
/// submitting (Cancel or backdrop click), the select is reset to
/// blank so a subsequent "Link to event" submit doesn't POST the
/// sentinel.
///
/// The Bootstrap Modal API (window.bootstrap.Modal) is loaded globally
/// by _Layout.cshtml (bootstrap.bundle.min.js). The modal's own form
/// (a different form, posting to the create-event route) submits on
/// its own; only the set-event form's state is touched on a *closed*
/// modal.
///
/// The ADR 0031 tsc-only discipline: this module lives in client/lib/
/// and is compiled to wwwroot/js/lib/ by `tsc` — never an inline
/// script (CSP's `script-src 'self'` forbids inline scripts).
/// </summary>
(() => {
  const setEventForm = document.getElementById("set-event-form");
  const setEventSelect = document.getElementById("set-event-select") as HTMLSelectElement | null;
  const createEventModalEl = document.getElementById("create-event-modal");

  // Guard: the page may not have the picker card (EventPicker is null)
  // or the Bootstrap bundle may not be loaded (a test construction or
  // a CSP-disabled environment). In either case the sentinel option
  // is not present, so the module is a no-op.
  if (!setEventForm || !setEventSelect || !createEventModalEl) return;
  // @ts-expect-error — bootstrap.bundle exposes a global `bootstrap`
  // (the UMD build loaded before this module in _Layout.cshtml).
  const Modal = window.bootstrap?.Modal;
  if (!Modal) return; // bootstrap.bundle failed to load; the sentinel
                      // option simply won't open a modal (the form
                      // POSTs the sentinel as the eventId — a no-op in
                      // the server's set-event lane, the degraded path).

  const SENTINEL = "__new__";

  // Intercept the set-event form's submit: when the sentinel is
  // selected, prevent the form's native POST (which would POST the
  // sentinel as the eventId — a no-op in the server's set-event lane,
  // but the wrong affordance) and open the create-event modal
  // instead.
  setEventForm.addEventListener("submit", (e) => {
    if (setEventSelect.value === SENTINEL) {
      e.preventDefault();
      Modal.getOrCreateInstance(createEventModalEl).show();
    }
  });

  // When the modal is hidden without submitting (the Cancel button or
  // a backdrop click), reset the set-event form's select to blank so
  // a subsequent "Link to event" submit doesn't POST the sentinel.
  // (The modal's own form POSTs to the create-event route on submit —
  // a different form, a different action — so the set-event form's
  // state is only touched on a *closed* modal.)
  createEventModalEl.addEventListener("hidden.bs.modal", () => {
    setEventSelect.value = "";
  });
})();
