namespace SRNSMudApp.Services.Auth;

/// <summary>
///     最初に登録された一般ユーザー（system を除く）を Admin ロールに自動昇格するサービスの契約。
/// </summary>
public interface IFirstUserAdminService
{
    /// <summary>
    ///     指定ユーザーが「system を除いて最初に登録された一般ユーザー」かどうかを確認し、
    ///     該当する場合に Admin ロールを付与する。
    ///     すでに一般ユーザーが 1 人以上存在する場合は何もしない（べき等）。
    /// </summary>
    /// <param name="userId">登録直後のユーザー ID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task GrantAdminIfFirstUserAsync(string userId, CancellationToken cancellationToken = default);
}

