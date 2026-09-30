using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

#pragma warning disable CA1508

namespace SRNSMudApp.Components.Diagram;

/// <summary>
///     TagDiagramCanvas におけるエッジ生成・タグ関連付けなどのロジックを集約する ViewModel。
///     bUnit を使わずに xUnit で直接単体テスト可能。
/// </summary>
public class TagDiagramCanvasViewModel
{
    private readonly ITagEdgeService _tagEdgeService;

    public TagDiagramCanvasViewModel(ITagEdgeService tagEdgeService)
    {
        _tagEdgeService = tagEdgeService;
    }

    /// <summary>
    ///     エッジ作成条件を検証する（自己ループの禁止等）。
    /// </summary>
    public static (bool CanCreate, string? ErrorMessage) CanCreateEdge(int sourceTagId, int targetTagId)
    {
        if (sourceTagId <= 0 || targetTagId <= 0)
        {
            return (false, "無効なタグIDです。");
        }

        if (sourceTagId == targetTagId)
        {
            return (false, "同一タグ間にエッジを作成することはできません。");
        }

        return (true, null);
    }

    /// <summary>
    ///     タグ間のリレーション（エッジ）を作成する。
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> CreateEdgeAsync(
        int sourceTagId,
        int targetTagId,
        string currentUserId)
    {
        (bool canCreate, string? validationError) = CanCreateEdge(sourceTagId, targetTagId);
        if (!canCreate)
        {
            return (false, validationError);
        }

        if (string.IsNullOrEmpty(currentUserId))
        {
            return (false, "ユーザーが認証されていません。");
        }

        Result<TagEdge> result = await _tagEdgeService.CreateEdgeAsync(sourceTagId, targetTagId, currentUserId);
        return result switch
        {
            Success<TagEdge> => (true, null),
            Failure failure => (false, failure.ErrorMessage),
            _ => (false, "エッジの作成に失敗しました。")
        };
    }
}