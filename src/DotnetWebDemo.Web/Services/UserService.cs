using DotnetWebDemo.Web.Data;
using DotnetWebDemo.Web.Models;
using DotnetWebDemo.Web.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DotnetWebDemo.Web.Services;

/// <inheritdoc />
public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UserService> _logger;

    public UserService(AppDbContext db, UserManager<ApplicationUser> userManager, ILogger<UserService> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<PagedResult<UserListItem>> SearchUsersAsync(
        string? keyword, string? role, bool? enabled, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        IQueryable<ApplicationUser> query = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";
            query = query.Where(u =>
                EF.Functions.Like(u.UserName ?? string.Empty, pattern)
                || EF.Functions.Like(u.Email ?? string.Empty, pattern)
                || EF.Functions.Like(u.FullName ?? string.Empty, pattern));
        }

        if (enabled.HasValue)
        {
            query = query.Where(u => u.IsEnabled == enabled.Value);
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleId = await _db.Roles
                .Where(r => r.Name == role)
                .Select(r => (string?)r.Id)
                .FirstOrDefaultAsync();

            if (roleId is null)
            {
                return new PagedResult<UserListItem>
                {
                    Items = [],
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize
                };
            }

            var userIdsInRole = _db.UserRoles
                .Where(ur => ur.RoleId == roleId)
                .Select(ur => ur.UserId);

            query = query.Where(u => userIdsInRole.Contains(u.Id));
        }

        var total = await query.CountAsync();

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .ThenBy(u => u.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = await AttachRolesAsync(users);

        return new PagedResult<UserListItem>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync()
    {
        return await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => r.Name!)
            .ToListAsync();
    }

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        var today = DateTime.UtcNow.Date;

        var totalUsers = await _db.Users.CountAsync();
        var enabledUsers = await _db.Users.CountAsync(u => u.IsEnabled);
        var newUsersToday = await _db.Users.CountAsync(u => u.CreatedAt >= today);
        var totalRoles = await _db.Roles.CountAsync();

        return new DashboardStats
        {
            TotalUsers = totalUsers,
            EnabledUsers = enabledUsers,
            DisabledUsers = totalUsers - enabledUsers,
            NewUsersToday = newUsersToday,
            TotalRoles = totalRoles,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<(bool Success, string Message)> SetUserEnabledAsync(
        string userId, bool enabled, string currentUserId)
    {
        if (string.Equals(userId, currentUserId, StringComparison.Ordinal))
        {
            return (false, "不能修改当前登录账号自身的启用状态。");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return (false, "用户不存在。");
        }

        if (user.IsEnabled == enabled)
        {
            return (false, $"用户 {user.UserName} 已经是{(enabled ? "启用" : "停用")}状态。");
        }

        user.IsEnabled = enabled;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return (false, string.Join("; ", updateResult.Errors.Select(e => e.Description)));
        }

        if (!enabled)
        {
            // 刷新安全戳，使该用户已签发的登录 Cookie 立即失效
            await _userManager.UpdateSecurityStampAsync(user);
        }

        _logger.LogInformation("用户 {UserName} 的启用状态被设置为 {Enabled}(操作人 {Operator})",
            user.UserName, enabled, currentUserId);

        return (true, $"用户 {user.UserName} 已{(enabled ? "启用" : "停用")}。");
    }

    public async Task RecordLoginAsync(string userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return;
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task<List<UserListItem>> AttachRolesAsync(List<ApplicationUser> users)
    {
        if (users.Count == 0)
        {
            return [];
        }

        var userIds = users.Select(u => u.Id).ToList();

        var roleNamesByUser = await _db.UserRoles
            .AsNoTracking()
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(_db.Roles.AsNoTracking(),
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new { ur.UserId, RoleName = r.Name! })
            .ToListAsync();

        var rolesLookup = roleNamesByUser
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.RoleName).OrderBy(n => n).ToList());

        return users.Select(u => new UserListItem
        {
            Id = u.Id,
            UserName = u.UserName ?? string.Empty,
            Email = u.Email,
            FullName = u.FullName,
            Department = u.Department,
            IsEnabled = u.IsEnabled,
            EmailConfirmed = u.EmailConfirmed,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt,
            Roles = rolesLookup.TryGetValue(u.Id, out var roles) ? roles : []
        }).ToList();
    }
}
