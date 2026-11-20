using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotnetWebDemo.Web.Data;

/// <summary>
/// 设计时 DbContext 工厂：让 `dotnet ef migrations` 在不启动 Web 主机、
/// 也不连接数据库的情况下生成迁移脚本。
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DefaultConnectionString =
        "Server=localhost,1433;Database=DotnetWebDemoDb;User Id=sa;Password=YourStrong@Password123;TrustServerCertificate=True;Encrypt=False";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? DefaultConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name))
            .Options;

        return new AppDbContext(options);
    }
}
