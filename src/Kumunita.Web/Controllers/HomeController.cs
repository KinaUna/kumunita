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

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
