namespace SRNSMudApp.Components.Item;

using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using SRNSMudApp.Models;

/// <summary>
///     タグ検索バーコンポーネントのコードビハインド。
///     フィルタチップ表示、サジェスト選択、検索テキスト入力を制御する。
/// </summary>
public partial class TagSearchBar : ComponentBase
{
    [Parameter]
    public TagSearchViewModel ViewModel { get; set; } = null!;

    private MudAutocomplete<TagSuggestion>? _autocomplete;
    private string _searchText = string.Empty;

    private async Task OnSuggestionSelected(TagSuggestion? suggestion)
    {
        if (suggestion == null)
        {
            return;
        }

        _ = await ViewModel.AddFilterFromSuggestionAsync(suggestion);
        _searchText = string.Empty;
        if (_autocomplete != null)
        {
            await _autocomplete.ClearAsync();
        }
    }

    private async Task OnAdornmentClick()
    {
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            _ = await ViewModel.AddFilterFromTextAsync(_searchText);
            _searchText = string.Empty;
            if (_autocomplete != null)
            {
                await _autocomplete.ClearAsync();
            }
        }
    }
}