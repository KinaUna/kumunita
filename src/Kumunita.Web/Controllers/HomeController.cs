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

    // NOTE (ML-UI U7): GET /about moved to StaticPagesController.About — one
    // route, one owner. It now renders an admin-created `about` LocalizedPage
    // when one exists, falling back to the product-story view (Views/Home/About)
    // when the page is truly absent. The footer's asp-action="About" link is a
    // route, so it still targets /about unchanged.

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
