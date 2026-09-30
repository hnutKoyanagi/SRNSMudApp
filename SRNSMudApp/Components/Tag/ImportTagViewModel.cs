#region

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using TagEntity = SRNSMudApp.Data.Tag;

#endregion

namespace SRNSMudApp.Components.Tag;

/// <summary>
///     CSV タグインポートページ (ImportTag.razor) のユーザー権限管理、親タグ検索、CSVインポート実行およびエラーハンドリングを担当する ViewModel。
/// </summary>
public sealed class ImportTagViewModel
{
    private readonly IImportTagDataProvider _importTagDataProvider;

    public ImportTagViewModel(IImportTagDataProvider importTagDataProvider)
    {
        _importTagDataProvider = importTagDataProvider ?? throw new ArgumentNullException(nameof(importTagDataProvider));
    }

    public string? CurrentUserId { get; private set; }
    public bool IsAdmin { get; private set; }
    public bool ImportAsSystem { get; set; }
    public TagEntity? SelectedParentTag { get; set; }
    public bool IsImporting { get; private set; }

    public bool CanImport => !IsImporting && SelectedParentTag != null && !string.IsNullOrEmpty(CurrentUserId);

    /// <summary>
    ///     認証ユーザー情報に基づいてユーザーIDおよび管理者権限を初期化します。
    /// </summary>
    public void Initialize(ClaimsPrincipal? user)
    {
        CurrentUserId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        IsAdmin = user?.IsInRole("Admin") == true;
    }

    /// <summary>
    ///     親タグ候補をインクリメンタル検索します。
    /// </summary>
    public async Task<IEnumerable<TagEntity>> SearchTagsAsync(string? value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return [];
        }

        return await _importTagDataProvider.SearchUserTagsAsync(CurrentUserId, value, cancellationToken);
    }

    /// <summary>
    ///     CSV テキストデータを親タグ配下へ一括インポートします。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "インポート失敗時にも画面崩壊を防ぎエラーメッセージを返すため捕捉")]
    public async Task<Result<TagImportResult>> ImportCsvAsync(string? csvContent, CancellationToken cancellationToken = default)
    {
        if (IsImporting)
        {
            return new Failure("インポート処理中です。");
        }

        if (SelectedParentTag is null)
        {
            return new Failure("親タグを選択してください。");
        }

        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return new Failure("CSVデータが空です。");
        }

        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return new Failure("ログインが必要です。");
        }

        IsImporting = true;
        try
        {
            bool asSystem = IsAdmin && ImportAsSystem;
            TagImportResult result = await _importTagDataProvider.ImportCsvTagsAsync(
                CurrentUserId,
                SelectedParentTag.Name,
                csvContent,
                asSystem);

            return new Success<TagImportResult>(result);
        }
        catch (Exception ex)
        {
            return new Failure($"インポート中にエラーが発生しました: {ex.Message}");
        }
        finally
        {
            IsImporting = false;
        }
    }
}