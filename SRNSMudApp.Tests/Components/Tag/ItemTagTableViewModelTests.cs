using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Moq;

using MudBlazor;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;

using Xunit;

namespace SRNSMudApp.Tests.Components.Tag;

/// <summary>
///     ItemTagTableViewModel の単体テスト。
///     TagSearchQuery を用いた 2 段階サジェストおよびテーブルフィルタ判定を検証する。
/// </summary>
public class ItemTagTableViewModelTests
{
    private static SRNSMudApp.Data.Tag CreateTag(int id = 1, string name = "CSharp", string ownerName = "alice") =>
        new()
        {
            Id = id,
            Name = name,
            OwnerId = $"user-{ownerName}",
            Owner = new ApplicationUser { Id = $"user-{ownerName}", UserName = ownerName },
            Content = $"Content for {name}"
        };

    private static TagRelation CreateTagRelation(int id, SRNSMudApp.Data.Tag tag, string ownerName = "alice") =>
        new()
        {
            Id = id,
            TagId = tag.Id,
            Tag = tag,
            OwnerId = $"user-{ownerName}",
            Owner = new ApplicationUser { Id = $"user-{ownerName}", UserName = ownerName },
            Weight = 1
        };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FilterFunc_WithEmptySearch_MatchesAll(string? search)
    {
        var relation = CreateTagRelation(1, CreateTag(1, "CSharp", "alice"));

        Assert.True(ItemTagTableViewModel.FilterFunc(relation, search));
    }

    [Fact]
    public void FilterFunc_WithTagNameSearch_MatchesTagOrUser()
    {
        var tag = CreateTag(1, "CSharp", "alice");
        var relation = CreateTagRelation(1, tag, "alice");

        Assert.True(ItemTagTableViewModel.FilterFunc(relation, "csharp"));
        Assert.True(ItemTagTableViewModel.FilterFunc(relation, "ali"));
        Assert.False(ItemTagTableViewModel.FilterFunc(relation, "python"));
    }

    [Fact]
    public void FilterFunc_WithIncompleteSearch_MatchesTag()
    {
        var tag = CreateTag(1, "CSharp", "alice");
        var relation = CreateTagRelation(1, tag, "alice");

        Assert.True(ItemTagTableViewModel.FilterFunc(relation, "CSharp @"));
        Assert.False(ItemTagTableViewModel.FilterFunc(relation, "Python @"));
    }

    [Fact]
    public void FilterFunc_WithTagWithUserSearch_MatchesBothTagAndUser()
    {
        var tag = CreateTag(1, "CSharp", "alice");
        var relation = CreateTagRelation(1, tag, "alice");

        Assert.True(ItemTagTableViewModel.FilterFunc(relation, "CSharp @alice"));
        Assert.True(ItemTagTableViewModel.FilterFunc(relation, "CSharp @ali"));
        Assert.False(ItemTagTableViewModel.FilterFunc(relation, "CSharp @bob"));
        Assert.False(ItemTagTableViewModel.FilterFunc(relation, "Python @alice"));
    }

    [Fact]
    public void GetSearchSuggestions_WithEmptyValue_ReturnsDistinctTagNamesWithAt()
    {
        var relations = new List<TagRelation>
        {
            CreateTagRelation(1, CreateTag(1, "CSharp", "alice")),
            CreateTagRelation(2, CreateTag(1, "CSharp", "bob")),
            CreateTagRelation(3, CreateTag(2, "Blazor", "charlie"))
        };

        var suggestions = ItemTagTableViewModel.GetSearchSuggestions(relations, "");

        Assert.Equal(["CSharp @", "Blazor @"], suggestions);
    }

    [Fact]
    public void GetSearchSuggestions_WithTagName_ReturnsFilteredTagNamesWithAt()
    {
        var relations = new List<TagRelation>
        {
            CreateTagRelation(1, CreateTag(1, "CSharp", "alice")),
            CreateTagRelation(2, CreateTag(2, "Blazor", "charlie")),
            CreateTagRelation(3, CreateTag(3, "Cloud", "dave"))
        };

        var suggestions = ItemTagTableViewModel.GetSearchSuggestions(relations, "c");

        Assert.Equal(["CSharp @", "Cloud @"], suggestions);
    }

