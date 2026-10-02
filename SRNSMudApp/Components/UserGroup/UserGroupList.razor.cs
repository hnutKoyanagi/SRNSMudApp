namespace SRNSMudApp.Components.UserGroup;

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;

/// <summary>
///     ユーザーグループ一覧画面コンポーネントのコードビハインド。
///     所属グループ一覧の表示、新規作成・編集・メンバー管理ダイアログの表示、グループ削除を制御する。
///     データ取得・永続化処理は <see cref="UserGroupListViewModel"/> に委譲する。
/// </summary>
public partial class UserGroupList : ComponentBase
{
    [Inject] private UserGroupListViewModel ViewModel { get; set; } = null!;
    [Inject] private IDialogLauncher DialogLauncher { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private IReadOnlyList<UserGroup> Groups => ViewModel.Groups;
    private string CurrentUserId => ViewModel.CurrentUserId;
    private bool IsLoading => ViewModel.IsLoading;

    protected override async Task OnInitializedAsync()
    {
        if (AuthState is not null)
        {
            var auth = await AuthState;
            ViewModel.CurrentUserId = auth.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }

        await ViewModel.LoadGroupsAsync();
    }

    private async Task OpenCreateDialogAsync()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return;
        }

        var dialog = await DialogLauncher.ShowAsync<UserGroupCreateEditDialog>("新規グループ作成", ViewModel.CreateDialogParameters());
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            await ViewModel.LoadGroupsAsync();
        }
    }

    private async Task OpenEditDialogAsync(UserGroup group)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return;
        }

        var dialog = await DialogLauncher.ShowAsync<UserGroupCreateEditDialog>("グループを編集", ViewModel.EditDialogParameters(group));
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            await ViewModel.LoadGroupsAsync();
        }
    }

    private async Task OpenMembersDialogAsync(UserGroup group)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return;
        }

        var dialog = await DialogLauncher.ShowAsync<UserGroupMembersDialog>("メンバー管理", ViewModel.MembersDialogParameters(group));
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            await ViewModel.LoadGroupsAsync();
        }
    }

    private async Task DeleteGroupAsync(UserGroup group)
    {
        var result = await ViewModel.DeleteGroupAsync(group.Id);
        switch (result)
        {
            case Success<bool>:
                Snackbar.Add($"グループ「{group.Name}」を削除しました。", Severity.Success);
                break;
            case Failure fail:
                Snackbar.Add(fail.ErrorMessage, Severity.Error);
                break;
            default:
                break;
        }
    }
}