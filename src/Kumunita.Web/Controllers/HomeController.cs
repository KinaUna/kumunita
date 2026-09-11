using System.Diagnostics;
using Kumunita.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Kumunita.Web.Models;

namespace Kumunita.Web.Controllers;

public class HomeController : Controller
{
    private readonly CommunityOptions _community;

    public HomeController(IOptions<CommunityOptions> community)
    {
        _community = community.Value;
    }

    public IActionResult Index()
    {
        return View(new HomeViewModel(_community.Name, _community.SupportEmail));
    }

    /// <summary>
    /// GET /about — the product story: the neighbourhood pitch, feature cards,
    /// community stats, code/docs links and the contact CTA band. The home page
    /// stays the short roadmap; the longer landing content lives here.
    /// </summary>
    [HttpGet("/about")]
    public IActionResult About()
    {
        return View(new HomeViewModel(_community.Name, _community.SupportEmail));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
