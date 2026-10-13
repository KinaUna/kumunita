namespace Kumunita.Web.Models;

/// <summary>
/// One step in an onboarding walk-through — the <b>shared</b> shape the M22
/// resident walk-through (<c>/onboarding</c>, ADR 0132) and the M30 admin
/// walk-through (<c>/admin/onboarding</c>, ADR 0153) pass to the shared
/// <c>_OnboardingModal</c> partial (the "open the setting in a modal + previous
/// / next navigation" rework).
/// <para>
/// A <b>record</b> (structural equality, value semantics). The fields mirror
/// the admin <see cref="AdminOnboardingViewModel.Step"/> record (the M30·7
/// closed set already carried exactly these four) so the two walks' step
/// lists can be rendered through the same modal without one re-shaping the
/// other. The resident walk's per-step "set / not set" hint (the owner-scope
/// <c>Has*</c> read, <see cref="OnboardingViewModel"/>) is <b>not</b> carried
/// here — it is resident-scope only and stays on the resident view model; the
/// shared step is the <i>pointer into the surface that owns the setting</i>
/// (the M22 D3 / M30·7 "rides frozen lanes, never re-implements a write" pin —
/// the walk-through links into the surface, it does not re-route or re-author
/// it).
/// </para>
/// </summary>
/// <param name="Key">
/// The step's stable identity — the value each step's
/// <c>data-ob-step</c> button and the <c>_OnboardingModal</c> step list agree
/// on (e.g. <c>displayname</c> / <c>timezone</c> on the resident walk;
/// <c>communityname</c> / <c>languages</c> / … on the admin walk). Not a
/// <c>kw-l</c> key — it is the <see cref="LabelKey"/> /
/// <see cref="DescriptionKey"/> that carry the <c>onboarding.*</c> /
/// <c>adminonboarding.*</c> keys.
/// </param>
/// <param name="LabelKey">
/// The <c>*.step_*</c> <c>kw-l</c> key for the step's title (the closed-set
/// parity-pinned in four languages, the M22 D7 / M30·6 shape).
/// </param>
/// <param name="Route">
/// The existing surface that already owns the setting (the "rides frozen
/// lanes" pin, resident- / admin-scope). The modal iframes this route (the
/// surface's own GET — which owns its form, anti-forgery token, and save
/// lane) and the previous / next navigation walks the step list by index.
/// </param>
/// <param name="DescriptionKey">
/// The <c>*.desc_*</c> <c>kw-l</c> key for the step's one-line description
/// (admin-scope; the resident walk leaves it empty — its steps carry no
/// description, so the view renders the label only when it is blank).
/// </param>
public sealed record OnboardingStep(
    string Key,
    string LabelKey,
    string Route,
    string DescriptionKey);
