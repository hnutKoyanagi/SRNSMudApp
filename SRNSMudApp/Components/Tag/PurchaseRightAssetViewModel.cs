using System.Diagnostics.CodeAnalysis;

using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using TagEntity = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Components.Tag;

/// <summary>
///     PurchaseRightAssetDialog の状態管理、ネットワーク選択、受取ウォレット取得、シミュレーション、および購入ロジックを担当する ViewModel。
///     UI（Blazor / bUnit）から切り離し、純粋な xUnit + Moq 単体テストを可能にする。
/// </summary>
public class PurchaseRightAssetViewModel
{
    private readonly IRightAssetPurchaseService _purchaseService;

    public PurchaseRightAssetViewModel(IRightAssetPurchaseService purchaseService)
    {
        _purchaseService = purchaseService;
    }

    public TagEntity? RequestedTag { get; set; }
    public string CurrentUserId { get; set; } = string.Empty;

    public int Amount { get; set; } = 1;
    public int UnitPriceJpyc { get; set; } = 100;
    public string SelectedNetworkName { get; set; } = "polygon-amoy";
    public string TransactionHash { get; set; } = string.Empty;
    public string DepositAddress { get; private set; } = string.Empty;

    public IReadOnlyList<JpycNetworkInfo> Networks { get; private set; } = [];
    public bool IsLoadingWallet { get; private set; } = true;
    public bool IsSubmitting { get; private set; }

    public int TotalJpyc => Amount * UnitPriceJpyc;

    public JpycNetworkInfo? CurrentNetwork =>
        Networks.FirstOrDefault(n => n.Name == SelectedNetworkName) ?? (Networks.Count > 0 ? Networks[0] : null);

    public bool CanSubmit =>
        !IsSubmitting &&
        !IsLoadingWallet &&
        Amount >= 1 &&
        UnitPriceJpyc >= 1 &&
        !string.IsNullOrWhiteSpace(TransactionHash) &&
        !string.IsNullOrWhiteSpace(CurrentUserId) &&
        RequestedTag != null;

    /// <summary>
    ///     ダイアログ初期化パラメータを設定し、ネットワーク一覧および受取ウォレットを取得する。
    /// </summary>
    public async Task InitializeAsync(
        TagEntity tag,
        string currentUserId,
        int defaultAmount = 1,
        int defaultUnitPriceJpyc = 100,
        CancellationToken cancellationToken = default)
    {
        RequestedTag = tag;
        CurrentUserId = currentUserId;
        Amount = defaultAmount > 0 ? defaultAmount : 1;
        UnitPriceJpyc = defaultUnitPriceJpyc > 0 ? defaultUnitPriceJpyc : 100;

        Networks = _purchaseService.GetSupportedNetworks();
        var recommended = Networks.FirstOrDefault(n => n.IsRecommended);
        if (recommended != null)
        {
            SelectedNetworkName = recommended.Name;
        }
        else if (Networks.Count > 0)
        {
            SelectedNetworkName = Networks[0].Name;
        }

        await LoadDepositWalletAsync(cancellationToken);
    }

    /// <summary>
    ///     選択ネットワークを変更し、対応する受取ウォレットを再取得する。
    /// </summary>
    public async Task SelectNetworkAsync(string networkName, CancellationToken cancellationToken = default)
    {
        SelectedNetworkName = networkName;
        await LoadDepositWalletAsync(cancellationToken);
    }

    /// <summary>
    ///     ユーザー専用の受取ウォレットアドレスを非同期取得する。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "ウォレット取得失敗時にも画面崩壊を防ぐため例外を捕捉する")]
    public async Task LoadDepositWalletAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
        {
            IsLoadingWallet = false;
            return;
        }

        IsLoadingWallet = true;
        try
        {
            var wallet = await _purchaseService.GetOrCreateUserDepositWalletAsync(
                CurrentUserId, SelectedNetworkName, cancellationToken);
            DepositAddress = wallet.DepositAddress;
        }
        catch
        {
            DepositAddress = string.Empty;
        }
        finally
        {
            IsLoadingWallet = false;
        }
    }

    /// <summary>
    ///     テストネット向け送金シミュレーションを実行し、自動生成された TxHash を設定する。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "例外発生時にも画面崩壊を防ぎ、UI にエラー結果を返却するため捕捉する")]
    public async Task<Result<string>> SimulatePaymentAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
        {
            return Result.Fail<string>("ログインが必要です。");
        }

        try
        {
            var txHash = await _purchaseService.SimulateDepositAsync(
                CurrentUserId,
                SelectedNetworkName,
                TotalJpyc,
                cancellationToken);

            TransactionHash = txHash;
            return Result.Ok(txHash);
        }
        catch (Exception ex)
        {
            return Result.Fail<string>($"シミュレーションに失敗しました: {ex.Message}");
        }
    }

    /// <summary>
    ///     送金確認を実行し、RightAsset を購入・付与する。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "例外発生時にも画面崩壊を防ぎ、UI にエラー結果を返却するため捕捉する")]
    public async Task<Result<RightAsset>> SubmitPurchaseAsync(CancellationToken cancellationToken = default)
    {
        if (RequestedTag == null)
        {
            return Result.Fail<RightAsset>("対象のタグが指定されていません。");
        }

        if (string.IsNullOrWhiteSpace(CurrentUserId))
        {
            return Result.Fail<RightAsset>("ログインが必要です。");
        }

        if (Amount < 1)
        {
            return Result.Fail<RightAsset>("1以上の数量を入力してください。");
        }

        if (UnitPriceJpyc < 1)
        {
            return Result.Fail<RightAsset>("1以上の単価を入力してください。");
        }

        if (string.IsNullOrWhiteSpace(TransactionHash))
        {
            return Result.Fail<RightAsset>("トランザクションハッシュを入力してください。");
        }

        IsSubmitting = true;
        try
        {
            var request = new JpycPurchaseRequestDto(
                RequestedTagId: RequestedTag.Id,
                Amount: Amount,
                UnitPriceJpyc: UnitPriceJpyc,
                NetworkName: CurrentNetwork?.DisplayName ?? SelectedNetworkName,
                TransactionHash: TransactionHash.Trim());

            return await _purchaseService.PurchaseRightAssetWithJpycAsync(
                CurrentUserId, request, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Fail<RightAsset>($"トランザクション確認中にエラーが発生しました: {ex.Message}");
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}