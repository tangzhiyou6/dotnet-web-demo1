# Blazor + MVC 用户登录系统（ASP.NET Core 11 / SQL Server 2025）

一个 **ASP.NET Core MVC 为主、内嵌 Blazor Server 组件** 的用户登录示例，包含：

- **登录 / 注册 / 登出**：基于 ASP.NET Core Identity + Cookie 认证
- **首页**：内嵌 Blazor Server 仪表盘组件，统计信息经 SignalR 实时推送
- **用户页面**：内嵌 Blazor Server 交互式表格，支持搜索、角色过滤、分页、启用/停用
- **数据库**：SQL Server 2025 + EF Core 11（启动时自动迁移 + 写入种子数据）
- **部署**：Docker 多阶段构建，`docker compose` 一键启动，目标平台 **linux/amd64**

---

## 1. 技术栈

| 层次 | 技术 |
| --- | --- |
| 运行时 | .NET 11 (ASP.NET Core 11) |
| 页面与路由 | ASP.NET Core MVC（Controller + Razor View） |
| 交互组件 | Blazor Server（`render-mode="ServerPrerendered"` 内嵌于 MVC 视图） |
| 认证 | ASP.NET Core Identity + Cookie Authentication |
| ORM | EF Core 11 (`Microsoft.EntityFrameworkCore.SqlServer`) |
| 数据库 | SQL Server 2025（容器镜像 `mcr.microsoft.com/mssql/server:2025-latest`） |
| 部署 | Docker + Docker Compose，平台 `linux/amd64` |

> ⚠️ **关于 "SQL Server 2026"**：SQL Server 2026 截至目前尚未发布，官方容器镜像仓库中最新的 GA 版本是
> **SQL Server 2025**（17.x，2025-11-18 发布），镜像标签为 `2025-latest`。因此本项目使用 SQL Server 2025。

---

## 2. 目录结构

```
dotnet-web-demo1/
├── DotnetWebDemo.sln                  # 解决方案
├── Dockerfile                       # 多阶段构建（sdk:11.0 → aspnet:11.0）
├── docker-compose.yml               # sqlserver(2025) + web 两个服务
├── .env.example                     # 环境变量样例（复制为 .env 后可改密码/端口）
├── scripts/smoke-test.sh            # 端到端冒烟测试（curl + cookie jar）
├── src/DotnetWebDemo.Web/
│   ├── Program.cs                   # 启动装配：MVC + Blazor + Identity + EF Core
│   ├── Controllers/
│   │   ├── AccountController.cs     # 登录 / 注册 / 登出
│   │   ├── HomeController.cs        # 首页
│   │   └── UsersController.cs       # 用户管理（[Authorize]）
│   ├── Components/                  # 内嵌的 Blazor Server 组件
│   │   ├── App.razor                # 根组件（MapRazorComponents 注册）
│   │   ├── Account/PasswordStrength.razor   # 实时密码强度提示
│   │   ├── Home/Dashboard.razor     # 首页统计仪表盘（定时自动刷新）
│   │   ├── Shared/StatCard.razor    # 统计卡片
│   │   └── Users/UserTable.razor    # 交互式用户表格
│   ├── Data/
│   │   ├── AppDbContext.cs          # IdentityDbContext
│   │   ├── AppDbContextFactory.cs   # 设计时工厂（生成迁移用）
│   │   ├── DbInitializer.cs         # 等待 DB 就绪 → 迁移 → 种子数据
│   │   └── Migrations/              # EF Core 迁移
│   ├── Models/                      # ApplicationUser + ViewModels
│   ├── Services/                    # IUserService / UserService（MVC 与 Blazor 共用）
│   └── Views/                       # Razor 视图（MVC 路由与页面）
```

---

## 3. Docker Compose 运行（推荐，linux/amd64）

### 3.1 前置条件

- Docker 24+ / Docker Compose v2+
- 可用内存 ≥ 3GB（SQL Server 至少需要 2GB）
- 若在 Apple Silicon(M1/M2/M3) 等 arm64 机器上构建 amd64 镜像，需支持 amd64 模拟（Docker Desktop / OrbStack 均支持）

### 3.2 启动

```bash
cd dotnet-web-demo1

# 可选：自定义密码与端口
cp .env.example .env

# 构建并启动
docker compose up -d --build

# 查看日志
docker compose logs -f web
```

