#region

using System.Security.Claims;

using Moq;

using MudBlazor;

using SRNSMudApp.Components.Admin;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Tests.Components.Admin;

/// <summary>
///     <see cref="InvitationManagerViewModel" /> の単体テスト。
/// </summary>
public class InvitationManagerViewModelTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public void GenerateRandomCode_GeneratesStringOfSpecifiedLength(int length)
    {
        var code = InvitationManagerViewModel.GenerateRandomCode(length);

        Assert.NotNull(code);
        Assert.Equal(length, code.Length);
        Assert.All(code, c => Assert.True(char.IsLetterOrDigit(c)));
    }

    [Fact]
    public void GenerateRandomCode_GeneratesDifferentCodes()
    {
        var code1 = InvitationManagerViewModel.GenerateRandomCode(16);
        var code2 = InvitationManagerViewModel.GenerateRandomCode(16);

        Assert.NotEqual(code1, code2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GenerateRandomCode_WhenLengthNonPositive_ThrowsArgumentOutOfRangeException(int length)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => InvitationManagerViewModel.GenerateRandomCode(length));
    }

    [Fact]
    public void GetInvitationStatus_WhenIsUsedIsTrue_ReturnsUsed()
    {
        var now = DateTime.UtcNow;
        var invitation = new Invitation
        {
            OwnerId = "test-owner",
            IsUsed = true,
            ExpirationDate = now.AddDays(1)
        };

        var actual = InvitationManagerViewModel.GetInvitationStatus(invitation, now);

        Assert.Equal(InvitationStatus.Used, actual);
    }

    [Fact]
    public void GetInvitationStatus_WhenNotUsedAndExpired_ReturnsExpired()
    {
        var now = DateTime.UtcNow;
        var invitation = new Invitation
        {
            OwnerId = "test-owner",
            IsUsed = false,
            ExpirationDate = now.AddMinutes(-5)
        };

        var actual = InvitationManagerViewModel.GetInvitationStatus(invitation, now);

        Assert.Equal(InvitationStatus.Expired, actual);
    }

    [Fact]
    public void GetInvitationStatus_WhenNotUsedAndNotExpired_ReturnsPending()
    {
        var now = DateTime.UtcNow;
        var invitation = new Invitation
        {
            OwnerId = "test-owner",
            IsUsed = false,
            ExpirationDate = now.AddHours(2)
        };

        var actual = InvitationManagerViewModel.GetInvitationStatus(invitation, now);

        Assert.Equal(InvitationStatus.Pending, actual);
    }

    [Fact]
    public void GetInvitationStatus_WhenInvitationIsNull_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => InvitationManagerViewModel.GetInvitationStatus(null!, DateTime.UtcNow));
    }

    [Theory]
    [InlineData(InvitationStatus.Used, "Used")]
    [InlineData(InvitationStatus.Expired, "Expired")]
    [InlineData(InvitationStatus.Pending, "Pending")]
    [InlineData((InvitationStatus)999, "Unknown")]
    public void GetStatusText_MapsStatusToDisplayText(InvitationStatus status, string expectedText)
    {
        var actual = InvitationManagerViewModel.GetStatusText(status);
        Assert.Equal(expectedText, actual);
    }

    [Theory]
    [InlineData(InvitationStatus.Used, Color.Success)]
    [InlineData(InvitationStatus.Expired, Color.Error)]
    [InlineData(InvitationStatus.Pending, Color.Warning)]
    [InlineData((InvitationStatus)999, Color.Default)]
    public void GetStatusColor_MapsStatusToMudColor(InvitationStatus status, Color expectedColor)
    {
        var actual = InvitationManagerViewModel.GetStatusColor(status);
        Assert.Equal(expectedColor, actual);
    }

    [Fact]
    public void Constructor_WhenAdminDataIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new InvitationManagerViewModel(null!));
    }

    [Fact]
    public void BuildInviteLink_ReturnsExpectedRelativeUri()
    {
        var link = InvitationManagerViewModel.BuildInviteLink("abc123XYZ");
        Assert.Equal("/Account/Login?inviteCode=abc123XYZ", link);
    }

    [Fact]
    public async Task LoadInvitationsAsync_CallsProviderAndUpdatesProperty()
    {
        var mockAdminData = new Mock<IAdminDataProvider>();
        var sampleList = new List<Invitation>
        {
            new() { Email = "test@example.com", InvitationCode = "code1", OwnerId = "user1" }
        };
        mockAdminData.Setup(d => d.GetInvitationsAsync()).ReturnsAsync(sampleList);

        var sut = new InvitationManagerViewModel(mockAdminData.Object);
        await sut.LoadInvitationsAsync();

        Assert.Single(sut.Invitations);
        Assert.Equal("test@example.com", sut.Invitations[0].Email);
        Assert.False(sut.IsProcessing);
    }

    [Fact]
    public async Task CreateInvitationAsync_WhenEmailEmpty_ReturnsFailure()
    {
        var mockAdminData = new Mock<IAdminDataProvider>();
        var sut = new InvitationManagerViewModel(mockAdminData.Object) { NewEmail = "   " };

        var result = await sut.CreateInvitationAsync();

        Assert.True(result is Failure f && f.ErrorMessage == "Email is required.");
        mockAdminData.Verify(d => d.CreateInvitationAsync(It.IsAny<Invitation>()), Times.Never);
    }

    [Fact]
    public async Task CreateInvitationAsync_WhenValidEmail_CreatesInvitationAndClearsEmail()
    {
        var mockAdminData = new Mock<IAdminDataProvider>();
        mockAdminData.Setup(d => d.GetInvitationsAsync()).ReturnsAsync([]);
        mockAdminData.Setup(d => d.CreateInvitationAsync(It.IsAny<Invitation>())).Returns(Task.CompletedTask);

        var fakeTime = new TestTimeProvider(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        var sut = new InvitationManagerViewModel(mockAdminData.Object, fakeTime)
        {
            NewEmail = "newuser@example.com"
        };
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "admin-42") };
        sut.Initialize(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));

        var result = await sut.CreateInvitationAsync();

        Assert.True(result is Success<Invitation> s
            && s.Value.Email == "newuser@example.com"
            && s.Value.InvitedByAdminId == "admin-42"
            && s.Value.OwnerId == "admin-42"
            && s.Value.ExpirationDate == fakeTime.GetUtcNow().UtcDateTime.AddDays(7));
        Assert.Equal(string.Empty, sut.NewEmail);
        mockAdminData.Verify(d => d.CreateInvitationAsync(It.IsAny<Invitation>()), Times.Once);
    }

    [Fact]
    public async Task DeleteInvitationAsync_WhenCalled_CallsServiceAndReloads()
    {
        var mockAdminData = new Mock<IAdminDataProvider>();
        mockAdminData.Setup(d => d.GetInvitationsAsync()).ReturnsAsync([]);
        mockAdminData.Setup(d => d.DeleteInvitationAsync(It.IsAny<Invitation>())).Returns(Task.CompletedTask);

        var sut = new InvitationManagerViewModel(mockAdminData.Object);
        var target = new Invitation { Email = "del@example.com", InvitationCode = "c", OwnerId = "user" };

        var result = await sut.DeleteInvitationAsync(target);

        Assert.True(result is Success<bool>);
        mockAdminData.Verify(d => d.DeleteInvitationAsync(target), Times.Once);
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}