#region

using TagEntity = SRNSMudApp.Data.Tag;

#endregion

namespace SRNSMudApp.Components.Tag;

/// <summary>
///     TagSearch ページ (Tag/TagSearch.razor) の選択状態および表示ロジックを管理する ViewModel。
/// </summary>
public sealed class TagSearchPageViewModel
{
    public TagEntity? SelectedTag { get; set; }

    public bool HasSelectedTag => SelectedTag is not null;

    public string SelectedTagSummary => SelectedTag == null
        ? string.Empty
        : $"選択されたタグ: 名前 = {SelectedTag.Name}, ウェイト = {SelectedTag.CachedWeight}";
}