启动后访问：

| 地址 | 说明 |
| --- | --- |
| <http://localhost:8080> | 首页 |
| <http://localhost:8080/Account/Login> | 登录 |
| <http://localhost:8080/Account/Register> | 注册 |
| <http://localhost:8080/Users> | 用户管理（需登录） |
| <localhost:1433> | SQL Server（sa 账号） |

> SQL Server 容器首次启动需要 30~60 秒完成初始化，compose 已通过 `healthcheck` + `depends_on: service_healthy`
> 确保数据库就绪后才启动应用；应用启动时还会自动执行 EF Core 迁移并写入种子数据。

### 3.3 常用命令

```bash
docker compose ps                    # 查看服务状态
docker compose logs -f web           # 查看应用日志
docker compose down                  # 停止并移除容器
docker compose down -v               # 同时删除数据库卷（数据清空）
docker compose up -d --build --force-recreate   # 重新构建并重建
```

### 3.4 指定 amd64 平台

`docker-compose.yml` 中已为两个服务设置 `platform: linux/amd64`，web 服务的 `build.platforms` 同样指定为
`linux/amd64`。若在 arm64 宿主机上遇到分层缓存问题，可先拉取 amd64 变体再构建：

```bash
docker pull --platform linux/amd64 mcr.microsoft.com/dotnet/sdk:11.0
docker pull --platform linux/amd64 mcr.microsoft.com/dotnet/aspnet:11.0
docker pull --platform linux/amd64 mcr.microsoft.com/mssql/server:2025-latest
docker compose build --no-cache
```

部署到纯 amd64 的 Linux 服务器时，直接 `docker compose up -d --build` 即可，无需任何额外处理。

---

## 4. 默认账号（种子数据自动创建）

| 用户名 | 密码 | 角色 |
| --- | --- | --- |
| `admin` | `Admin@123` | Admin |
| `demo` | `Demo@123` | User |

> 生产环境请务必修改 `appsettings.json` / 环境变量中的数据库连接与种子密码。

---

## 5. 本地开发运行

```bash
cd src/DotnetWebDemo.Web

# 连接本机 SQL Server 2025（默认连接串见 appsettings.Development.json）
dotnet run
```

如需自行创建数据库：

```bash
dotnet tool install --global dotnet-ef --version 11.0.0
export PATH="$PATH:$HOME/.dotnet/tools"

dotnet ef database update      # 应用迁移
dotnet ef migrations add Xxx   # 新增迁移
```

也可完全用 Docker 起一个本地数据库：

```bash
docker run -d --name demo1-sqlserver \
  -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='YourStrong@Password123' -e MSSQL_PID=Developer \
  -p 1433:1433 mcr.microsoft.com/mssql/server:2025-latest
```

---

## 6. 实现要点

### 6.1 MVC 为主 + 内嵌 Blazor Server

MVC 负责路由、登录表单与页面渲染；页面中通过组件标签助手嵌入交互式组件：

```cshtml
<component type="typeof(DotnetWebDemo.Web.Components.Users.UserTable)"
           render-mode="ServerPrerendered"
           CurrentUserId="@currentUserId" />

@section Scripts {
    <script src="_framework/blazor.web.js"></script>
}
```

对应启动装配：

```csharp
builder.Services.AddControllersWithViews();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

app.MapControllers();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
```

> 注意：ASP.NET Core 11 中 `MapControllersWithViews()` 已被移除，改为 `MapControllers()` + `MapControllerRoute()`。
>
> 另外，带类级 `[Route("Account")]` 的控制器，其所有 action 都会映射到 `/Account`；
> 需要按 action 区分时必须写成 `[Route("Account/[action]")]`，否则 `/Account/Login` 会 404。

### 6.2 登录与授权

- `AccountController` 使用 `SignInManager.PasswordSignInAsync` 登录，登录前校验 `ApplicationUser.IsEnabled`
- 停用用户时刷新安全戳（`UpdateSecurityStampAsync`），已签发的登录 Cookie 立即失效
- 连续登录失败 5 次锁定 5 分钟（`Lockout` 配置）
- `UsersController` 标注 `[Authorize]`，未登录访问 `/Users` 会跳转登录页并带上 `returnUrl`

### 6.3 数据访问

