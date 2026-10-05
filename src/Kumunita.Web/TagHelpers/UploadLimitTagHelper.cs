using Kumunita.Web.Security;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Kumunita.Web.TagHelpers;

/// <summary>
/// <c>&lt;kw-upload-limit /&gt;</c> — renders the admin-configured per-file
/// upload limit as a form hint under a file input. Renders nothing when the
/// limit is unlimited.
/// </summary>
[HtmlTargetElement("kw-upload-limit")]
public sealed class UploadLimitTagHelper(IUploadLimitHint hint) : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var text = await hint.GetTextAsync();
        if (text is null)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? $"{c.Value} " : "";
        output.Attributes.SetAttribute("class", cls + "form-text small");
        output.Content.SetContent(text);
    }
}
