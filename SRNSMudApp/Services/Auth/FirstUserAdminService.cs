#region

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using SRNSMudApp.Data;

#endregion

namespace SRNSMudApp.Services.Auth;

/// <summary>
///     最初に登録された一般ユーザー（system を除く）を Admin ロールに自動昇格する実装。
///     初回デプロイ後の最初の登録ユーザーをサービス管理者として扱う用途を想定している。
/// </summary>
public sealed class FirstUserAdminService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ILogger<FirstUserAdminService> logger) : IFirstUserAdminService
{
    /// <inheritdoc />
    public async Task GrantAdminIfFirstUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        // system ユーザー以外の一般ユーザーが既に 1 人でも存在する場合は昇格しない（べき等保証）
        bool hasOtherUser = await userManager.Users
            .AnyAsync(u => u.Id != "system" && u.Id != userId, cancellationToken);

        if (hasOtherUser)
        {
            return;
        }

        ApplicationUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogWarning("FirstUserAdminService: userId={UserId} のユーザーが見つかりません。Admin 昇格をスキップします。", userId);
            return;
        }

        // Admin ロールが存在しない場合は作成（シード済みが前提だが念のため保証）
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            _ = await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        if (!await userManager.IsInRoleAsync(user, "Admin"))
        {
            IdentityResult result = await userManager.AddToRoleAsync(user, "Admin");
            if (result.Succeeded)
            {
                logger.LogInformation(
                    "FirstUserAdminService: userId={UserId} を Admin ロールに昇格しました（初回登録ユーザー）。",
                    userId);
            }
            else
            {
                logger.LogWarning(
                    "FirstUserAdminService: userId={UserId} の Admin 昇格に失敗しました: {Errors}",
                    userId,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}