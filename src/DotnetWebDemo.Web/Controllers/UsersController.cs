using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotnetWebDemo.Web.Controllers;

/// <summary>用户管理页面，需要登录。</summary>
[Authorize]
[Route("Users")]
public class UsersController : Controller
{
    /// <summary>用户列表页，页面内嵌 Blazor Server 用户表格组件。</summary>
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
