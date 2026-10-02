#region

using Microsoft.AspNetCore.Identity;

using Moq;

using SRNSMudApp.Components.Account.Pages.Debug;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using Xunit;

#endregion

namespace SRNSMudApp.Tests.Components.Admin;

public class RequireConfirmedAccountViewModelTests
{
    private readonly Mock<IUserDataProvider> _userDataProviderMock = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;

    public RequireConfirmedAccountViewModelTests()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            storeMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private RequireConfirmedAccountViewModel CreateSut()
    {
        return new RequireConfirmedAccountViewModel(_userDataProviderMock.Object, _userManagerMock.Object);
    }

    [Fact]
    public void Constructor_WhenDependenciesNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RequireConfirmedAccountViewModel(null!, _userManagerMock.Object));
        Assert.Throws<ArgumentNullException>(() => new RequireConfirmedAccountViewModel(_userDataProviderMock.Object, null!));
    }

    [Fact]
    public void InitialState_PropertiesHaveExpectedDefaults()
    {
        var sut = CreateSut();

        Assert.Null(sut.Users);
        Assert.False(sut.IsLoading);
    }

    [Fact]
    public async Task LoadUsersAsync_CallsProviderAndUpdatesProperty()
    {
        var expectedUsers = new List<ApplicationUser>
        {
            new() { Id = "u1", Email = "u1@example.com", EmailConfirmed = false },
            new() { Id = "u2", Email = "u2@example.com", EmailConfirmed = true }
        };
        _userDataProviderMock.Setup(p => p.GetAllUsersAsync()).ReturnsAsync(expectedUsers);

        var sut = CreateSut();
        await sut.LoadUsersAsync();

        Assert.NotNull(sut.Users);
        Assert.Equal(2, sut.Users.Count);
        Assert.False(sut.IsLoading);
        _userDataProviderMock.Verify(p => p.GetAllUsersAsync(), Times.Once);
    }

    [Fact]
    public async Task ToggleConfirmationAsync_WhenSuccessful_InvertsConfirmedAndReloads()
    {
        var user = new ApplicationUser { Id = "u1", Email = "u1@example.com", EmailConfirmed = false };
        _userManagerMock
            .Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _userDataProviderMock
            .Setup(p => p.GetAllUsersAsync())
            .ReturnsAsync([user]);

        var sut = CreateSut();
        var result = await sut.ToggleConfirmationAsync(user);

        Assert.True(result is Success<bool> s && s.Value);
        Assert.True(user.EmailConfirmed);
        _userManagerMock.Verify(m => m.UpdateAsync(user), Times.Once);
        _userDataProviderMock.Verify(p => p.GetAllUsersAsync(), Times.Once);
    }

    [Fact]
    public async Task ToggleConfirmationAsync_WhenFails_ReturnsFailure()
    {
        var user = new ApplicationUser { Id = "u1", Email = "u1@example.com", EmailConfirmed = true };
        _userManagerMock
            .Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed" }));

        var sut = CreateSut();
        var result = await sut.ToggleConfirmationAsync(user);

        Assert.True(result is Failure f && f.ErrorMessage == "Failed to update user");
        Assert.False(user.EmailConfirmed);
    }
}