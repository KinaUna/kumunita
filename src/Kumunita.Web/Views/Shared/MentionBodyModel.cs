namespace Kumunita.Web.Views.Shared;

/// <summary>
/// The <c>_MentionRenderer</c> partial's model (M36 U07): the content's
/// <b>Markdown</b> body + its author's <b>subject id</b> (the self-exclusion
/// input — the author is never mentioned by their own name, M36-5 / ADR 0165 D2).
/// <para>
/// The partial runs the frozen <see cref="Kumunita.Web.Security.MarkdownRenderer"/>
/// first (the one-renderer pin — <b>unchanged</b>, M36·6), then the M36
/// <b>mention render pass</b> (<see cref="Kumunita.Web.Security.MentionRenderer"/>,
/// the server-side pass at the <c>MarkdownRenderer</c> boundary — ADR 0165 D6),
/// and emits the result via <c>Html.Raw</c> (the body is already
/// escape-first-rendered by the <c>MarkdownRenderer</c>; the pass only wraps
/// resolved tokens in an anchor whose <c>href</c> is a platform-relative
/// <c>/directory/{id}</c>).
/// </para>
/// </summary>
/// <param name="Body">The content's Markdown body (the <c>@</c>-tokens' source).</param>
/// <param name="AuthorId">The content's author subject id (the self-exclusion input).</param>
public sealed record MentionBodyModel(string Body, string AuthorId);
