#region

using System.Diagnostics.CodeAnalysis;

using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Components.Admin;

/// <summary>
///     TagManagement コンポーネントの表示・状態管理・ロック操作を担当する ViewModel。
///     UI への依存を持たないため、単体テストで高速にテスト可能。
/// </summary>
public sealed class TagManagementViewModel
{
    private readonly ITagLockService _tagLockService;

    public TagManagementViewModel(ITagLockService tagLockService)
    {
        _tagLockService = tagLockService ?? throw new ArgumentNullException(nameof(tagLockService));
    }

    public int CurrentConfiguredLevel { get; private set; }
    public int InputLevel { get; set; }
    public bool IsSavingSetting { get; private set; }
    public bool IsLoading { get; private set; } = true;
    public string? SearchString { get; set; }
    public IReadOnlyList<TagLockItemDto> Tags { get; private set; } = [];

    public IEnumerable<TagLockItemDto> FilteredTags => FilterTags(Tags, SearchString);

    /// <summary>
    ///     階層ロック設定およびタグ一覧をロードします。
    /// </summary>
    public async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            CurrentConfiguredLevel = await _tagLockService.GetLockedHierarchyLevelAsync(cancellationToken);
            InputLevel = CurrentConfiguredLevel;
            Tags = await _tagLockService.GetAllTagsWithLockStatusAsync(cancellationToken);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    ///     階層ロック設定を保存します。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "操作結果をResult型でUIに返却するため")]
    public async Task<Result<int>> SaveHierarchyLockSettingAsync(CancellationToken cancellationToken = default)
    {
        IsSavingSetting = true;
        try
        {
            int savedLevel = InputLevel;
            await _tagLockService.SetLockedHierarchyLevelAsync(savedLevel, cancellationToken);
            await LoadDataAsync(cancellationToken);
            return new Success<int>(savedLevel);
        }
        catch (Exception ex)
        {
            return new Failure($"設定の保存に失敗しました: {ex.Message}");
        }
        finally
        {
            IsSavingSetting = false;
        }
    }

    /// <summary>
    ///     タグの個別ロックをトグルします。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "操作結果をResult型でUIに返却するため")]
    public async Task<Result<bool>> ToggleTagLockAsync(int tagId, CancellationToken cancellationToken = default)
    {
        try
        {
            bool isLocked = await _tagLockService.ToggleTagLockAsync(tagId, cancellationToken);
            await LoadDataAsync(cancellationToken);
            return new Success<bool>(isLocked);
        }
        catch (Exception ex)
        {
            return new Failure($"ロック操作に失敗しました: {ex.Message}");
        }
    }

    /// <summary>
    ///     検索文字列に基づいてタグ一覧をフィルタリングする。
    ///     タグ名、親タグ名、または作成者ユーザー名に部分一致するものを抽出する。
    /// </summary>
    /// <param name="tags">対象のタグ一覧。</param>
    /// <param name="searchString">検索キーワード。</param>
    /// <returns>フィルタリングされたタグ列挙。</returns>
    public static IEnumerable<TagLockItemDto> FilterTags(IEnumerable<TagLockItemDto> tags, string? searchString)
    {
        ArgumentNullException.ThrowIfNull(tags);

        if (string.IsNullOrWhiteSpace(searchString))
        {
            return tags;
        }

        return tags.Where(t =>
            t.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
            (t.ParentTagName?.Contains(searchString, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (t.OwnerUserName?.Contains(searchString, StringComparison.OrdinalIgnoreCase) ?? false));
    }
}