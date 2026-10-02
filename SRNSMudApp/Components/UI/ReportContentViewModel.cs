#region

using SRNSMudApp.Data;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Components.UI;

/// <summary>
///     不適切コンテンツ通報ダイアログ用 ViewModel。
///     通報理由・詳細の入力状態管理、バリデーション、および通報リクエストの送信をカプセル化する。
/// </summary>
public sealed class ReportContentViewModel
{
    private readonly IContentReportService _contentReportService;

    public ReportContentViewModel(IContentReportService contentReportService)
    {
        _contentReportService = contentReportService ?? throw new ArgumentNullException(nameof(contentReportService));
    }

    public ReportTargetType TargetType { get; private set; }
    public int? ItemId { get; private set; }
    public int? TagId { get; private set; }
    public string TargetContent { get; private set; } = string.Empty;
    public string? TargetOwnerName { get; private set; }
    public string CurrentUserId { get; set; } = string.Empty;

    public string SelectedReason { get; set; } = "スパム・宣伝目的";
    public string Detail { get; set; } = string.Empty;
    public bool IsSubmitting { get; private set; }

    public bool CanSubmit =>
        !IsSubmitting &&
        !string.IsNullOrWhiteSpace(SelectedReason) &&
        !string.IsNullOrWhiteSpace(CurrentUserId);

    /// <summary>
    ///     通報対象および現在のユーザーIDを設定して初期化する。
    /// </summary>
    public void Initialize(
        ReportTargetType targetType,
        int? itemId,
        int? tagId,
        string targetContent,
        string? targetOwnerName,
        string currentUserId)
    {
        TargetType = targetType;
        ItemId = itemId;
        TagId = tagId;
        TargetContent = targetContent ?? string.Empty;
        TargetOwnerName = targetOwnerName;
        CurrentUserId = currentUserId ?? string.Empty;
        SelectedReason = "スパム・宣伝目的";
        Detail = string.Empty;
        IsSubmitting = false;
    }

    /// <summary>
    ///     通報リクエストを送信する。
    /// </summary>
    public async Task<ContentReport> SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
        {
            throw new InvalidOperationException("通報するにはログインが必要です。");
        }

        if (string.IsNullOrWhiteSpace(SelectedReason))
        {
            throw new InvalidOperationException("通報の理由を選択してください。");
        }

        IsSubmitting = true;
        try
        {
            var dto = new CreateContentReportDto
            {
                TargetType = TargetType,
                ItemId = ItemId,
                TagId = TagId,
                Reason = SelectedReason.Trim(),
                Detail = Detail.Trim()
            };

            return await _contentReportService.CreateReportAsync(dto, CurrentUserId);
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}