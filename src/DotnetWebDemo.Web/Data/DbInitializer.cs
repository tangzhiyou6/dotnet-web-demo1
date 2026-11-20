using DotnetWebDemo.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DotnetWebDemo.Web.Data;

/// <summary>
/// 启动时的数据库初始化：等待 SQL Server 就绪 → 确保数据库存在 → 执行迁移 → 写入种子数据。
/// </summary>
public static class DbInitializer
{
    /// <summary>种子角色。</summary>
    public static readonly string[] Roles = ["Admin", "User"];

    /// <summary>种子管理员账号。</summary>
    public const string AdminUserName = "admin";

    /// <summary>种子管理员密码。</summary>
    public const string AdminPassword = "Admin@123";

    /// <summary>种子普通用户账号。</summary>
    public const string DemoUserName = "demo";

    /// <summary>种子普通用户密码。</summary>
    public const string DemoPassword = "Demo@123";

    public static async Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        var provider = services.GetRequiredService<AppDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        // 1) 等待数据库服务器就绪并确保目标数据库存在
        await EnsureDatabaseAsync(provider, logger);

        // 3) 应用迁移
        var pending = await GetPendingMigrationsAsync(provider, logger);
        if (pending.Count > 0)
        {
            logger.LogInformation("正在应用 {Count} 个数据库迁移: {Migrations}",
                pending.Count, string.Join(", ", pending));
            await provider.Database.MigrateAsync();
        }
        else
        {
            logger.LogInformation("数据库结构已是最新，无需迁移。");
        }

        // 4) 角色
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"创建角色 {role} 失败: {DescribeErrors(result)}");
                }

                logger.LogInformation("已创建角色 {Role}", role);
            }
        }

        // 5) 种子账号
        await EnsureUserAsync(userManager, AdminUserName, "admin@example.com", "系统管理员", "IT",
            AdminPassword, "Admin", logger);
        await EnsureUserAsync(userManager, DemoUserName, "demo@example.com", "演示用户", "产品部",
            DemoPassword, "User", logger);
    }

    /// <summary>
    /// 连接 master 库等待 SQL Server 就绪，并创建目标数据库。
    /// EF Core 的 Migrate() 不会自动创建数据库，首次部署必须先建库，
    /// 否则连接目标库会报 "Cannot open database ... The login failed"。
    /// </summary>
    private static async Task EnsureDatabaseAsync(AppDbContext context, ILogger logger)
    {
        var connectionString = context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("缺少数据库连接字符串。");

        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master",
            ConnectTimeout = 15
        };

        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        const int maxAttempts = 30;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync();

                if (!string.IsNullOrWhiteSpace(databaseName)
                    && !databaseName.Equals("master", StringComparison.OrdinalIgnoreCase))
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = @"
IF DB_ID(@dbName) IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@dbName);
    EXEC sp_executesql @sql;
END";
                    command.Parameters.AddWithValue("@dbName", databaseName);
                    await command.ExecuteNonQueryAsync();

                    logger.LogInformation("数据库 {Database} 已就绪。", databaseName);
                }

                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("数据库尚未就绪(第 {Attempt}/{MaxAttempts} 次尝试): {Message}",
                    attempt, maxAttempts, ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new InvalidOperationException("在预期时间内无法连接 SQL Server。");
    }

    private static async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(AppDbContext context, ILogger logger)
    {
        try
        {
            return (await context.Database.GetPendingMigrationsAsync()).ToList();
        }
        catch (Exception ex)
        {
            // 迁移历史表尚未创建时可能抛错，忽略并直接尝试迁移
            logger.LogDebug("读取待执行迁移失败，将直接尝试执行迁移: {Message}", ex.Message);
            return [];
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string userName,
        string email,
        string fullName,
        string department,
        string password,
        string role,
        ILogger logger)
    {
        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                Department = department,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"创建种子用户 {userName} 失败: {DescribeErrors(result)}");
            }

            await userManager.AddToRoleAsync(user, role);
            logger.LogInformation("已创建种子用户 {UserName}(角色 {Role}),初始密码 {Password}",
                userName, role, password);
        }
        else if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
            logger.LogInformation("已为用户 {UserName} 补充角色 {Role}", userName, role);
        }
    }

    private static string DescribeErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => e.Description));
}