MVC 控制器与 Blazor 组件共用 `IUserService`（Scoped），统一走 EF Core；
用户列表使用 `AsNoTracking` + 一次性批量关联角色，避免 N+1 查询。

### 6.4 数据库初始化

`DbInitializer.InitializeAsync` 在应用启动时依次执行：

1. 连接 **master** 库等待 SQL Server 就绪（最多 30 次 × 2 秒）
2. 若目标库不存在则执行 `CREATE DATABASE`
3. `Database.MigrateAsync()` 应用待执行迁移
4. 创建 `Admin` / `User` 角色
5. 创建种子账号 `admin` / `demo`

> ⚠️ **第 2 步是必需的**：EF Core 的 `Migrate()` **不会**自动创建数据库，
> 首次部署时若目标库不存在，连接会直接报 `Cannot open database "DotnetWebDemoDb" ... The login failed`。
> 因此应用先连 `master` 库建库，再执行迁移。

可通过配置关闭自动初始化：`Database:AutoMigrate=false`（环境变量 `Database__AutoMigrate=false`）。

---

## 7. 端到端冒烟测试

仓库内置 `scripts/smoke-test.sh`，用 curl + cookie jar 跑完整链路：

```bash
bash scripts/smoke-test.sh                 # 默认 http://localhost:8080
bash scripts/smoke-test.sh http://host:port
```

覆盖：首页渲染 → Blazor 客户端资源 → `/blazor` 电路端点 → 未登录访问 `/Users` 被拦截 →
`admin` 登录 → 用户页渲染 → 注册新用户并出现在列表 → 退出登录。

预期输出：

```
结果：通过 15 项，失败 0 项
```

---

## 8. 常见问题

| 现象 | 原因与处理 |
| --- | --- |
| 应用日志反复出现 `Cannot open database "DotnetWebDemoDb"` | 应用已自动建库；若仍出现，检查连接字符串里的库名与 `Database__AutoMigrate` 配置 |
| 容器反复重启，日志报 `Login failed for user 'sa'` | SA 密码不满足复杂度要求（至少 8 位，含大小写、数字、符号），修改 `.env` 中的 `MSSQL_SA_PASSWORD` 后 `docker compose down -v` 重建 |
| 页面能打开但组件无交互 | 浏览器控制台检查 `_framework/blazor.web.js` 是否 200；若被反向代理拦截，需放行 `/_blazor`（WebSocket）与 `/_framework/*` |
| 数据库密码改了但连不上 | 卷 `mssql-data` 中保存的是旧密码；执行 `docker compose down -v` 清空后重启 |
| `dotnet ef` 找不到命令 | `dotnet tool install --global dotnet-ef --version 11.0.0` 后把 `$HOME/.dotnet/tools` 加入 PATH |
| amd64 构建报分层缓存错误 | 先 `docker pull --platform linux/amd64 <基础镜像>`，再 `docker compose build --no-cache` |
| 页面能打开但 Blazor 组件完全无交互，Console 报 `blazor.web.js` 404 | Docker 分层还原导致静态资源清单不完整。**不要在 `dotnet publish` 上加 `--no-restore`**（详见下方「构建踩坑」） |
| 国内网络拉取 MCR 镜像很慢 | MCR 不在 Docker Hub 镜像加速范围内，可换用支持 MCR 的代理（如 `m.daocloud.io/mcr.microsoft.com/...`）拉取后重新打回原标签 |

### 8.1 构建踩坑：不要在 publish 时加 `--no-restore`

ASP.NET Core 的静态资源清单（StaticWebAssetsManifest）是在**还原阶段**生成的。
Dockerfile 里「先只复制 csproj 做还原，再复制源码」的经典分层写法，在还原那一刻项目目录里
**还没有 `wwwroot/`**，生成的是一份不完整清单，结果是
`Microsoft.AspNetCore.App.Internal.Assets` 提供的 `wwwroot/_framework/blazor.web.js`
（Blazor Server 客户端脚本）被整个漏掉：

- 页面能正常渲染（组件是预渲染出来的，静态 HTML 看着没问题）
- 但组件**毫无交互**，浏览器 Console 里 `/_framework/blazor.web.js` 404

因此 Dockerfile 中的 publish 步骤**保留隐式还原**（NuGet 包已在缓存层就绪，几乎不额外耗时）。
本仓库的 Dockerfile 已按此处理并附注释，请勿"优化"回去。
