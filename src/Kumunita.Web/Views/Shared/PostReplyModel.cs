using Kumunita.Web.Models;

namespace Kumunita.Web.Views.Shared;

/// <summary>
/// The <c>_PostReply</c> partial's model (U06): one reply (the
/// <see cref="ReplyItem"/>) plus the minimum parent context the reply markup
/// needs (the post id for the reply's routes; the post's enabled-language
/// set for the translation add/edit candidates; the viewer's default-visible
/// variant for the ADR 0049 swap; the confirm + aria + report-reason strings
/// the reply's forms/menus read).
/// <para>
/// The partial declares <c>r</c> (the reply), re-injects its own localization
/// services + re-resolves <c>LangName</c> / the confirm / aria strings, so the
/// reply body is byte-identical to the prior inline copy in
/// <c>Posts/Detail.cshtml</c> — U06 is a pure view-layer refactor and the
/// served-page diff is the evidence.
/// </para>
/// </summary>
public sealed record PostReplyModel(
    ReplyItem Reply,
    string PostId,
    IReadOnlyList<LanguageOption> Languages,
    string DefaultVariant,
    string ConfirmRemoveTrans,
    string ConfirmDeleteReply,
    string AriaActionsFor,
    string ReportReasonPlaceholder);
