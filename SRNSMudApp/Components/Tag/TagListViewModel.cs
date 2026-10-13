#region

using SRNSMudApp.Data;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Components.Tag;

/// <summary>
///     TagList (Tag/TagList.razor) のデータロードと状態管理を担当する ViewModel。
/// </summary>
public sealed class TagListViewModel
{
    private readonly ITagSearchQueryService _tagSearchQueryService;

    public TagListViewModel(ITagSearchQueryService tagSearchQueryService)
    {
        _tagSearchQueryService = tagSearchQueryService ?? throw new ArgumentNullException(nameof(tagSearchQueryService));
    }

    public IReadOnlyList<Data.Tag>? Tags { get; private set; }
    public bool IsLoading { get; private set; }
    public string? CurrentUserId { get; private set; }
    public bool IsAdmin { get; private set; }

    /// <summary>
    ///     閲覧ユーザーのコンテキストを設定する。
    /// </summary>
    public void SetUser(string? currentUserId, bool isAdmin)
    {
        CurrentUserId = currentUserId;
        IsAdmin = isAdmin;
    }

    /// <summary>
    ///     現在のユーザーコンテキストに基づいてタグ一覧を取得する。
    /// </summary>
    public Task LoadDataAsync() => LoadDataCoreAsync();

    /// <summary>
    ///     ユーザー情報を指定してタグ一覧を取得する。
    /// </summary>
    public Task LoadDataAsync(string? currentUserId, bool isAdmin = false)
    {
        SetUser(currentUserId, isAdmin);
        return LoadDataCoreAsync();
    }

    private async Task LoadDataCoreAsync()
    {
        IsLoading = true;
        try
        {
            Tags = await _tagSearchQueryService.GetTagsWithDetailsAsync(CurrentUserId, IsAdmin);
        }
        finally
        {
            IsLoading = false;
        }
    }
}