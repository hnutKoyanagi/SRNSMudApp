#region

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Identity;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Components.Account.Pages.Debug;

/// <summary>
///     RequireConfirmedAccount (Admin デバッグ用) のユーザー一覧取得およびメール確認状態トグルを担当する ViewModel。
/// </summary>
public sealed class RequireConfirmedAccountViewModel
{
    private readonly IUserDataProvider _userDataProvider;
    private readonly UserManager<ApplicationUser> _userManager;

    public RequireConfirmedAccountViewModel(
        IUserDataProvider userDataProvider,
        UserManager<ApplicationUser> userManager)
    {
        _userDataProvider = userDataProvider ?? throw new ArgumentNullException(nameof(userDataProvider));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    public IReadOnlyList<ApplicationUser>? Users { get; private set; }
    public bool IsLoading { get; private set; }

    /// <summary>
    ///     全ユーザーの一覧を読み込みます。
    /// </summary>
    public async Task LoadUsersAsync()
    {
        IsLoading = true;
        try
        {
            Users = await _userDataProvider.GetAllUsersAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    ///     指定したユーザーの EmailConfirmed 状態を反転させて保存します。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "更新結果をResult型で返却するため")]
    public async Task<Result<bool>> ToggleConfirmationAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            user.EmailConfirmed = !user.EmailConfirmed;
            IdentityResult result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                await LoadUsersAsync();
                return new Success<bool>(user.EmailConfirmed);
            }

            return new Failure("Failed to update user");
        }
        catch (Exception ex)
        {
            return new Failure($"更新に失敗しました: {ex.Message}");
        }
    }
}