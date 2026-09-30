using Microsoft.AspNetCore.Identity;

using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Components.User;

/// <summary>
///     UserDetail コンポーネントにおけるユーザー操作（フォロー、管理者昇格など）を集約する ViewModel。
///     bUnit を使わずに xUnit で直接単体テスト可能。
/// </summary>
public class UserDetailActionViewModel
{
    private readonly IUserDataProvider _userDataProvider;
    private readonly UserManager<ApplicationUser>? _userManager;

    public UserDetailActionViewModel(
        IUserDataProvider userDataProvider,
        UserManager<ApplicationUser>? userManager = null)
    {
        _userDataProvider = userDataProvider;
        _userManager = userManager;
    }

    /// <summary>
    ///     ユーザーのフォロー状態をトグルする。
    /// </summary>
    public async Task<bool> ToggleFollowAsync(string currentUserId, string targetUserId)
    {
        if (string.IsNullOrEmpty(currentUserId) || string.IsNullOrEmpty(targetUserId) || currentUserId == targetUserId)
        {
            return false;
        }

        return await _userDataProvider.ToggleFollowUserAsync(currentUserId, targetUserId);
    }

    /// <summary>
    ///     ユーザーを Admin ロールへ昇格する。
    /// </summary>
    public async Task<IdentityResult> MakeAdminAsync(string targetUserId)
    {
        if (string.IsNullOrEmpty(targetUserId))
        {
            return IdentityResult.Failed(new IdentityError { Description = "ユーザーIDが指定されていません。" });
        }

        if (_userManager == null)
        {
            return IdentityResult.Failed(new IdentityError { Description = "UserManager が利用できません。" });
        }

        ApplicationUser? user = await _userManager.FindByIdAsync(targetUserId);
        if (user == null)
        {
            return IdentityResult.Failed(new IdentityError { Description = "ユーザーが見つかりません。" });
        }

        return await _userManager.AddToRoleAsync(user, "Admin");
    }
}