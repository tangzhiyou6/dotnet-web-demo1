using DotnetWebDemo.Web.Models.ViewModels;

namespace DotnetWebDemo.Web.Services;

/// <summary>
/// 用户业务服务：MVC 控制器与 Blazor 组件共用的数据访问入口。
/// </summary>
public interface IUserService
{
    /// <summary>按关键字 / 角色 / 启用状态分页查询用户。</summary>
    Task<PagedResult<UserListItem>> SearchUsersAsync(
        string? keyword, string? role, bool? enabled, int page, int pageSize);

    /// <summary>获取全部角色名称。</summary>
    Task<IReadOnlyList<string>> GetRolesAsync();

    /// <summary>获取首页统计信息。</summary>
    Task<DashboardStats> GetDashboardStatsAsync();

    /// <summary>启用 / 停用用户，返回操作结果说明。</summary>
    Task<(bool Success, string Message)> SetUserEnabledAsync(
        string userId, bool enabled, string currentUserId);

    /// <summary>记录用户最后登录时间。</summary>
    Task RecordLoginAsync(string userId);
}
