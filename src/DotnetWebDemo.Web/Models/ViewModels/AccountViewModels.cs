using System.ComponentModel.DataAnnotations;

namespace DotnetWebDemo.Web.Models.ViewModels;

/// <summary>登录表单模型。</summary>
public class LoginViewModel
{
    [Required(ErrorMessage = "请输入用户名")]
    [Display(Name = "用户名")]
    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入密码")]
    [Display(Name = "密码")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "记住我")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

/// <summary>注册表单模型。</summary>
public class RegisterViewModel
{
    [Required(ErrorMessage = "请输入用户名")]
    [Display(Name = "用户名")]
    [StringLength(256, MinimumLength = 3, ErrorMessage = "用户名长度需在 3 到 256 个字符之间")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入邮箱")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    [Display(Name = "邮箱")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入姓名")]
    [Display(Name = "姓名")]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "部门")]
    [StringLength(100)]
    public string? Department { get; set; }

    [Required(ErrorMessage = "请输入密码")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "密码长度不能少于 8 位")]
    [DataType(DataType.Password)]
    [Display(Name = "密码")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "请再次输入密码")]
    [DataType(DataType.Password)]
    [Display(Name = "确认密码")]
    [Compare(nameof(Password), ErrorMessage = "两次输入的密码不一致")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>用户列表中的单行数据。</summary>
public class UserListItem
{
    public required string Id { get; init; }
    public required string UserName { get; init; }
    public string? Email { get; init; }
    public string? FullName { get; init; }
    public string? Department { get; init; }
    public bool IsEnabled { get; init; }
    public bool EmailConfirmed { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}

/// <summary>分页结果。</summary>
public class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

/// <summary>首页仪表盘统计。</summary>
public class DashboardStats
{
    public int TotalUsers { get; init; }
    public int EnabledUsers { get; init; }
    public int DisabledUsers { get; init; }
    public int NewUsersToday { get; init; }
    public int TotalRoles { get; init; }
    public DateTime GeneratedAt { get; init; }
}
