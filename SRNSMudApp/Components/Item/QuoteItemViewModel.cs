using System.Diagnostics.CodeAnalysis;

using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

namespace SRNSMudApp.Components.Item;

/// <summary>
///     QuoteItemDialog の状態管理、バリデーション、および引用投稿ロジックを担当する ViewModel。
///     UI（Blazor / bUnit）から切り離し、純粋な xUnit + Moq 単体テストを可能にする。
/// </summary>
public class QuoteItemViewModel
{
    private readonly IItemQuoteService _itemQuoteService;

    public QuoteItemViewModel(IItemQuoteService itemQuoteService)
    {
        _itemQuoteService = itemQuoteService;
    }

    public Data.Item? QuotedItem { get; set; }
    public string CurrentUserId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsSubmitting { get; private set; }

    public bool CanSubmit => !string.IsNullOrWhiteSpace(Content) && !IsSubmitting;

    /// <summary>
    ///     引用投稿を作成する。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "例外発生時にも画面崩壊を防ぎ、UI にエラー結果を返却するため捕捉する")]
    public async Task<Result<Data.Item>> SubmitAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Content))
        {
            return Result.Fail<Data.Item>("本文を入力してください。");
        }

        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Result.Fail<Data.Item>("ログインが必要です。");
        }

        if (QuotedItem == null)
        {
            return Result.Fail<Data.Item>("引用元のアイテムが指定されていません。");
        }

        IsSubmitting = true;
        try
        {
            var created = await _itemQuoteService.CreateQuoteItemAsync(
                QuotedItem.Id, Content, CurrentUserId, cancellationToken: cancellationToken);

            if (created is null)
            {
                return Result.Fail<Data.Item>("引用元のアイテムが見つかりませんでした。");
            }

            return Result.Ok(created);
        }
        catch (Exception ex)
        {
            return Result.Fail<Data.Item>($"エラーが発生しました: {ex.Message}");
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}