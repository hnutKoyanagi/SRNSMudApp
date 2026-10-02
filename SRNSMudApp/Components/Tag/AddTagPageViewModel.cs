#region

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using Microsoft.EntityFrameworkCore;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using TagEntity = SRNSMudApp.Data.Tag;

#endregion

namespace SRNSMudApp.Components.Tag;

/// <summary>
///     管理者向け新規タグ追加ページ (AddTag.razor) のユーザー認証確認、入力検証、タグ作成および一意制約エラーハンドリングを担当する ViewModel。
/// </summary>
public sealed class AddTagPageViewModel
{
    private readonly IUserDataProvider _userDataProvider;
    private readonly ITagCommandService _tagCommandService;

    public AddTagPageViewModel(IUserDataProvider userDataProvider, ITagCommandService tagCommandService)
    {
        _userDataProvider = userDataProvider ?? throw new ArgumentNullException(nameof(userDataProvider));
        _tagCommandService = tagCommandService ?? throw new ArgumentNullException(nameof(tagCommandService));
    }

    public string Name { get; set; } = string.Empty;
    public string? Content { get; set; }
    public bool IsSubmitting { get; private set; }

    public bool CanSubmit => !IsSubmitting && !string.IsNullOrWhiteSpace(Name);

    /// <summary>
    ///     認証ユーザー情報に基づいて新規タグを作成・登録します。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "タグ作成失敗時にUIへ適切なエラーメッセージを返すため捕捉")]
    public async Task<Result<TagEntity>> CreateTagAsync(ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        if (IsSubmitting)
        {
            return new Failure("送信処理中です。");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            return new Failure("タグ名は必須です。");
        }

        string? userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return new Failure("ユーザーが見つかりません。");
        }

        ApplicationUser? currentUser = await _userDataProvider.FindUserByIdAsync(userId);
        if (currentUser is null)
        {
            return new Failure("ユーザーが見つかりません。");
        }

        var tag = new TagEntity
        {
            Name = Name.Trim(),
            Content = Content?.Trim() ?? string.Empty,
            OwnerId = userId,
            Owner = currentUser,
            CachedWeight = 0
        };

        IsSubmitting = true;
        try
        {
            await _tagCommandService.CreateTagWithoutEmbeddingAsync(tag);
            return new Success<TagEntity>(tag);
        }
        catch (DbUpdateException ex)
        {
            bool isUniqueError = ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true
                || ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true;

            return new Failure(isUniqueError ? "同じ名前のタグが既に存在します。" : $"エラーが発生しました: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new Failure($"エラーが発生しました: {ex.Message}");
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}