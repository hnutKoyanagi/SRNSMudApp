#region

using System.Security.Claims;

using Moq;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using Xunit;

using TagEntity = SRNSMudApp.Data.Tag;

#endregion

namespace SRNSMudApp.Tests.Components.Tag;

public class ImportTagViewModelTests
{
    private readonly Mock<IImportTagDataProvider> _dataProviderMock = new();

    private ImportTagViewModel CreateSut()
    {
        return new ImportTagViewModel(_dataProviderMock.Object);
    }

    private static ClaimsPrincipal CreateClaimsPrincipal(string? userId, bool isAdmin = false)
    {
        if (userId is null)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    [Fact]
    public void Constructor_WhenDataProviderNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImportTagViewModel(null!));
    }

    [Fact]
    public void Initialize_ExtractsUserIdAndAdminRole()
    {
        var sut = CreateSut();
        var user = CreateClaimsPrincipal("user-123", isAdmin: true);

        sut.Initialize(user);

        Assert.Equal("user-123", sut.CurrentUserId);
        Assert.True(sut.IsAdmin);
    }

    [Fact]
    public void Initialize_WhenNormalUser_AdminIsFalse()
    {
        var sut = CreateSut();
        var user = CreateClaimsPrincipal("user-456", isAdmin: false);

        sut.Initialize(user);

        Assert.Equal("user-456", sut.CurrentUserId);
        Assert.False(sut.IsAdmin);
    }

    [Fact]
    public void CanImport_WhenParentTagNullOrUserIdNull_ReturnsFalse()
    {
        var sut = CreateSut();
        Assert.False(sut.CanImport);

        sut.Initialize(CreateClaimsPrincipal("user-123"));
        Assert.False(sut.CanImport);

        sut.SelectedParentTag = new TagEntity { Name = "RootTag", OwnerId = "user-123" };
        Assert.True(sut.CanImport);
    }

    [Fact]
    public async Task SearchTagsAsync_WhenUserIdNull_ReturnsEmpty()
    {
        var sut = CreateSut();

        var results = await sut.SearchTagsAsync("query");

        Assert.Empty(results);
        _dataProviderMock.Verify(d => d.SearchUserTagsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchTagsAsync_WhenUserIdSet_CallsDataProvider()
    {
        var sut = CreateSut();
        sut.Initialize(CreateClaimsPrincipal("user-123"));

        var expectedTags = new List<TagEntity>
        {
            new() { Id = 1, Name = "Tag1", OwnerId = "user-123" }
        };

        _dataProviderMock
            .Setup(d => d.SearchUserTagsAsync("user-123", "query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTags);

        var results = await sut.SearchTagsAsync("query");

        Assert.Single(results);
        _dataProviderMock.Verify(d => d.SearchUserTagsAsync("user-123", "query", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenParentTagNull_ReturnsFailure()
    {
        var sut = CreateSut();
        sut.Initialize(CreateClaimsPrincipal("user-123"));

        var result = await sut.ImportCsvAsync("A,B,C");

        Assert.True(result is Failure f && f.ErrorMessage == "親タグを選択してください。");
        _dataProviderMock.Verify(d => d.ImportCsvTagsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenCsvEmpty_ReturnsFailure()
    {
        var sut = CreateSut();
        sut.Initialize(CreateClaimsPrincipal("user-123"));
        sut.SelectedParentTag = new TagEntity { Name = "RootTag", OwnerId = "user-123" };

        var result = await sut.ImportCsvAsync("   ");

        Assert.True(result is Failure f && f.ErrorMessage == "CSVデータが空です。");
        _dataProviderMock.Verify(d => d.ImportCsvTagsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenUserIdNull_ReturnsFailure()
    {
        var sut = CreateSut();
        sut.SelectedParentTag = new TagEntity { Name = "RootTag", OwnerId = "user-123" };

        var result = await sut.ImportCsvAsync("A,B,C");

        Assert.True(result is Failure f && f.ErrorMessage == "ログインが必要です。");
        _dataProviderMock.Verify(d => d.ImportCsvTagsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenNormalUser_CallsDataProviderWithAsSystemFalse()
    {
        var sut = CreateSut();
        sut.Initialize(CreateClaimsPrincipal("user-123", isAdmin: false));
        sut.ImportAsSystem = true; // 一般ユーザーは強制的に false
        sut.SelectedParentTag = new TagEntity { Name = "RootTag", OwnerId = "user-123" };

        var expectedResult = new TagImportResult(CreatedCount: 5, UpdatedCount: 2);
        _dataProviderMock
            .Setup(d => d.ImportCsvTagsAsync("user-123", "RootTag", "A,B,C", false))
            .ReturnsAsync(expectedResult);

        var result = await sut.ImportCsvAsync("A,B,C");

        Assert.True(result is Success<TagImportResult> s && s.Value == expectedResult);
        Assert.False(sut.IsImporting);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenAdminAndImportAsSystem_CallsDataProviderWithAsSystemTrue()
    {
        var sut = CreateSut();
        sut.Initialize(CreateClaimsPrincipal("admin-user", isAdmin: true));
        sut.ImportAsSystem = true;
        sut.SelectedParentTag = new TagEntity { Name = "RootTag", OwnerId = "admin-user" };

        var expectedResult = new TagImportResult(CreatedCount: 10, UpdatedCount: 0);
        _dataProviderMock
            .Setup(d => d.ImportCsvTagsAsync("admin-user", "RootTag", "SysA,SysB", true))
            .ReturnsAsync(expectedResult);

        var result = await sut.ImportCsvAsync("SysA,SysB");

        Assert.True(result is Success<TagImportResult> s && s.Value == expectedResult);
        Assert.False(sut.IsImporting);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenProviderThrows_ReturnsFailure()
    {
        var sut = CreateSut();
        sut.Initialize(CreateClaimsPrincipal("user-123"));
        sut.SelectedParentTag = new TagEntity { Name = "RootTag", OwnerId = "user-123" };

        _dataProviderMock
            .Setup(d => d.ImportCsvTagsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ThrowsAsync(new FormatException("不正なCSVフォーマットです"));

        var result = await sut.ImportCsvAsync("invalid,csv");

        Assert.True(result is Failure f && f.ErrorMessage.Contains("不正なCSVフォーマットです"));
        Assert.False(sut.IsImporting);
    }
}