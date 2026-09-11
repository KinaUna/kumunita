# U7 — Web: `ProfileController.Avatar` (the serving-lane contract)

> Self-contained: this file + the **entry reads** below is the whole context.
> **This is the contract unit** — the exact C# + the FACES row mapping are in
> `docs/design/media-file-storage-design.md` §2.3 + §2.5 (FACES M1–M6). A
> prior handoff section (if any) is **U6**.

## Understanding

Add the **avatar serving action** — the **contract** every follow-on lane
(group logos / post attachments / badge icons) will copy: gated by
`Profile.ToAuditableResource()` via the **frozen** `CanAsync(…Read…)` seam
(C-MED·1 — **no** new `AccessAction` / `AccessVia`), **audit-by-default**
(C-MED·2), **owner branch included** (C-MED·1), `X-Content-Type-Options:
nosniff` + the stored `Content-Type` (C-MED·5). This is the **single decision
path** (C-MED·1) the whole serving feature runs through — the **most**
important unit in the series.

## Assumptions

- **No new `AccessAction` / `AccessVia`** (C-MED·1, §2.7 rule 4): the
  action reuses the **frozen** `IAuthorizationService.CanAsync(
  profileActor, Read, profile.ToAuditableResource(), …)` — the **exact**
  FACES row mapping is in the design doc §2.5.
- **Audit-by-default** (C-MED·2): the `CanAsync` seam commits an
  `AccessAudit` row (Allow **or** Deny) — the action does **not** re-implement
  the audit (a double-audit drift).
- **Owner branch** (C-MED·1): the `subject` is the **current user**
  (`SubjectId(User)`), the owner branch is the `Read` `AccessVia` — the
  action gates **self-only** (a `subject`-param drift is a **fail-closed**
  design-doc violation).
- **`X-Content-Type-Options: nosniff`** (C-MED·5, §2.7 rule 5): the action
  sets this header (a **fail-closed** design-doc violation if missing).

## Approach

Add the `Avatar` action (design doc §2.2/§2.3 shape): load `profile` →
`profile.AvatarId is null` → 404 → `CanAsync(profile, Read,
profile.ToAuditableResource(), actor)` → `decision.Allowed ? stream : 404`
→ `OpenRead` → set `Content-Type` + `X-Content-Type-Options: nosniff` →
`File(stream, contentType, fileDownloadName: null)`.

## Key files (entry reads — no more)

- `docs/design/media-file-storage-design.md` — §2.2 (the `Avatar` action
  shape, the `X-Content-Type-Options` + Content-Type) + §2.3 (the
  serving-lane rule, C-MED·1/2/3/5) + §2.5 (the FACES M1–M6 test names) +
  §2.7 rules 4/5 (the no-new-`AccessAction` / `nosniff` rules).
- `docs/plans-milestones/in-progress/media-u2-plan.md` + `media-u4-plan.md`
  + `media-u6-plan.md` + the **U2** + **U4** + **U6** handoff (the
  `IMediaStore` + `MediaObject` + `Profile.AvatarId` to read bytes back).
- `src/Kumunita.Web/Controllers/ProfileController.cs` — the
  `Profile.ToAuditableResource` + `IAuthorizationService.CanAsync` idiom
  **already** in the controller (the `Preview` / `Edit` idiom to mirror).
- `docs/SECURITY.md` — the (a)–(d) data-class model + the (e) media class
  (the C-MED·1/2/3/5 controls; the `nosniff` + audit notes).

## Deliverables (1 file, modify)

- `src/Kumunita.Web/Controllers/ProfileController.cs` — add the `Avatar`
  action (design doc §2.2/§2.3 shape: the C-MED·1/2/3/5 gate + the
  `X-Content-Type-Options: nosniff` + Content-Type), and the FACES-gate
  logic (allow / deny / owner / blocked / unknown) matching
  `DirectoryService.PreviewAsAsync`'s `CanAsync` idiom.

## Risks & open questions

- **`subject` self-only:** the action gates `SubjectId(User)` (the current
  user) — **never** a path param (C-MED·8 — the **serve** side of the
  C-MED·8 lane). A `subject`-param drift is a **fail-closed** design-doc
  violation (the **whole** serving lane re-copiable by future lanes).
- **`X-Content-Type-Options: nosniff`:** the action **must** set this header
  (C-MED·5, §2.7 rule 5). A missing `nosniff` is a **fail-closed** design-doc
  violation (a browser-MIME-sniffing drift).
- **Audit double-implementation:** the `CanAsync` seam already commits an
  `AccessAudit` row (C-MED·2) — the action does **not** re-implement the
  audit (a double-audit drift). The action's job is to **gate** + **stream**,
  not to audit.
- **`MediaObject` `Content-Type`:** the action streams the `MediaObject`
  (the doc, not the raw file) — a `MediaObject` read drifts the C-MED·7
  (the `mt` doc is the **catalog**; the **bytes** are the volume — the
  action bridges the two via `OpenRead`). A `MediaObject`-less action
  (streaming the raw file directly) drifts the C-MED·7.

## Steps

1. Add the `Avatar` action to `ProfileController` (the C-MED·1/2/3/5 gate:
   load profile → `AvatarId is null ? 404 : CanAsync(read) ? stream : 404`).
2. Set the `X-Content-Type-Options: nosniff` + `Content-Type` headers (the
   C-MED·5 / §2.7 rule 5 shape).
3. `run_build` → green on `Kumunita.Web`.
4. Append **`## U7`** to the handoff notes: the action signature (verbatim
   from the design doc §2.2), the `CanAsync` + `OpenRead` call order (the
   single decision path, C-MED·1), the `X-Content-Type-Options: nosniff` +
   Content-Type header set (C-MED·5), and the **FACES M1–M6 row mapping**
   (which row = which `Decision` branch — the contract every follow-on
   lane copies).
