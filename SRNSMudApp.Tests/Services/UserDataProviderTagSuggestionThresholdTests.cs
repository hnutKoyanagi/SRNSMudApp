#region

using Microsoft.EntityFrameworkCore;

using SRNSMudApp.Data;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Tests.Services;

/// <summary>
///     <see cref="UserDataProvider" /> のタグ提案閾値設定更新に関する単体テスト。
/// </summary>
public class UserDataProviderTagSuggestionThresholdTests : IAsyncLifetime
{
    private MsSqlTestDatabase _sharedDb = null!;

    public async Task InitializeAsync()
    {
        _sharedDb = await SharedMsSqlTestDatabase.GetInstanceAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UpdateTagSuggestionThresholdsAsync_ShouldUpdateUserThresholds()
    {
        var tid = Guid.NewGuid().ToString("N")[..8];
        var userId = $"threshold_user_{tid}";

        await using (var db = new ApplicationDbContext(_sharedDb.Options))
        {
            await db.SeedUsersAsync(userId);
        }

        var stubFactory = new DbContextFactoryStub(_sharedDb.Options);
        var sut = new UserDataProvider(stubFactory);

        // Act
        await sut.UpdateTagSuggestionThresholdsAsync(userId, 0.75f, 0.45f);

        // Assert
        await using (var db = new ApplicationDbContext(_sharedDb.Options))
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            Assert.NotNull(user);
            Assert.Equal(0.75f, user.TagSuggestionStrongThreshold);
            Assert.Equal(0.45f, user.TagSuggestionCandidateThreshold);
        }
    }

    private sealed class DbContextFactoryStub(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ApplicationDbContext(options));
    }
}