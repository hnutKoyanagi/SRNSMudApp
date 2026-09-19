// Components/Admin/InvitationManagerViewModelTests.cs
#region

using MudBlazor;

using SRNSMudApp.Components.Admin;
using SRNSMudApp.Data;

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
}