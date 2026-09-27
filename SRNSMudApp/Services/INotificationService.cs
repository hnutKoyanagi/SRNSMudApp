using System.Diagnostics.CodeAnalysis;

using SRNSMudApp.Models;

namespace SRNSMudApp.Services;

public interface INotificationService
{
    /// <summary>通知の未読状態や件数が変化した際に発火するイベント。</summary>
    event EventHandler? NotificationsChanged;

    Task<IReadOnlyList<NotificationDto>> GetUserNotificationsAsync(string userId);
    Task<int> GetUnreadCountAsync(string userId);
    Task MarkAsReadAsync(string userId, int sourceId, string sourceType);

    /// <summary>指定されたユーザーの未読通知をすべて既読として記録する。</summary>
    Task MarkAllAsReadAsync(string userId);

    /// <summary>通知の変更（新規通知発生やステータス変化）を通知リスナーへブロードキャストする。</summary>
    void NotifyNotificationsChanged();

    /// <summary>
    /// 対象ユーザーへ Web Push 通知を配信し、UI 通知イベントを発火します。
    /// </summary>
    [SuppressMessage("Design", "CA1054:URI parameters should not be strings", Justification = "URL is serialized to JSON for Web Push payload")]
    Task NotifyUserAsync(string userId, string title, string message, string? url = "/notifications", CancellationToken cancellationToken = default);
}