    [Fact]
    public void GetSearchSuggestions_WithIncompleteSearch_ReturnsUserSuggestions()
    {
        var tagCSharp = CreateTag(1, "CSharp", "alice");
        var tagRelation1 = CreateTagRelation(1, tagCSharp, "alice");
        var tagRelation2 = new TagRelation
        {
            Id = 2,
            TagId = 1,
            Tag = tagCSharp,
            OwnerId = "user-bob",
            Owner = new ApplicationUser { Id = "user-bob", UserName = "bob" }
        };

        var relations = new List<TagRelation> { tagRelation1, tagRelation2 };

        var suggestions = ItemTagTableViewModel.GetSearchSuggestions(relations, "CSharp @");

        Assert.Contains("CSharp @alice", suggestions);
        Assert.Contains("CSharp @bob", suggestions);
    }

    [Fact]
    public void GetSearchSuggestions_WithIncompleteSearch_WhenNoUser_ReturnsTagNameWithAt()
    {
        var tag = new SRNSMudApp.Data.Tag { Id = 1, Name = "CSharp", OwnerId = "user-1" };
        var relation = new TagRelation { Id = 1, TagId = 1, Tag = tag, OwnerId = "user-1" };

        var suggestions = ItemTagTableViewModel.GetSearchSuggestions([relation], "CSharp @");

        Assert.Equal(["CSharp @"], suggestions);
    }

    [Fact]
    public void GetSearchSuggestions_WithTagWithUserSearch_FiltersUsers()
    {
        var tagCSharp = CreateTag(1, "CSharp", "alice");
        var tagRelation1 = CreateTagRelation(1, tagCSharp, "alice");
        var tagRelation2 = new TagRelation
        {
            Id = 2,
            TagId = 1,
            Tag = tagCSharp,
            OwnerId = "user-bob",
            Owner = new ApplicationUser { Id = "user-bob", UserName = "bob" }
        };

        var relations = new List<TagRelation> { tagRelation1, tagRelation2 };

        var suggestions = ItemTagTableViewModel.GetSearchSuggestions(relations, "CSharp @al");

        Assert.Equal(["CSharp @alice"], suggestions);
    }

