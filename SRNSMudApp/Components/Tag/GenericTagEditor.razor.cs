namespace SRNSMudApp.Components.Tag;

using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using SRNSMudApp.Data;
using SRNSMudApp.Services;

/// <summary>
///     汎用タグエディタコンポーネントのコードビハインド。
///     エンティティに対するタグの追加・削除処理および変更通知イベントの発火を制御する。
/// </summary>
/// <typeparam name="TItem">タグ付け可能なエンティティ型。</typeparam>
public partial class GenericTagEditor<TItem> : ComponentBase
    where TItem : class, IDirectTaggable
{
    [Parameter]
    [EditorRequired]
    public TItem TargetEntity { get; set; } = null!;

    [Parameter]
    public EventCallback OnTagsChanged { get; set; }

    [Inject]
    private GenericTagEditorViewModel ViewModel { get; set; } = null!;

    private async Task AddTagAsync()
    {
        var success = await ViewModel.AddTagAsync<TItem>(TargetEntity.Id);
        if (success && OnTagsChanged.HasDelegate)
        {
            await OnTagsChanged.InvokeAsync();
        }
    }

    private async Task RemoveTagAsync(int tagId)
    {
        var success = await ViewModel.RemoveTagAsync<TItem>(TargetEntity.Id, tagId);
        if (success && OnTagsChanged.HasDelegate)
        {
            await OnTagsChanged.InvokeAsync();
        }
    }
}