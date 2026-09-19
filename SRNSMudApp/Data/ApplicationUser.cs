#region

using Microsoft.AspNetCore.Identity;

#endregion

namespace SRNSMudApp.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    /// <summary>
    ///     アカウントのデフォルトプライベートモード設定。
    ///     true の場合、新規投稿時にデフォルトでプライベートモードが有効化される。
    /// </summary>
    public bool IsPrivateModeDefault { get; set; }

    /// <summary>
    ///     デフォルトの公開対象ユーザーグループID（null の場合はフォロワー限定）。
    /// </summary>
    public int? DefaultPrivateUserGroupId { get; set; }

    /// <summary>
    ///     デフォルト公開対象ユーザーグループへのナビゲーションプロパティ。
    /// </summary>
    public UserGroup? DefaultPrivateUserGroup { get; set; }

    /// <summary>
    ///     タグ提案の自動関連付け（強い関連）類似度閾値（null の場合はデフォルト 0.65f）。
    /// </summary>
    public float? TagSuggestionStrongThreshold { get; set; }

    /// <summary>
    ///     タグ提案の候補推薦類似度閾値（null の場合はデフォルト 0.40f）。
    /// </summary>
    public float? TagSuggestionCandidateThreshold { get; set; }

    /// <summary>
    ///     テキスト→内部リンク自動変換が有効かどうか。
    /// </summary>
    public bool IsLinkConversionEnabled { get; set; }

    /// <summary>
    ///     内部リンク自動変換の類似度閾値（null の場合はデフォルト 0.85f）。
    /// </summary>
    public float? LinkConversionThreshold { get; set; }
}