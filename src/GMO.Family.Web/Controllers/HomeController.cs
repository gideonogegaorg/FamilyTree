using System.Diagnostics;

using GMO.Family.Web.Models;
using GMO.Family.Web.Options;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GMO.Family.Web.Controllers;

public class HomeController : Controller
{
    private readonly IOptionsMonitor<GoogleAuthOptions> _googleAuth;

    public HomeController(IOptionsMonitor<GoogleAuthOptions> googleAuth)
    {
        _googleAuth = googleAuth;
    }

    [AllowAnonymous]
    public IActionResult Index()
    {
        ViewBag.GoogleAuthEnabled = _googleAuth.CurrentValue.Enabled;
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}