    [Fact]
    public async Task SearchUsersAsync_CallsUserDataProvider()
    {
        var userMock = new Mock<IUserDataProvider>();
        var contractMock = new Mock<ITaggingContractService>();
        var expectedUsers = new List<ApplicationUser> { new() { Id = "u1", UserName = "alice" } };

        userMock.Setup(u => u.SearchUsersByNormalizedNameAsync("ali", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUsers);

        var vm = new ItemTagTableViewModel(userMock.Object, contractMock.Object);
        var result = await vm.SearchUsersAsync("ali");

        Assert.Equal(expectedUsers, result);
    }

    [Fact]
    public async Task RequestSelectedTagAddAsync_WhenTargetUserNull_ReturnsWarning()
    {
        var vm = new ItemTagTableViewModel(new Mock<IUserDataProvider>().Object, new Mock<ITaggingContractService>().Object);

        var result = await vm.RequestSelectedTagAddAsync(null, "u1", "tag", [], 1);

        Assert.False(result.Success);
        Assert.Equal(Severity.Warning, result.Severity);
        Assert.False(result.ShouldNotifyChanged);
    }

    [Fact]
    public async Task RequestSelectedTagAddAsync_WhenNotLoggedIn_ReturnsError()
    {
        var vm = new ItemTagTableViewModel(new Mock<IUserDataProvider>().Object, new Mock<ITaggingContractService>().Object);
        var targetUser = new ApplicationUser { Id = "u2", UserName = "bob" };

        var result = await vm.RequestSelectedTagAddAsync(targetUser, "", "tag", [], 1);

        Assert.False(result.Success);
        Assert.Equal(Severity.Error, result.Severity);
    }

    [Fact]
    public async Task RequestSelectedTagAddAsync_WhenTargetUserIsSelf_ReturnsWarning()
    {
        var vm = new ItemTagTableViewModel(new Mock<IUserDataProvider>().Object, new Mock<ITaggingContractService>().Object);
        var targetUser = new ApplicationUser { Id = "u1", UserName = "alice" };

        var result = await vm.RequestSelectedTagAddAsync(targetUser, "u1", "tag", [], 1);

        Assert.False(result.Success);
        Assert.Equal(Severity.Warning, result.Severity);
        Assert.Equal("自分自身には依頼できません。", result.Message);
    }

    [Fact]
    public async Task RequestSelectedTagAddAsync_WhenTargetTagNameEmpty_ReturnsWarning()
    {
        var vm = new ItemTagTableViewModel(new Mock<IUserDataProvider>().Object, new Mock<ITaggingContractService>().Object);
        var targetUser = new ApplicationUser { Id = "u2", UserName = "bob" };

        var result = await vm.RequestSelectedTagAddAsync(targetUser, "u1", "", [], 1);

        Assert.False(result.Success);
        Assert.Equal(Severity.Warning, result.Severity);
    }

    [Fact]
    public async Task RequestSelectedTagAddAsync_WhenTargetUserDoesNotOwnTag_ReturnsWarning()
    {
        var vm = new ItemTagTableViewModel(new Mock<IUserDataProvider>().Object, new Mock<ITaggingContractService>().Object);
        var targetUser = new ApplicationUser { Id = "u2", UserName = "bob" };
        var tag = new SRNSMudApp.Data.Tag { Id = 1, Name = "CSharp", OwnerId = "u3" }; // 他人のタグ

        var result = await vm.RequestSelectedTagAddAsync(targetUser, "u1", "CSharp", [tag], 1);

        Assert.False(result.Success);
        Assert.Equal(Severity.Warning, result.Severity);
        Assert.Contains("発行していません", result.Message);
    }

    [Fact]
    public async Task RequestSelectedTagAddAsync_WhenContractSuccess_ReturnsSuccessAndShouldNotify()
    {
        var userMock = new Mock<IUserDataProvider>();
        var contractMock = new Mock<ITaggingContractService>();
        var targetUser = new ApplicationUser { Id = "u2", UserName = "bob" };
        var tag = new SRNSMudApp.Data.Tag { Id = 1, Name = "CSharp", OwnerId = "u2" };

        contractMock.Setup(c => c.ProposeGratisContractAsync(
                "u1", "u2", 100, 1, TaggingRequestType.Add, 1, null))
            .ReturnsAsync(new Success<TaggingRequestEntity>(new TaggingRequestEntity { Id = 10, OwnerId = "u1" }));

        var vm = new ItemTagTableViewModel(userMock.Object, contractMock.Object);

        var result = await vm.RequestSelectedTagAddAsync(targetUser, "u1", "CSharp", [tag], 100);

        Assert.True(result.Success);
        Assert.Equal(Severity.Success, result.Severity);
        Assert.True(result.ShouldNotifyChanged);
        Assert.Contains("付与リクエストを送信しました", result.Message);
    }

    [Fact]
    public async Task RequestSelectedTagAddAsync_WhenContractFails_ReturnsError()
    {
        var userMock = new Mock<IUserDataProvider>();
        var contractMock = new Mock<ITaggingContractService>();
        var targetUser = new ApplicationUser { Id = "u2", UserName = "bob" };
        var tag = new SRNSMudApp.Data.Tag { Id = 1, Name = "CSharp", OwnerId = "u2" };

        contractMock.Setup(c => c.ProposeGratisContractAsync(
                "u1", "u2", 100, 1, TaggingRequestType.Add, 1, null))
            .ReturnsAsync(new Failure("既に存在します"));

        var vm = new ItemTagTableViewModel(userMock.Object, contractMock.Object);

        var result = await vm.RequestSelectedTagAddAsync(targetUser, "u1", "CSharp", [tag], 100);

        Assert.False(result.Success);
        Assert.Equal(Severity.Error, result.Severity);
        Assert.False(result.ShouldNotifyChanged);
        Assert.Contains("既に存在します", result.Message);
    }
}