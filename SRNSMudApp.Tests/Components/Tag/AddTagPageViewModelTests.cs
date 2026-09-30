#region

using System.Security.Claims;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Moq;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using Xunit;

using TagEntity = SRNSMudApp.Data.Tag;

#endregion

namespace SRNSMudApp.Tests.Components.Tag;

public class AddTagPageViewModelTests
{
    private readonly Mock<IUserDataProvider> _userDataProviderMock = new();
    private readonly Mock<ITagCommandService> _tagCommandServiceMock = new();

    private AddTagPageViewModel CreateSut()
    {
        return new AddTagPageViewModel(_userDataProviderMock.Object, _tagCommandServiceMock.Object);
    }

    private static ClaimsPrincipal CreateClaimsPrincipal(string? userId)
    {
        if (userId is null)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    [Fact]
    public void Constructor_WhenDependenciesNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AddTagPageViewModel(null!, _tagCommandServiceMock.Object));
        Assert.Throws<ArgumentNullException>(() => new AddTagPageViewModel(_userDataProviderMock.Object, null!));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("TestTag", true)]
    public void CanSubmit_EvaluatesCorrectly(string name, bool expected)
    {
        var sut = CreateSut();
        sut.Name = name;

        Assert.Equal(expected, sut.CanSubmit);
    }

    [Fact]
    public async Task CreateTagAsync_WhenNameIsEmpty_ReturnsFailure()
    {
        var sut = CreateSut();
        sut.Name = "   ";
        var user = CreateClaimsPrincipal("user-1");

        var result = await sut.CreateTagAsync(user);

        Assert.True(result is Failure f && f.ErrorMessage == "タグ名は必須です。");
        _tagCommandServiceMock.Verify(s => s.CreateTagWithoutEmbeddingAsync(It.IsAny<TagEntity>()), Times.Never);
    }

    [Fact]
    public async Task CreateTagAsync_WhenUserNull_ReturnsFailure()
    {
        var sut = CreateSut();
        sut.Name = "TestTag";

        var result = await sut.CreateTagAsync(null);

        Assert.True(result is Failure f && f.ErrorMessage == "ユーザーが見つかりません。");
        _tagCommandServiceMock.Verify(s => s.CreateTagWithoutEmbeddingAsync(It.IsAny<TagEntity>()), Times.Never);
    }

    [Fact]
    public async Task CreateTagAsync_WhenUserIdMissing_ReturnsFailure()
    {
        var sut = CreateSut();
        sut.Name = "TestTag";
        var user = CreateClaimsPrincipal(null);

        var result = await sut.CreateTagAsync(user);

        Assert.True(result is Failure f && f.ErrorMessage == "ユーザーが見つかりません。");
        _tagCommandServiceMock.Verify(s => s.CreateTagWithoutEmbeddingAsync(It.IsAny<TagEntity>()), Times.Never);
    }

    [Fact]
    public async Task CreateTagAsync_WhenCurrentUserNotFoundInDb_ReturnsFailure()
    {
        var sut = CreateSut();
        sut.Name = "TestTag";
        var user = CreateClaimsPrincipal("user-1");

        _userDataProviderMock
            .Setup(u => u.FindUserByIdAsync("user-1"))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await sut.CreateTagAsync(user);

        Assert.True(result is Failure f && f.ErrorMessage == "ユーザーが見つかりません。");
        _tagCommandServiceMock.Verify(s => s.CreateTagWithoutEmbeddingAsync(It.IsAny<TagEntity>()), Times.Never);
    }

    [Fact]
    public async Task CreateTagAsync_WhenUniqueConstraintFails_ReturnsDuplicateErrorMessage()
    {
        var sut = CreateSut();
        sut.Name = "DuplicateTag";
        var user = CreateClaimsPrincipal("user-1");
        var appUser = new ApplicationUser { Id = "user-1", UserName = "testuser" };

        _userDataProviderMock
            .Setup(u => u.FindUserByIdAsync("user-1"))
            .ReturnsAsync(appUser);

        var innerException = new Exception("UNIQUE constraint failed: Tags.Name");
        _tagCommandServiceMock
            .Setup(s => s.CreateTagWithoutEmbeddingAsync(It.IsAny<TagEntity>()))
            .ThrowsAsync(new DbUpdateException("DB Error", innerException));

        var result = await sut.CreateTagAsync(user);

        Assert.True(result is Failure f && f.ErrorMessage == "同じ名前のタグが既に存在します。");
        Assert.False(sut.IsSubmitting);
    }

    [Fact]
    public async Task CreateTagAsync_WhenGenericExceptionOccurs_ReturnsErrorMessage()
    {
        var sut = CreateSut();
        sut.Name = "ErrorTag";
        var user = CreateClaimsPrincipal("user-1");
        var appUser = new ApplicationUser { Id = "user-1", UserName = "testuser" };

        _userDataProviderMock
            .Setup(u => u.FindUserByIdAsync("user-1"))
            .ReturnsAsync(appUser);

        _tagCommandServiceMock
            .Setup(s => s.CreateTagWithoutEmbeddingAsync(It.IsAny<TagEntity>()))
            .ThrowsAsync(new InvalidOperationException("Connection timeout"));

        var result = await sut.CreateTagAsync(user);

        Assert.True(result is Failure f && f.ErrorMessage.Contains("Connection timeout"));
        Assert.False(sut.IsSubmitting);
    }

    [Fact]
    public async Task CreateTagAsync_WhenSuccess_CreatesTagAndReturnsSuccess()
    {
        var sut = CreateSut();
        sut.Name = "  NewTag  ";
        sut.Content = "  Some description  ";
        var user = CreateClaimsPrincipal("user-1");
        var appUser = new ApplicationUser { Id = "user-1", UserName = "testuser" };

        _userDataProviderMock
            .Setup(u => u.FindUserByIdAsync("user-1"))
            .ReturnsAsync(appUser);

        TagEntity? createdTag = null;
        _tagCommandServiceMock
            .Setup(s => s.CreateTagWithoutEmbeddingAsync(It.IsAny<TagEntity>(), It.IsAny<bool>()))
            .Callback<TagEntity, bool>((t, _) => createdTag = t)
            .Returns(Task.CompletedTask);

        var result = await sut.CreateTagAsync(user);

        Assert.True(result is Success<TagEntity> s && s.Value.Name == "NewTag");
        Assert.NotNull(createdTag);
        Assert.Equal("NewTag", createdTag.Name);
        Assert.Equal("Some description", createdTag.Content);
        Assert.Equal("user-1", createdTag.OwnerId);
        Assert.Same(appUser, createdTag.Owner);
        Assert.Equal(0, createdTag.CachedWeight);
        Assert.False(sut.IsSubmitting);
    }
}