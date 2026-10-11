namespace SRNSMudApp.Models;

/// <summary>
///     タイムラインイベントの操作種別を表す列挙型。
/// </summary>
public enum TimelineEventType
{
    /// <summary>タグ追加イベント ("Insert")</summary>
    Insert,

    /// <summary>タグ更新イベント ("Update")</summary>
    Update,

    /// <summary>タグ削除イベント ("Delete")</summary>
    Delete
}

/// <summary>
///     <see cref="TimelineEventType"/> と文字列値の相互変換ヘルパー。
/// </summary>
public static class TimelineEventTypeExtensions
{
    public const string InsertValue = "Insert";
    public const string UpdateValue = "Update";
    public const string DeleteValue = "Delete";

    /// <summary>
    ///     列挙値をデータベース保存用文字列に変換する。
    /// </summary>
    public static string ToDbString(this TimelineEventType type) => type switch
    {
        TimelineEventType.Insert => InsertValue,
        TimelineEventType.Update => UpdateValue,
        TimelineEventType.Delete => DeleteValue,
        _ => type.ToString()
    };

    /// <summary>
    ///     文字列から <see cref="TimelineEventType"/> への安全なパース。
    /// </summary>
    public static TimelineEventType? ToTimelineEventType(this string? value) => value switch
    {
        InsertValue => TimelineEventType.Insert,
        UpdateValue => TimelineEventType.Update,
        DeleteValue => TimelineEventType.Delete,
        _ => null
    };
}