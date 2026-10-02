using System.Diagnostics.CodeAnalysis;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Commands;

namespace SRNSMudApp.Components.PublicOffer;

/// <summary>
///     TriggerPublicOfferDialog の状態管理、アセット取得、アイテム検索、および契約作成ロジックを担当する ViewModel。
///     UI（Blazor / bUnit）から切り離し、純粋な xUnit + Moq 単体テストを可能にする。
/// </summary>
public class TriggerPublicOfferViewModel
{
    private readonly IContractLookupDataProvider _contractData;
    private readonly ICommandHandler<CreateTriggerContractCommand, Result<bool>> _createTriggerContractHandler;

    public TriggerPublicOfferViewModel(
        IContractLookupDataProvider contractData,
        ICommandHandler<CreateTriggerContractCommand, Result<bool>> createTriggerContractHandler)
    {
        _contractData = contractData;
        _createTriggerContractHandler = createTriggerContractHandler;
    }

    public PublicTradeOffer? Offer { get; set; }
    public string CurrentUserId { get; set; } = string.Empty;
    public Data.Item? SelectedItem { get; set; }
    public RightAsset? SelectedAsset { get; set; }
    public IReadOnlyList<RightAsset> ValidAssets { get; private set; } = [];

    public bool CanSubmit =>
        Offer != null &&
        SelectedItem != null &&
        (Offer.RequiredAssetAmount <= 0 || (SelectedAsset != null && ValidAssets.Count > 0));

    /// <summary>
    ///     オファーに応じるために必要なアセット一覧を非同期取得する。
    /// </summary>
    public async Task LoadMyAssetsAsync(CancellationToken cancellationToken = default)
    {
        if (Offer == null || string.IsNullOrEmpty(CurrentUserId))
        {
            ValidAssets = [];
            return;
        }

        var assets = await _contractData.GetValidRightAssetsAsync(
            CurrentUserId, Offer.OfferedTagId, Offer.RequiredAssetAmount);
        ValidAssets = assets ?? [];
    }

    /// <summary>
    ///     タグを付与する対象のアイテムを非同期検索する。
    /// </summary>
    public async Task<IEnumerable<Data.Item>> SearchItemsAsync(string? value, CancellationToken cancellationToken = default)
    {
        return await _contractData.SearchItemsAsync(value, cancellationToken);
    }

    /// <summary>
    ///     Trigger 型の TaggingRequestEntity を作成し、コマンドを実行する。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "例外発生時にも画面崩壊を防ぎ、UI にエラー結果を返却するため捕捉する")]
    [SuppressMessage("Style", "CA1508:Avoid dead code",
        Justification = "Result<bool> は Success<bool> または Failure の Union 型であり、ハンドラの実装に応じて両方のパスが実行される")]
    public async Task<Result<int>> SubmitAsync(CancellationToken cancellationToken = default)
    {
        if (Offer == null)
        {
            return Result.Fail<int>("オファーが指定されていません。");
        }

        if (SelectedItem == null)
        {
            return Result.Fail<int>("アイテムを選択してください。");
        }

        if (Offer.RequiredAssetAmount > 0 && SelectedAsset == null)
        {
            return Result.Fail<int>("要求量以上のアセットを選択してください。");
        }

        try
        {
            var triggerContract = new TaggingRequestEntity
            {
                ContractType = "Trigger",
                OwnerId = CurrentUserId,
                RequesterUserId = CurrentUserId,
                TagOwnerUserId = Offer.OwnerId,
                TargetItemId = SelectedItem.Id,
                RequestedTagId = Offer.OfferedTagId,
                Payload = new PublicOfferPayload(Offer.Id),
                ConsumedRightAssetId = SelectedAsset?.Id
            };

            var commandResult = await _createTriggerContractHandler.HandleAsync(
                new CreateTriggerContractCommand(triggerContract), cancellationToken);

            if (commandResult is Failure failure)
            {
                return Result.Fail<int>(failure.ErrorMessage);
            }

            return Result.Ok(triggerContract.Id);
        }
        catch (Exception ex)
        {
            return Result.Fail<int>($"契約の作成に失敗しました: {ex.Message}");
        }
    }
}