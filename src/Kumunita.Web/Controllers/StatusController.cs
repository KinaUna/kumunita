using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

[Route("status")]
public sealed class StatusController : Controller
{
    private static readonly int[] SupportedCodes = [400, 403, 404, 413, 415, 429, 500];

    [HttpGet("{code:int}")]
    public IActionResult Index(int code)
    {
        var supportedCode = SupportedCodes.Contains(code) ? code : 500;
        ViewData["Title"] = supportedCode.ToString();
        return View(new StatusPageViewModel(supportedCode, GetBackUrl()));
    }

    private string GetBackUrl()
    {
        var referer = Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Request.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Authority, Request.Host.Value, StringComparison.OrdinalIgnoreCase))
        {
            var pathAndQuery = uri.PathAndQuery;
            if (pathAndQuery.StartsWith("/", StringComparison.Ordinal)
                && !pathAndQuery.StartsWith("//", StringComparison.Ordinal)
                && !pathAndQuery.StartsWith("/\\", StringComparison.Ordinal))
            {
                return pathAndQuery;
            }
        }

        return "/";
    }
}

public sealed record StatusPageViewModel(int Code, string BackUrl);
