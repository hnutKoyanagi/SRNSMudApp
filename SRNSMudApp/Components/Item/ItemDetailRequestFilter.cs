#pragma warning disable CA1508

using SRNSMudApp.Data;
using SRNSMudApp.Models;

namespace SRNSMudApp.Components.Item;

/// <summary>
///     ItemDetail の「タグ付与依頼 (関連リクエスト)」に対する純粋フィルタリングロジック。
/// </summary>
public static class ItemDetailRequestFilter
{
    /// <summary>
    ///     リクエスト一覧を「自分のリクエストのみ」および「付けられたタグの検索条件」で絞り込む。
    /// </summary>
    public static IEnumerable<TaggingRequestEntity> FilterRequests(
        IEnumerable<TaggingRequestEntity>? requests,
        string? currentUserId,
        bool onlyMyRequests,
        string? searchQuery,
        IReadOnlyList<Data.Tag>? allTags = null)
    {
        if (requests == null)
        {
            return [];
        }

        IEnumerable<TaggingRequestEntity> query = requests;

        if (onlyMyRequests && !string.IsNullOrEmpty(currentUserId))
        {
            query = query.Where(r => r.RequesterUserId == currentUserId || r.OwnerId == currentUserId);
        }

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            TagSearchQuery parsed = TagSearchQuery.Parse(searchQuery);
            query = query.Where(r => MatchesSearch(r, parsed, allTags));
        }

        return query;
    }

    /// <summary>
    ///     タグ検索クエリとリクエストの一致判定を行う。
    /// </summary>
    public static bool MatchesSearch(TaggingRequestEntity request, TagSearchQuery parsed, IReadOnlyList<Data.Tag>? allTags = null)
    {
        string? tagName = request.RequestedTag?.Name;
        if (string.IsNullOrEmpty(tagName) && allTags != null && request.RequestedTagId > 0)
        {
            tagName = allTags.FirstOrDefault(t => t.Id == request.RequestedTagId)?.Name;
        }

        return parsed switch
        {
            EmptySearch => true,
            IncompleteSearch incomplete =>
                tagName?.Equals(incomplete.TagName, StringComparison.OrdinalIgnoreCase) == true
                || tagName?.Contains(incomplete.TagName, StringComparison.OrdinalIgnoreCase) == true,
            TagWithUserSearch tagWithUser =>
                (tagName?.Equals(tagWithUser.TagName, StringComparison.OrdinalIgnoreCase) == true
                 || tagName?.Contains(tagWithUser.TagName, StringComparison.OrdinalIgnoreCase) == true)
                && MatchUser(request, tagWithUser.UserName),
            TagNameSearch tagNameSearch =>
                tagName?.Contains(tagNameSearch.TagName, StringComparison.OrdinalIgnoreCase) == true
                || MatchUser(request, tagNameSearch.TagName),
            _ => true
        };
    }

    private static bool MatchUser(TaggingRequestEntity request, string userName)
    {
        return request.Owner?.UserName?.Contains(userName, StringComparison.OrdinalIgnoreCase) == true
               || request.RequestedTag?.Owner?.UserName?.Contains(userName, StringComparison.OrdinalIgnoreCase) == true;
    }
}