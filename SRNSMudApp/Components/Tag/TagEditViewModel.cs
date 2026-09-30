using System.Diagnostics.CodeAnalysis;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using TagEntity = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Components.Tag;

/// <summary>
///     TagEditDialog の状態管理、ユーザーグループ取得、バリデーション、および更新コマンド実行を担当する ViewModel。
///     UI（Blazor / bUnit）から切り離し、純粋な xUnit + Moq 単体テストを可能にする。
/// </summary>
public class TagEditViewModel
{
    private readonly ITagCommandService _tagCommandService;
    private readonly IUserGroupDataProvider _userGroupData;

    public TagEditViewModel(
        ITagCommandService tagCommandService,
        IUserGroupDataProvider userGroupData)
    {
        _tagCommandService = tagCommandService;
        _userGroupData = userGroupData;
    }

    public TagEntity? Tag { get; private set; }
    public string EditName { get; set; } = string.Empty;
    public string EditContent { get; set; } = string.Empty;
    public IReadOnlyCollection<int> SelectedGroupIds { get; set; } = [];
    public IReadOnlyList<Data.UserGroup> MyGroups { get; private set; } = [];
    public bool IsAdmin { get; private set; }

    public bool CanSave => !string.IsNullOrWhiteSpace(EditName);

    /// <summary>
    ///     編集対象のタグ情報およびユーザー権限を設定し、管理グループ一覧を非同期取得する。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "グループ取得失敗時にもタグ編集自体は継続可能にするため例外を捕捉する")]
    public async Task InitializeAsync(
        TagEntity tag,
        string? currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tag);

        Tag = tag;
        EditName = tag.Name;
        EditContent = tag.Content;
        SelectedGroupIds = tag.AutoApproveUserGroups?.Select(g => g.UserGroupId).ToHashSet() ?? [];
        IsAdmin = isAdmin;

        string? effectiveUserId = currentUserId ?? tag.OwnerId;
        if (!string.IsNullOrEmpty(effectiveUserId))
        {
            try
            {
                var groups = await _userGroupData.GetManagedGroupsAsync(effectiveUserId, cancellationToken);
                MyGroups = groups?.ToList() ?? [];
            }
            catch
            {
                MyGroups = [];
            }
        }
        else
        {
            MyGroups = [];
        }
    }

    /// <summary>
    ///     タグの更新を実行する。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "例外発生時にも画面崩壊を防ぎ、UI にエラー結果を返却するため捕捉する")]
    public async Task<Result<bool>> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (Tag == null)
        {
            return Result.Fail<bool>("対象のタグが指定されていません。");
        }

        if (string.IsNullOrWhiteSpace(EditName))
        {
            return Result.Fail<bool>("タグ名は空にできません。");
        }

        try
        {
            bool updated = await _tagCommandService.UpdateTagAsync(
                Tag.Id,
                EditName,
                EditContent,
                Tag.AutoAcceptIncomingTaggingRequests,
                SelectedGroupIds,
                IsAdmin);

            if (updated)
            {
                return Result.Ok(true);
            }

            return Result.Fail<bool>("対象のタグが見つからないか、ロックされているため更新できません。");
        }
        catch (Exception ex)
        {
            return Result.Fail<bool>($"エラーが発生しました: {ex.Message}");
        }
    }
}