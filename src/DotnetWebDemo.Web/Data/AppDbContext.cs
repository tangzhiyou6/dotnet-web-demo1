using DotnetWebDemo.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DotnetWebDemo.Web.Data;

/// <summary>
/// 应用数据库上下文，基于 Identity 的默认存储并扩展了用户表。
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FullName).HasMaxLength(100);
            entity.Property(u => u.Department).HasMaxLength(100);
            entity.Property(u => u.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(u => u.FullName).HasDatabaseName("IX_AspNetUsers_FullName");
        });
    }
}
