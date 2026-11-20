using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace DotnetWebDemo.Web.Models;

/// <summary>
/// 应用用户实体，继承 ASP.NET Core Identity 的 <see cref="IdentityUser"/>。
/// </summary>
public class ApplicationUser : IdentityUser
{
    [MaxLength(100)]
    [Display(Name = "姓名")]
    public string? FullName { get; set; }

    [MaxLength(100)]
    [Display(Name = "部门")]
    public string? Department { get; set; }

    /// <summary>账号是否启用（停用后禁止登录，但保留数据）。</summary>
    [Display(Name = "是否启用")]
    public bool IsEnabled { get; set; } = true;

    [Display(Name = "创建时间")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "最后登录")]
    public DateTime? LastLoginAt { get; set; }
}
