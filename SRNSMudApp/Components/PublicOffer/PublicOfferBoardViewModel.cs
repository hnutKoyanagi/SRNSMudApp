#region

using MudBlazor;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Components.PublicOffer;

/// <summary>
///     公開オファー掲示板画面用 ViewModel。
///     アクティブな公開オファー一覧の読み込み、取り下げ、オファー成立後の契約承認・エラー時キャンセル処理を担当する。
/// </summary>
public sealed class PublicOfferBoardViewModel
{
    private readonly IPublicOfferDataProvider _contractData;
    private readonly ITaggingContractService _contractService;

    public PublicOfferBoardViewModel(
        IPublicOfferDataProvider contractData,
        ITaggingContractService contractService)
    {
        _contractData = contractData ?? throw new ArgumentNullException(nameof(contractData));
        _contractService = contractService ?? throw new ArgumentNullException(nameof(contractService));
    }

    public IReadOnlyList<PublicTradeOffer> Offers { get; private set; } = [];
    public string CurrentUserId { get; set; } = string.Empty;
    public bool IsLoading { get; private set; } = true;

    /// <summary>
    ///     アクティブな公開オファー一覧を取得する。
    /// </summary>
    public async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            var offers = await _contractData.GetActivePublicOffersAsync();
            Offers = offers ?? [];
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    ///     指定されたオファーを取り下げる（非アクティブ化する）。
    /// </summary>
    public async Task<Result<bool>> DeactivateOfferAsync(PublicTradeOffer offer)
    {
        if (offer == null)
        {
            return new Failure("対象のオファーが指定されていません。");
        }

        if (string.IsNullOrWhiteSpace(CurrentUserId))
        {
            return new Failure("ユーザー情報が取得できませんでした。");
        }

        bool success = await _contractData.DeactivatePublicOfferAsync(offer.Id, CurrentUserId);
        if (success)
        {
            await LoadDataAsync();
            return new Success<bool>(true);
        }

        return new Failure("オファーの取り下げに失敗しました。");
    }

    /// <summary>
    ///     ダイアログで作成された契約を承認し、エラー発生時は契約をキャンセルする。
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Fallback compensation logic")]
    public async Task<Result<bool>> AcceptTriggeredContractAsync(int contractId)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
        {
            return new Failure("ユーザー情報が取得できませんでした。");
        }

        try
        {
            await _contractService.AcceptContractAsync(contractId, CurrentUserId);
            await LoadDataAsync();
            return new Success<bool>(true);
        }
        catch (Exception ex)
        {
            try
            {
                await _contractService.CancelContractAsync(contractId, CurrentUserId);
            }
            catch
            {
                // キャンセル自体のエラーは主因のエラーメッセージを優先するため握りつぶす
            }

            await LoadDataAsync();
            return new Failure($"エラーが発生しました: {ex.Message}");
        }
    }

    /// <summary>
    ///     現在のユーザーがオファーの所有者であるかを判定する。
    /// </summary>
    public bool IsOwner(PublicTradeOffer? offer)
    {
        return offer != null && !string.IsNullOrWhiteSpace(CurrentUserId) && offer.OwnerId == CurrentUserId;
    }

    /// <summary>
    ///     オファー作成ダイアログ用のオプションを生成する。
    /// </summary>
    public static DialogOptions CreateDialogOptions() => new()
    {
        CloseOnEscapeKey = true,
        MaxWidth = MaxWidth.Small,
        FullWidth = true
    };

    /// <summary>
    ///     オファー応諾ダイアログ用のパラメータを生成する。
    /// </summary>
    public static DialogParameters<TriggerPublicOfferDialog> TriggerDialogParameters(PublicTradeOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return new DialogParameters<TriggerPublicOfferDialog>
        {
            { x => x.Offer, offer }
        };
    }

    /// <summary>
    ///     オファー応諾ダイアログ用のオプションを生成する。
    /// </summary>
    public static DialogOptions TriggerDialogOptions() => new()
    {
        CloseOnEscapeKey = true,
        MaxWidth = MaxWidth.Small,
        FullWidth = true
    };
}