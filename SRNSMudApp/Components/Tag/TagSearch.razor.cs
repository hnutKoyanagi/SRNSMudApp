namespace SRNSMudApp.Components.Tag;

using Microsoft.AspNetCore.Components;

/// <summary>
///     タグ検索ページコンポーネントのコードビハインド。
///     タグ検索と選択されたタグの要約情報表示を制御する。
/// </summary>
public partial class TagSearch : ComponentBase
{
    [Inject]
    private TagSearchPageViewModel ViewModel { get; set; } = null!;
}