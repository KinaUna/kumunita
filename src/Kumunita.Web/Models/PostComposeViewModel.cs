using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Kumunita.Web.Models;

/// <summary>
/// The composer form-bound model (M3, plan U7) — <c>GET /posts/new</c> +
/// <c>POST /posts/new</c>. <b>Component picker + title + body + audience</b>:
/// the M3 analog of M2's <see cref="ProfileEditViewModel"/> (the
/// "editor = two audiences" shape, the M2 U11 single-source pin). The
/// <see cref="ComponentId"/> field is a *feed organizer / candidate filter*
/// selection (C-M3·2: the "which community is this post for" bucket), **not**
/// an access decision — the audience <see cref="Audience"/> (a form-bound
/// <see cref="AudienceEditorModel"/> — the M2 audience editor, reused verbatim
/// per the plan's "reuse, don't re-invent" pin) is the **only** access
/// boundary; it is written verbatim into the <c>Post.Audience</c> row at
/// <c>POST</c> (ADR 0001-B — the author's choice is absolute;
/// <see cref="Kumunita.Core.Posts.PostDraft"/> →
/// <see cref="Kumunita.Core.Posts.PostService.CreatePostAsync"/> → the
/// <c>Post</c> document, bit-identical to what the form posted — the
/// M3 seam test <c>AuthorAudienceWrittenVerbatim</c> pins this at the
/// document layer).
/// <para>
/// <b>Component list</b> (<see cref="Components"/>) is the composer's *picker*
/// — the <see cref="Kumunita.Core.UserInfo.Component"/> candidate set from
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetComponentsAsync(bool)"/>
/// (invariant C-M3·2; the M3's single freeze-surface ADD on the frozen
/// <see cref="IUserInfoService"/>, the M2's <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetProfilesAsync(bool)"/>
/// ADD analog). It is <b>[BindNever]</b> (never *POSTed* back; the composer
/// form POSTs a <see cref="ComponentId"/> — the single selected component's
/// id — not a grant-list shape; the audience editor's grant-list shape is a
/// *JSON-string* on <see cref="AudienceEditorModel.Grants"/>, not a complex
/// binding — the M2 single-source pin, never a second parallel component
/// pick on the form).
/// <para>
/// <b>Validation</b> (<see cref="IsValid"/>): the component must be selected
/// (a "post for which community?" post with no component is a malformed
/// post, not a silent default to "Safety" — the M2 mode-required pin applied
/// to the *which list* field), the body must be present, and the audience
/// editor must be well-formed. Title is optional (the M3 <see cref="Kumunita.Core.Posts.Post"/>
/// is <c>Title ?</c>); an empty title renders as a body-only post.
/// </para>
/// </summary>
public sealed class PostComposeViewModel
{
    public string ComponentId { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// The composer's component *picker* options — the
    /// <see cref="Kumunita.Core.UserInfo.Component"/> candidate set, read
    /// from <see cref="IUserInfoService.GetComponentsAsync(bool)"/> at
    /// <c>GET</c> and re-read at <c>POST</c> re-render (the "same
    /// <c>enabledOnly: true</c>" shape). <b>[BindNever]</b> — the form POSTs
    /// a <see cref="ComponentId"/> (the selected component's id), not a
    /// component-list shape; a form-bound list of components would be a
    /// *parallel* component pick next to <see cref="ComponentId"/> — the
    /// M2 single-source pin at the component layer (never a second component
    /// binding on the form).
    /// </summary>
    [BindNever]
    public IReadOnlyList<(string Id, string Name)> Components { get; set; } = [];

    /// <summary>
    /// The composer's <b>audience</b> editor — the M2 reusable
    /// <see cref="AudienceEditorModel"/> (the
    /// <see cref="Kumunita.Core.Authorization.Audience"/> form-bound shape;
    /// the single-source "editor + audience are the *same* shape through one
    /// binder" pin, the M2 U11 precedent). This is the **only** access
    /// boundary on the composer form (C-M3·2: <see cref="ComponentId"/> is a
    /// feed organizer, never a gate) and is serialized back to the
    /// <see cref="Kumunita.Core.Posts.Post.Audience"/> row via
    /// <see cref="AudienceEditorModel.BuildAudience"/> (the M2 single-
    /// deserialization site) by the <c>POST</c>. The M2 pin applies
    /// verbatim: the composer writes the <b>author's chosen audience
    /// verbatim</b> (ADR 0001-B) — never auto-augmented by M3's own logic.
    /// </summary>
    public AudienceEditorModel Audience { get; set; } = new();

    /// <summary>
    /// The composer's shape is well-formed for a <c>POST</c>. <see
    /// cref="ComponentId"/> must be non-empty (a "post for which community?"
    /// post with no component is a malformed post, not a silent default to
    /// the first component — the M2 "a form-bound owner id" pin applied to
    /// the *which list* field; an empty <see cref="ComponentId"/> is the
    /// same class of bug as a form-bound owner id lost on round-trip, which
    /// the M2 <see cref="AudienceEditorModel"/>'s <see
    /// cref="AudienceEditorModel.IsValid"/> mode-required pin guards
    /// against). <see cref="Body"/> must be non-empty. <see cref="Audience"/>
    /// must be well-formed (<see cref="AudienceEditorModel.IsValid"/>).
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ComponentId)) return false;
            if (string.IsNullOrWhiteSpace(Body)) return false;
            return Audience is { } a && a.IsValid;
        }
    }
}
