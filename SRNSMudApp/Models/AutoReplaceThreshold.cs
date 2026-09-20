namespace SRNSMudApp.Models;

/// <summary>
///     内部リンク自動置換の類似度閾値を表す値オブジェクト。
///     0.50f 〜 1.00f の有効範囲を型レベルで保証する。
/// </summary>
public readonly record struct AutoReplaceThreshold
{
    public const float MinValue = 0.50f;
    public const float MaxValue = 1.00f;

    public float Value { get; }

    public AutoReplaceThreshold(float value)
    {
        Value = Math.Clamp(value, MinValue, MaxValue);
    }

    public static AutoReplaceThreshold Default => new(LinkConversionCandidate.DefaultAutoReplaceThreshold);

    public static implicit operator float(AutoReplaceThreshold threshold) => threshold.Value;
    public static implicit operator AutoReplaceThreshold(float value) => new(value);

    public float ToSingle() => Value;
    public static AutoReplaceThreshold FromSingle(float value) => new(value);
}