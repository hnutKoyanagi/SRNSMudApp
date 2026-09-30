#region

using MudBlazor;

using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using UserGroupEntity = SRNSMudApp.Data.UserGroup;

#endregion

namespace SRNSMudApp.Components.UserGroup;

/// <summary>
///     ユーザーグループ一覧画面用 ViewModel。
///     グループ一覧の取得、削除、および各ダイアログ起動パラメータの生成を担当する。
/// </summary>
public sealed class UserGroupListViewModel
{
    private readonly IUserGroupDataProvider _userGroupDataProvider;

    public UserGroupListViewModel(IUserGroupDataProvider userGroupDataProvider)
    {
        _userGroupDataProvider = userGroupDataProvider ?? throw new ArgumentNullException(nameof(userGroupDataProvider));
    }

    public IReadOnlyList<UserGroupEntity> Groups { get; private set; } = [];
    public string CurrentUserId { get; set; } = string.Empty;
    public bool IsLoading { get; private set; } = true;

    /// <summary>
    ///     現在のユーザーが所属・管理するグループ一覧を取得する。
    /// </summary>
    public async Task LoadGroupsAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId))
            {
                Groups = [];
                return;
            }

            var groups = await _userGroupDataProvider.GetUserGroupsForUserAsync(CurrentUserId, cancellationToken);
            Groups = groups?.ToList() ?? [];
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    ///     指定されたグループを削除する。
    /// </summary>
    public async Task<Result<bool>> DeleteGroupAsync(int groupId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
        {
            return new Failure("ユーザー情報が取得できませんでした。");
        }

        bool deleted = await _userGroupDataProvider.DeleteUserGroupAsync(groupId, CurrentUserId, cancellationToken);
        if (deleted)
        {
            await LoadGroupsAsync(cancellationToken);
            return new Success<bool>(true);
        }

        return new Failure("グループの削除に失敗しました。");
    }

    /// <summary>
    ///     現在のユーザーがグループのオーナーであるかを判定する。
    /// </summary>
    public bool IsOwner(UserGroupEntity? group)
    {
        return group != null && !string.IsNullOrWhiteSpace(CurrentUserId) && group.OwnerId == CurrentUserId;
    }

    /// <summary>
    ///     新規グループ作成ダイアログ用のパラメータを生成する。
    /// </summary>
    public DialogParameters CreateDialogParameters() => new()
    {
        { nameof(UserGroupCreateEditDialog.CurrentUserId), CurrentUserId }
    };

    /// <summary>
    ///     グループ編集ダイアログ用のパラメータを生成する。
    /// </summary>
    public DialogParameters EditDialogParameters(UserGroupEntity group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return new DialogParameters
        {
            { nameof(UserGroupCreateEditDialog.Group), group },
            { nameof(UserGroupCreateEditDialog.CurrentUserId), CurrentUserId }
        };
    }

    /// <summary>
    ///     グループメンバー管理ダイアログ用のパラメータを生成する。
    /// </summary>
    public DialogParameters MembersDialogParameters(UserGroupEntity group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return new DialogParameters
        {
            { nameof(UserGroupMembersDialog.Group), group },
            { nameof(UserGroupMembersDialog.CurrentUserId), CurrentUserId }
        };
    }
}