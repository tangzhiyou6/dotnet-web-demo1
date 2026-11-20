using DotnetWebDemo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotnetWebDemo.Web.Controllers;

/// <summary>首页。</summary>
public class HomeController : Controller
{
    private readonly IUserService _userService;

    public HomeController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>首页，页面内嵌 Blazor Server 仪表盘组件。</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        ViewBag.Stats = await _userService.GetDashboardStatsAsync();
        return View();
    }

    /// <summary>错误页。</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View();
    }
}
