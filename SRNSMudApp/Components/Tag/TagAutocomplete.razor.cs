namespace SRNSMudApp.Components.Tag;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using SRNSMudApp.Data;

/// <summary>
///     タグオートコンプリート入力コンポーネントのコードビハインド。
///     タグ検索（フォールバックまたはカスタム）、値変更通知、表示スタイルを制御する。
/// </summary>
public partial class TagAutocomplete : ComponentBase
{
    [Parameter]
    public Tag? Value { get; set; }

    [Parameter]
    public EventCallback<Tag?> ValueChanged { get; set; }

    [Parameter]
    public string Label { get; set; } = "タグを検索";

    [Parameter]
    public string Placeholder { get; set; } = "タグ名 または 内容を入力...";

    [Parameter]
    public Variant Variant { get; set; } = Variant.Text;

    [Parameter]
    public bool Dense { get; set; }

    [Parameter]
    public Margin Margin { get; set; } = Margin.None;

    [Parameter]
    public bool Clearable { get; set; } = true;

    [Parameter]
    public bool ResetValueOnEmptyText { get; set; } = true;

    [Parameter]
    public int MaxItems { get; set; } = 20;

    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public string? Style { get; set; }

    [Parameter]
    public Func<string?, CancellationToken, Task<IEnumerable<Tag>>>? CustomSearchFunc { get; set; }

    [Parameter]
    public RenderFragment<Tag>? ItemTemplate { get; set; }

    [Inject]
    private TagAutocompleteViewModel ViewModel { get; set; } = null!;

    private async Task OnValueChanged(Tag? tag)
    {
        Value = tag;
        await ValueChanged.InvokeAsync(tag);
    }

    private async Task<IEnumerable<Tag>> SearchTagsAsync(string? value, CancellationToken token)
    {
        return await ViewModel.SearchTagsAsync(value, CustomSearchFunc, token);
    }
}