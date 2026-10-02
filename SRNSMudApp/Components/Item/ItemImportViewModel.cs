#region

using System.Diagnostics.CodeAnalysis;
using System.Text;

using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Components.Item;

/// <summary>
///     CSVインポート機能用 ViewModel。
///     CSVストリームのパース、ファイルサイズ検証、およびアイテム・タグの一括インポート処理を担当する。
/// </summary>
public sealed class ItemImportViewModel
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    private readonly IAdminDataProvider _adminDataProvider;

    public ItemImportViewModel(IAdminDataProvider adminDataProvider)
    {
        _adminDataProvider = adminDataProvider ?? throw new ArgumentNullException(nameof(adminDataProvider));
    }

    public string CurrentUserId { get; set; } = string.Empty;
    public bool IsUploading { get; private set; }

    /// <summary>
    ///     CSVストリームを行ごとにパースし、1列目（アイテム本文）が存在する行のリストを生成する。
    /// </summary>
    public static async Task<IReadOnlyList<string[]>> ParseCsvStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        List<string[]> linesToProcess = [];
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = line.Split(',').Select(v => v.Trim()).ToArray();
            if (values.Length > 0 && !string.IsNullOrWhiteSpace(values[0]))
            {
                linesToProcess.Add(values);
            }
        }

        return linesToProcess;
    }

    /// <summary>
    ///     CSVストリームを検証・パースし、アイテムとタグを一括インポートする。
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catching exception to return Failure for UI notification")]
    public async Task<Result<int>> ImportCsvAsync(Stream stream, long fileSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (string.IsNullOrWhiteSpace(CurrentUserId))
        {
            return new Failure("ログインが必要です。");
        }

        if (fileSize > MaxFileSizeBytes)
        {
            return new Failure("ファイルサイズが大きすぎます。5MB以下のファイルを選択してください。");
        }

        IsUploading = true;
        try
        {
            var linesToProcess = await ParseCsvStreamAsync(stream, cancellationToken).ConfigureAwait(false);
            if (linesToProcess.Count == 0)
            {
                return new Failure("処理するデータがありませんでした。");
            }

            int importedCount = await _adminDataProvider.ImportItemsWithTagsAsync(CurrentUserId, linesToProcess).ConfigureAwait(false);
            return new Success<int>(importedCount);
        }
        catch (Exception ex)
        {
            return new Failure($"インポート中にエラーが発生しました: {ex.Message}");
        }
        finally
        {
            IsUploading = false;
        }
    }
}