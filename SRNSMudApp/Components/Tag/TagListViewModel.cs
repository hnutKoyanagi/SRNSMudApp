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

    public async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            Tags = await _tagSearchQueryService.GetTagsWithDetailsAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }
}