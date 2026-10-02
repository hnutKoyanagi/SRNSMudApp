namespace SRNSMudApp.Tests.Components.User;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Moq;

using SRNSMudApp.Components.User;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

using Xunit;

public class UserSearchViewModelTests
{
    private readonly Mock<IUserDataProvider> _userDataProviderMock = new();

    private UserSearchViewModel CreateViewModel()
    {
        return new UserSearchViewModel(_userDataProviderMock.Object);
    }

    [Fact]
    public async Task SearchUsersAsync_DelegatesToUserDataProvider()
    {
        // Arrange
        var expectedUsers = new List<ApplicationUser>
        {
            new() { Id = "u1", UserName = "Alice" },
            new() { Id = "u2", UserName = "Bob" }
        };

        using var cts = new CancellationTokenSource();
        _userDataProviderMock.Setup(p => p.SearchUsersByNormalizedNameAsync("ali", cts.Token))
            .ReturnsAsync(expectedUsers);

        var vm = CreateViewModel();

        // Act
        var results = await vm.SearchUsersAsync("ali", cts.Token);

        // Assert
        Assert.Same(expectedUsers, results);
        _userDataProviderMock.Verify(p => p.SearchUsersByNormalizedNameAsync("ali", cts.Token), Times.Once);
    }

    [Fact]
    public void SelectUser_SetsSelectedUser()
    {
        var vm = CreateViewModel();
        var user = new ApplicationUser { Id = "u1", UserName = "Alice" };

        vm.SelectUser(user);

        Assert.Same(user, vm.SelectedUser);
    }

    [Fact]
    public void ClearSelection_ResetsSelectedUser()
    {
        var vm = CreateViewModel();
        var user = new ApplicationUser { Id = "u1", UserName = "Alice" };
        vm.SelectUser(user);
        Assert.NotNull(vm.SelectedUser);

        vm.ClearSelection();

        Assert.Null(vm.SelectedUser);
    }
}