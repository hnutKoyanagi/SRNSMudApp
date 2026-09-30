using System.Security.Claims;

using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

using Moq;

using SRNSMudApp.Components.User;

using Xunit;

#pragma warning disable BL0016

namespace SRNSMudApp.Tests.Components.User;

public class MakeMeAdminViewModelTests
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock = new();

    [Fact]
    public void Initialize_WhenUserHasAdminRole_SetsAdminTrueAndTargetFalse()
    {
        var vm = new MakeMeAdminViewModel();
        var claims = new[] { new Claim(ClaimTypes.Role, "Admin") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        vm.Initialize(principal);

        Assert.True(vm.IsAdmin);
        Assert.False(vm.TargetAdminState);
        Assert.False(vm.IsProcessing);
    }

    [Fact]
    public void Initialize_WhenUserIsRegular_SetsAdminFalseAndTargetTrue()
    {
        var vm = new MakeMeAdminViewModel();
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        vm.Initialize(principal);

        Assert.False(vm.IsAdmin);
        Assert.True(vm.TargetAdminState);
        Assert.False(vm.IsProcessing);
    }

    [Fact]
    public void Initialize_WhenUserIsNull_SetsAdminFalseAndTargetTrue()
    {
        var vm = new MakeMeAdminViewModel();

        vm.Initialize(null);

        Assert.False(vm.IsAdmin);
        Assert.True(vm.TargetAdminState);
    }

    [Fact]
    public async Task ToggleAdminAsync_WhenJsRuntimeNull_ThrowsArgumentNullException()
    {
        var vm = new MakeMeAdminViewModel();

        await Assert.ThrowsAsync<ArgumentNullException>(() => vm.ToggleAdminAsync(true, null!));
    }

    [Fact]
    public async Task ToggleAdminAsync_WhenAlreadyProcessing_ReturnsFalseWithoutCallingJs()
    {
        var vm = new MakeMeAdminViewModel { IsProcessing = true };

        var result = await vm.ToggleAdminAsync(true, _jsRuntimeMock.Object);

        Assert.False(result);
        _jsRuntimeMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            It.IsAny<string>(), It.IsAny<object?[]?>()), Times.Never);
    }

    [Fact]
    public async Task ToggleAdminAsync_WhenSuccessful_SetsTargetAndInvokesJs()
    {
        var vm = new MakeMeAdminViewModel();

        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<IJSVoidResult>(
                "eval",
                It.Is<object?[]?>(args => args != null && args.Length == 1 && ((string)args[0]!).Contains("toggleAdminSubmitBtn"))))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());

        var result = await vm.ToggleAdminAsync(true, _jsRuntimeMock.Object);

        Assert.True(result);
        Assert.True(vm.TargetAdminState);
        Assert.True(vm.IsProcessing);
    }

    [Fact]
    public async Task ToggleAdminAsync_WhenJsThrows_ResetsProcessingAndReturnsFalse()
    {
        var vm = new MakeMeAdminViewModel();

        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<IJSVoidResult>(
                "eval",
                It.IsAny<object?[]?>()))
            .ThrowsAsync(new JSException("DOM error"));

        var result = await vm.ToggleAdminAsync(true, _jsRuntimeMock.Object);

        Assert.False(result);
        Assert.False(vm.IsProcessing);
    }
}