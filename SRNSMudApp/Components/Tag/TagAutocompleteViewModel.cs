namespace SRNSMudApp.Components.Tag;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using SRNSMudApp.Services;

using Tag = SRNSMudApp.Data.Tag;

/// <summary>
///     TagAutocomplete コンポーネントのタグ検索ロジックを仲介する ViewModel。
/// </summary>
public sealed class TagAutocompleteViewModel
{
    private readonly ITagSearchQueryService _tagSearchQueryService;

    public TagAutocompleteViewModel(ITagSearchQueryService tagSearchQueryService)
    {
        _tagSearchQueryService = tagSearchQueryService;
    }

    /// <summary>
    ///     カスタム検索関数が指定されていればそれを呼び出し、無ければフォールバック付きタグ検索を実行する。
    /// </summary>
    public async Task<IEnumerable<Tag>> SearchTagsAsync(
        string? value,
        Func<string?, CancellationToken, Task<IEnumerable<Tag>>>? customSearchFunc,
        CancellationToken token)
    {
        if (customSearchFunc != null)
        {
            return await customSearchFunc(value, token);
        }

        return await _tagSearchQueryService.SearchTagsWithFallbackAsync(value, token);
    }
}