namespace Kumunita.Web.Models;

/// <summary>
/// The view shape behind the shared "Who to grant to" grant-picker
/// section rendered by <c>Views/Shared/_GrantPickers.cshtml</c>. One
/// instance per audience editor on a page: <c>Profile/Edit</c> invokes
/// the picker twice (the <c>Visibility</c> and <c>ContactVisibility</c>
/// editors inside <c>_AudienceEditor.cshtml</c>), and the M3 post
/// composer (<c>Posts/New</c>) invokes it once (the <c>Audience</c>
/// editor on <see cref="PostComposeViewModel"/>). Extracted from
/// <c>Views/Profile/_AudienceEditor.cshtml</c> (the M2 U11 editor's
/// checkbox-picker + hidden-<c>Grants</c>-textarea section, moved
/// verbatim) so the composer gets the *same* user-friendly picker M2
/// shipped, with the M2 pins intact:
/// <list type="bullet">
/// <item><see cref="Editor"/> is the editor's <see
/// cref="AudienceEditorModel"/> — its <see
/// cref="AudienceEditorModel.ParsedGrants"/> read-view pre-checks the
/// boxes, and its <c>Grants</c> JSON string is what the hidden textarea
/// posts. The partial is a *view* over the transport, not a second
/// audience object (the M2 U11 / F13 single-source pin: the hidden
/// textarea is still the <b>only</b> form-bound grants field, and
/// <see cref="AudienceEditorModel.BuildAudience"/> remains the only
/// deserialization site).</item>
/// <item><c>Users</c> / <c>Groups</c> are the <see cref="GrantOption"/>
/// option lists (the M2 editor's UX layer) the controller seeds on
/// <c>ViewData</c> ("<c>{EditorName}_Users</c>" /
/// "<c>{EditorName}_Groups</c>") — read-only view data, never a model
/// property (the M2 U11 "exactly six form fields" pin on
/// <see cref="ProfileEditViewModel"/> applies verbatim to every
/// view-model shape that carries an editor).</item>
/// </list>
/// The <c>GrantOption</c> option-type itself lives next to
/// <see cref="ProfileEditViewModel"/> (the M2 U11 origin) — an
/// un-relocation so the M2 pin comments there stay accurate.
/// </summary>
public sealed record GrantPickersModel
{
    /// <summary>The form-field prefix this picker posts under —
    /// <c>Visibility</c> / <c>ContactVisibility</c> on the profile
    /// editor, <c>Audience</c> on the post composer. The hidden
    /// textarea's <c>name</c> is <c>&lt;EditorName&gt;.Grants</c> and
    /// the pickers' <c>data-editor</c> / <c>data-editor-name</c>
    /// attributes carry it, so the profile edit page's sync script
    /// (the <c>_GrantPickerScripts</c> partial) works unchanged for
    /// every invocation.</summary>
    public string EditorName { get; init; } = string.Empty;

    /// <summary>The editor this section is rendered for (the
    /// <c>active</c> editor shape — the parent partial's off-shape
    /// fallback when the saved shape is absent). See
    /// <see cref="AudienceEditorModel.ParsedGrants"/> and <see
    /// cref="AudienceEditorModel.Grants"/> — the single form-bound
    /// grant surface the <c>POST</c> binds.</summary>
    public AudienceEditorModel Editor { get; init; } = new();

    /// <summary>The grantable <b>user</b> rows (the "Residences"
    /// checkbox list) — <see cref="GrantOption"/> items; an empty list
    /// renders the picker's "no one else to grant to yet" note.</summary>
    public IReadOnlyList<GrantOption> Users { get; init; } = new List<GrantOption>();

    /// <summary>The grantable <b>group</b> rows (the "Groups" checkbox
    /// list) — <see cref="GrantOption"/> items; an empty list renders
    /// the picker's "no groups exist yet" note.</summary>
    public IReadOnlyList<GrantOption> Groups { get; init; } = new List<GrantOption>();
}
