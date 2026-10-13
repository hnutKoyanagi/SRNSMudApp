using System.Diagnostics.CodeAnalysis;

using SRNSMudApp.Models;

namespace SRNSMudApp.Services;

/// <summary>
///     URL や内部リンクからプレビューデータ（OGP / 内部リソース概要）を取得するサービスインターフェース。
/// </summary>
public interface ILinkPreviewService
{
    /// <summary>
    ///     指定された URL（内部相対パスまたは外部絶対 URL）のプレビューデータを取得する。
    /// </summary>
    /// <param name="url">プレビュー対象の URL。</param>
    /// <returns>リンクプレビューデータ。</returns>
    [SuppressMessage("Design", "CA1054:URI parameters should not be strings", Justification = "Internal links are relative paths")]
    Task<LinkPreviewData> GetPreviewAsync(string url);

    /// <summary>
    ///     指定された URL（内部相対パスまたは外部絶対 URL）のプレビューデータを取得する。
    /// </summary>
    /// <param name="url">プレビュー対象の URL。</param>
    /// <param name="cancellationToken">キャンセレーショントークン。</param>
    /// <returns>リンクプレビューデータ。</returns>
    [SuppressMessage("Design", "CA1054:URI parameters should not be strings", Justification = "Internal links are relative paths")]
    Task<LinkPreviewData> GetPreviewAsync(string url, CancellationToken cancellationToken);

    /// <summary>
    ///     指定された URL（内部相対パスまたは外部絶対 URL）のプレビューデータを取得する（ユーザー認証コンテキスト対応）。
    /// </summary>
    /// <param name="url">プレビュー対象の URL。</param>
    /// <param name="currentUserId">閲覧ユーザーID。未ログイン時は null。</param>
    /// <param name="isAdmin">管理者権限フラグ。</param>
    /// <param name="cancellationToken">キャンセレーショントークン。</param>
    /// <returns>リンクプレビューデータ。</returns>
    [SuppressMessage("Design", "CA1054:URI parameters should not be strings", Justification = "Internal links are relative paths")]
    Task<LinkPreviewData> GetPreviewAsync(string url, string? currentUserId, bool isAdmin = false, CancellationToken cancellationToken = default);
}