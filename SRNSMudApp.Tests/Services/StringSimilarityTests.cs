#region

using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Tests.Services;

/// <summary>
///     StringSimilarity ユーティリティの単体テスト。
///     レーベンシュタイン距離計算・正規化類似度・全角半角正規化の各種ケースを検証する。
/// </summary>
public class StringSimilarityTests
{
    // --- LevenshteinDistance テスト ---

    [Fact]
    public void LevenshteinDistance_IdenticalStrings_ReturnsZero()
    {
        int distance = StringSimilarity.LevenshteinDistance("hello", "hello");
        Assert.Equal(0, distance);
    }

    [Fact]
    public void LevenshteinDistance_EmptyString_ReturnsOtherLength()
    {
        Assert.Equal(5, StringSimilarity.LevenshteinDistance("hello", ""));
        Assert.Equal(3, StringSimilarity.LevenshteinDistance("", "abc"));
    }

    [Fact]
    public void LevenshteinDistance_BothEmpty_ReturnsZero()
    {
        Assert.Equal(0, StringSimilarity.LevenshteinDistance("", ""));
    }

    [Fact]
    public void LevenshteinDistance_OneCharDifference_ReturnsOne()
    {
        // 置換
        Assert.Equal(1, StringSimilarity.LevenshteinDistance("cat", "bat"));
        // 挿入
        Assert.Equal(1, StringSimilarity.LevenshteinDistance("cat", "cats"));
        // 削除
        Assert.Equal(1, StringSimilarity.LevenshteinDistance("cats", "cat"));
    }

    [Fact]
    public void LevenshteinDistance_JapaneseStrings_CalculatesCorrectly()
    {
        // 完全一致
        Assert.Equal(0, StringSimilarity.LevenshteinDistance("プログラミング", "プログラミング"));
        // 1文字異なる
        Assert.Equal(1, StringSimilarity.LevenshteinDistance("プログラミング", "プロクラミング"));
        // 完全不一致（7文字 vs 7文字）
        Assert.Equal(7, StringSimilarity.LevenshteinDistance("プログラミング", "データサイエンス"));
    }

    // --- Calculate (正規化類似度) テスト ---

    [Fact]
    public void Calculate_IdenticalStrings_ReturnsOne()
    {
        float similarity = StringSimilarity.Calculate("hello", "hello");
        Assert.Equal(1.0f, similarity, 0.001f);
    }

    [Fact]
    public void Calculate_BothEmpty_ReturnsOne()
    {
        float similarity = StringSimilarity.Calculate("", "");
        Assert.Equal(1.0f, similarity, 0.001f);
    }

    [Fact]
    public void Calculate_OneEmpty_ReturnsZero()
    {
        Assert.Equal(0.0f, StringSimilarity.Calculate("hello", ""), 0.001f);
        Assert.Equal(0.0f, StringSimilarity.Calculate("", "hello"), 0.001f);
    }

    [Fact]
    public void Calculate_SimilarStrings_ReturnsHighSimilarity()
    {
        // "kitten" vs "sitting" = 編集距離3、最大長7 → 類似度 ≈ 0.571
        float similarity = StringSimilarity.Calculate("kitten", "sitting");
        Assert.InRange(similarity, 0.5f, 0.7f);
    }

    [Fact]
    public void Calculate_FullWidthHalfWidth_NormalizesAndMatches()
    {
        // 全角英字 "Ａ" → 半角 "A" に正規化されて完全一致
        float similarity = StringSimilarity.Calculate("Ａ", "A");
        Assert.Equal(1.0f, similarity, 0.001f);
    }

    [Fact]
    public void Calculate_CaseInsensitive_NormalizesAndMatches()
    {
        float similarity = StringSimilarity.Calculate("Hello", "hello");
        Assert.Equal(1.0f, similarity, 0.001f);
    }

    [Fact]
    public void Calculate_FullWidthNumbers_Normalizes()
    {
        float similarity = StringSimilarity.Calculate("１２３", "123");
        Assert.Equal(1.0f, similarity, 0.001f);
    }

    [Fact]
    public void Calculate_JapaneseSimilar_ReturnsHighSimilarity()
    {
        // "プログラミング" vs "プロクラミング" = 1文字異なる、7文字 → 類似度 ≈ 0.857
        float similarity = StringSimilarity.Calculate("プログラミング", "プロクラミング");
        Assert.True(similarity > 0.8f, $"Expected > 0.8 but got {similarity}");
    }

    [Fact]
    public void Calculate_CompletelyDifferent_ReturnsLowSimilarity()
    {
        float similarity = StringSimilarity.Calculate("abc", "xyz");
        Assert.True(similarity < 0.3f, $"Expected < 0.3 but got {similarity}");
    }

    [Fact]
    public void Calculate_FullWidthJapaneseSymbols_Normalizes()
    {
        // 全角記号の正規化テスト: "（" -> "(" は FF01-FF5E 範囲内
        float similarity = StringSimilarity.Calculate("（Ａ）", "(A)");
        Assert.Equal(1.0f, similarity, 0.001f);
    }
}