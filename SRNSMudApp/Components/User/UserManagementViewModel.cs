using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Components.User;

/// <summary>
///     ユーザー管理画面で表示する各ユーザーの ViewModel。
/// </summary>
public class UserManagementItemViewModel
{
    public ApplicationUser User { get; init; } = null!;
    public bool IsAdmin { get; set; }
    public bool IsBanned { get; set; }
}

/// <summary>
///     管理者用ユーザー管理画面の ViewModel。
///     ユーザー一覧のロード、保護アカウント判定、Admin権限付与/剥奪、BAN（利用停止）操作を担当する。
/// </summary>
public class UserManagementViewModel
{
    private readonly IUserDataProvider _userDataProvider;
    private readonly ISnackbar _snackbar;

    public UserManagementViewModel(
        IUserDataProvider userDataProvider,
        ISnackbar snackbar)
    {
        _userDataProvider = userDataProvider;
        _snackbar = snackbar;
    }

    public IReadOnlyList<UserManagementItemViewModel>? Users { get; private set; }
    public string? CurrentUserId { get; private set; }

    /// <summary>
    ///     ユーザー一覧と各ユーザーのAdminロール状態を読み込む。
    /// </summary>
    public async Task InitializeAsync(string? currentUserId)
    {
        CurrentUserId = currentUserId;

        List<ApplicationUser> users = await _userDataProvider.GetAllUsersAsync() ?? [];
        List<UserManagementItemViewModel> items = [];

        foreach (var user in users)
        {
            bool isAdmin = await _userDataProvider.IsUserInRoleAsync(user, "Admin");
            items.Add(new UserManagementItemViewModel
            {
                User = user,
                IsAdmin = isAdmin,
                IsBanned = user.IsBanned
            });
        }

        Users = items;
    }

    /// <summary>
    ///     ログイン中の管理者自身およびシステムアカウントであるか（自己BANや権限剥奪の防止対象か）を判定する。
    /// </summary>
    public bool IsProtectedUser(ApplicationUser user)
    {
        if (user == null) return false;

        return string.Equals(user.Id, CurrentUserId, StringComparison.Ordinal)
            || string.Equals(user.UserName, "system", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     指定ユーザーのAdmin権限を付与または剥奪する。
    /// </summary>
    public async Task<bool> ToggleAdminAsync(UserManagementItemViewModel item, bool newIsAdmin)
    {
        if (item == null || item.IsAdmin == newIsAdmin)
        {
            return false;
        }

        if (IsProtectedUser(item.User) && !newIsAdmin)
        {
            _snackbar.Add("保護されたユーザーの管理者権限は剥奪できません。", Severity.Warning);
            return false;
        }

        var result = await _userDataProvider.UpdateUserAdminRoleAsync(item.User.Id, newIsAdmin);
        if (result.Succeeded)
        {
            item.IsAdmin = newIsAdmin;
            _snackbar.Add(
                newIsAdmin ? $"{item.User.UserName} にAdmin権限を付与しました。" : $"{item.User.UserName} のAdmin権限を剥奪しました。",
                Severity.Success);
            return true;
        }

        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
        _snackbar.Add($"権限の変更に失敗しました: {errors}", Severity.Error);
        return false;
    }

    /// <summary>
    ///     指定ユーザーのBAN（利用停止）状態を設定または解除する。
    /// </summary>
    public async Task<bool> ToggleBanAsync(UserManagementItemViewModel item, bool newIsBanned)
    {
        if (item == null || item.IsBanned == newIsBanned)
        {
            return false;
        }

        if (IsProtectedUser(item.User) && newIsBanned)
        {
            _snackbar.Add("保護されたユーザーを利用停止にすることはできません。", Severity.Warning);
            return false;
        }

        var result = await _userDataProvider.SetUserBanStatusAsync(item.User.Id, newIsBanned);
        if (result.Succeeded)
        {
            item.IsBanned = newIsBanned;
            item.User.IsBanned = newIsBanned;
            _snackbar.Add(
                newIsBanned ? $"{item.User.UserName} を利用停止（BAN）にしました。" : $"{item.User.UserName} の利用停止（BAN）を解除しました。",
                newIsBanned ? Severity.Warning : Severity.Success);
            return true;
        }

        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
        _snackbar.Add($"利用停止状態の変更に失敗しました: {errors}", Severity.Error);
        return false;
    }
}