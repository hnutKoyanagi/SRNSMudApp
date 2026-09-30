// IDE0010: union 型・enum の網羅的 switch に対する「Populate switch」は、
// 全ケース列挙済み・default 併記済みでも解消されない解析器の誤検知のため抑制する。
#pragma warning disable IDE0010, CA1508

namespace SRNSMudApp.Components.Tag;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

/// <summary>
///     ItemTagTable のコードビハインド。
///     マークアップ (.razor) 側は表示のみを担い、サジェストとテーブルフィルタのイベント処理はこちらに集約する。
/// </summary>
public partial class ItemTagTable
{
    [Parameter] public IEnumerable<TagRelation> TagRelations { get; set; } = [];
    [Parameter] public Data.Item Item { get; set; } = null!;
    [Parameter] public string CurrentUserId { get; set; } = "";
    [Parameter] public IReadOnlyList<Data.Tag> AllTags { get; set; } = [];
    [Parameter] public IReadOnlyList<TagRelationToTag> AllTagRelationsToTags { get; set; } = [];
    [Parameter] public EventCallback OnDataChanged { get; set; }
    [Parameter] public string? SearchString { get; set; }
    [Parameter] public EventCallback<string?> SearchStringChanged { get; set; }

    [Inject] private ItemTagTableViewModel ViewModel { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private ApplicationUser? _selectedTargetUser;

    private MudAutocomplete<string>? _autocomplete;
    private string _searchString = "";

    protected override void OnParametersSet()
    {
        if (SearchString != null && SearchString != _searchString)
        {
            _searchString = SearchString;
        }
        else if (SearchString == null && !string.IsNullOrEmpty(_searchString) && SearchStringChanged.HasDelegate)
        {
            _searchString = "";
        }
    }

    private async Task HandleValueChangedAsync(string? value)
    {
        _searchString = value ?? "";
        await SearchStringChanged.InvokeAsync(string.IsNullOrWhiteSpace(_searchString) ? null : _searchString);

        switch (TagSearchQuery.Parse(_searchString))
        {
            case IncompleteSearch:
                _ = ReopenMenuAfterDelayAsync();
                break;
            default:
                break;
        }
    }

    private async Task ReopenMenuAfterDelayAsync()
    {
        await Task.Delay(100);
        await InvokeAsync(async () =>
        {
            switch (_autocomplete)
            {
                case not null:
                    await _autocomplete.FocusAsync();
                    await _autocomplete.ToggleMenuAsync();
                    StateHasChanged();
                    break;
                default:
                    break;
            }
        });
    }

    private Task<IEnumerable<string>> SearchSuggestionsAsync(string? value, CancellationToken _)
    {
        return Task.FromResult<IEnumerable<string>>(
            ItemTagTableViewModel.GetSearchSuggestions(TagRelations, value));
    }

    private bool FilterFunc(TagRelation relation) =>
        ItemTagTableViewModel.FilterFunc(relation, _searchString);

    private async Task<IEnumerable<ApplicationUser>> SearchUsersAsync(string? value, CancellationToken token)
    {
        return await ViewModel.SearchUsersAsync(value, token);
    }

    private async Task RequestSelectedTagAddAsync()
    {
        var result = await ViewModel.RequestSelectedTagAddAsync(
            _selectedTargetUser,
            CurrentUserId,
            _searchString,
            AllTags,
            Item.Id);

        Snackbar.Add(result.Message, result.Severity);
        if (result.ShouldNotifyChanged)
        {
            await OnDataChanged.InvokeAsync();
        }
    }
}