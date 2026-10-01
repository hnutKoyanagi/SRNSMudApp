namespace SRNSMudApp.Components.Tag;

using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

/// <summary>
///     タグ一覧ページコンポーネントのコードビハインド。
///     タグ一覧データの初期ロードおよび更新を制御する。
/// </summary>
public partial class TagList : ComponentBase
{
    [Inject]
    private TagListViewModel ViewModel { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        await ViewModel.LoadDataAsync();
    }
}