using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Components.Tag;

/// <summary>
///     タグ付けリクエスト（承認・却下・キャンセル・返信）の操作ロジックを集約する ViewModel。
///     bUnit を使わずに直接単体テスト可能。
/// </summary>
public class TaggingRequestActionViewModel
{
    private readonly ITaggingRequestActions _actions;
    private readonly ITaggingContractService _contractService;

    public TaggingRequestActionViewModel(
        ITaggingRequestActions actions,
        ITaggingContractService contractService)
    {
        _actions = actions;
        _contractService = contractService;
    }

    /// <summary>
    ///     リクエストを承認できるかどうかを判定する。
    /// </summary>
    public bool CanApprove(TaggingRequestEntity request, string currentUserId) =>
        _actions.CanApprove(request, currentUserId);

    /// <summary>
    ///     リクエストを承認する。
    /// </summary>
    public async Task<bool> ApproveRequestAsync(int requestId, string currentUserId)
    {
        if (string.IsNullOrEmpty(currentUserId))
        {
            return false;
        }

        return await _actions.ApproveAsync(requestId, currentUserId);
    }

    /// <summary>
    ///     リクエストを却下する。
    /// </summary>
    public async Task<bool> RejectRequestAsync(int requestId, string currentUserId)
    {
        if (string.IsNullOrEmpty(currentUserId))
        {
            return false;
        }

        return await _actions.RejectViaDialogAsync(requestId, currentUserId);
    }

    /// <summary>
    ///     申請者本人がリクエストをキャンセルする。
    /// </summary>
    public async Task<bool> CancelRequestAsync(TaggingRequestEntity request, string currentUserId)
    {
        if (request == null || string.IsNullOrEmpty(currentUserId))
        {
            return false;
        }

        if (request.RequesterUserId != currentUserId)
        {
            return false; // 本人のみキャンセル可能
        }

        var result = await _contractService.CancelContractAsync(request.Id, currentUserId);
        return result is Models.Unions.Success<string>;
    }
}