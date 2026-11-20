using DotnetWebDemo.Web.Components;
using DotnetWebDemo.Web.Data;
using DotnetWebDemo.Web.Models;
using DotnetWebDemo.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------------
// MVC(路由 / 视图 / 表单) + 内嵌 Blazor Server 组件
// ------------------------------------------------------------------
builder.Services.AddControllersWithViews();

// Blazor Server：允许在 MVC 视图中以 <component render-mode="ServerPrerendered" /> 方式嵌入组件
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 让 Blazor 组件内可使用 AuthorizeView / AuthenticationState
builder.Services.AddCascadingAuthenticationState();

// ------------------------------------------------------------------
// 数据库：SQL Server + EF Core 11
// ------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("未配置连接字符串 ConnectionStrings:DefaultConnection。");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
        sql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name)));

// ------------------------------------------------------------------
// ASP.NET Core Identity：用户 / 角色 / 登录
// ------------------------------------------------------------------
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.AllowedForNewUsers = true;
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

// ------------------------------------------------------------------
// 启动初始化：等待 SQL Server 就绪 → 应用迁移 → 写入种子数据
// ------------------------------------------------------------------
if (app.Configuration.GetValue("Database:AutoMigrate", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
    await DbInitializer.InitializeAsync(scope.ServiceProvider, logger);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// 属性路由（Account / Users 等带 [Route] 的控制器）
app.MapControllers();

// 约定路由（首页 {controller}/{action}/{id?}）
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 注册 Blazor Server 交互式渲染模式（同时映射 /_blazor 电路端点）
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
