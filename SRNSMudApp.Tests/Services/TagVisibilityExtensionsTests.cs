#region

using SRNSMudApp.Data;
using SRNSMudApp.Services;

#endregion

namespace SRNSMudApp.Tests.Services;

/// <summary>
///     <see cref="TagVisibilityExtensions" /> の単体テスト。
///     BAN されたユーザーがオーナーのタグが不可視となることを検証する。
/// </summary>
public sealed class TagVisibilityExtensionsTests
{
    [Fact]
    public void IsTagVisibleToUser_WhenOwnerIsNull_ReturnsTrue()
    {
        // Arrange
        var tag = new Tag { Name = "TagWithoutOwner", OwnerId = "user1", Owner = null! };

        // Act & Assert
        Assert.True(tag.IsTagVisibleToUser());
    }

    [Fact]
    public void IsTagVisibleToUser_WhenOwnerIsNotBanned_ReturnsTrue()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "active_user", IsBanned = false };
        var tag = new Tag { Name = "ActiveUserTag", OwnerId = user.Id, Owner = user };

        // Act & Assert
        Assert.True(tag.IsTagVisibleToUser());
    }

    [Fact]
    public void IsTagVisibleToUser_WhenOwnerIsBanned_ReturnsFalse()
    {
        // Arrange
        var bannedUser = new ApplicationUser { Id = "banned1", UserName = "banned_user", IsBanned = true };
        var tag = new Tag { Name = "BannedUserTag", OwnerId = bannedUser.Id, Owner = bannedUser };

        // Act & Assert
        Assert.False(tag.IsTagVisibleToUser());
    }

    [Fact]
    public void WhereVisibleToUser_FiltersOutTagsOwnedByBannedUsers()
    {
        // Arrange
        var bannedUser = new ApplicationUser { Id = "banned1", UserName = "banned_user", IsBanned = true };
        var activeUser = new ApplicationUser { Id = "active1", UserName = "active_user", IsBanned = false };

        var tags = new List<Tag>
        {
            new() { Id = 1, Name = "ActiveTag", OwnerId = activeUser.Id, Owner = activeUser },
            new() { Id = 2, Name = "BannedTag", OwnerId = bannedUser.Id, Owner = bannedUser },
            new() { Id = 3, Name = "SystemOrNullOwnerTag", OwnerId = "system", Owner = null! }
        }.AsQueryable();

        // Act
        var visibleTags = tags.WhereVisibleToUser().ToList();

        // Assert
        Assert.Contains(visibleTags, t => t.Id == 1);
        Assert.Contains(visibleTags, t => t.Id == 3);
        Assert.DoesNotContain(visibleTags, t => t.Id == 2);
    }

    [Fact]
    public void IsTagVisibleInMemory_WhenAdmin_ReturnsTrueEvenIfAdminHidden()
    {
        var tag = new Tag { Name = "HiddenTag", OwnerId = "user1", IsAdminHidden = true };
        Assert.True(tag.IsTagVisibleInMemory("other_user", isAdmin: true, followedUserIds: null, memberGroupIds: null));
    }

    [Fact]
    public void IsTagVisibleInMemory_WhenAdminHiddenAndNotAdmin_ReturnsFalse()
    {
        var tag = new Tag { Name = "HiddenTag", OwnerId = "user1", IsAdminHidden = true };
        Assert.False(tag.IsTagVisibleInMemory("user1", isAdmin: false, followedUserIds: null, memberGroupIds: null));
    }

    [Fact]
    public void IsTagVisibleInMemory_WhenPublicTag_ReturnsTrueForAnyone()
    {
        var tag = new Tag { Name = "PublicTag", OwnerId = "user1", IsPrivate = false };
        Assert.True(tag.IsTagVisibleInMemory(null, isAdmin: false, followedUserIds: null, memberGroupIds: null));
        Assert.True(tag.IsTagVisibleInMemory("stranger", isAdmin: false, followedUserIds: null, memberGroupIds: null));
    }

    [Fact]
    public void IsTagVisibleInMemory_WhenPrivateTag_ReturnsFalseForAnonymous()
    {
        var tag = new Tag { Name = "PrivateTag", OwnerId = "user1", IsPrivate = true };
        Assert.False(tag.IsTagVisibleInMemory(null, isAdmin: false, followedUserIds: null, memberGroupIds: null));
    }

    [Fact]
    public void IsTagVisibleInMemory_WhenPrivateTag_ReturnsTrueForOwner()
    {
        var tag = new Tag { Name = "PrivateTag", OwnerId = "user1", IsPrivate = true };
        Assert.True(tag.IsTagVisibleInMemory("user1", isAdmin: false, followedUserIds: null, memberGroupIds: null));
    }

    [Fact]
    public void IsTagVisibleInMemory_WhenFollowersOnly_ReturnsTrueOnlyForFollowers()
    {
        var tag = new Tag { Name = "FollowersTag", OwnerId = "user1", IsPrivate = true, TargetUserGroupId = null };
        var followerIds = new HashSet<string> { "user1" }; // user2 follows user1
        Assert.True(tag.IsTagVisibleInMemory("user2", isAdmin: false, followedUserIds: followerIds, memberGroupIds: null));

        var notFollowerIds = new HashSet<string> { "user3" }; // user4 does not follow user1
        Assert.False(tag.IsTagVisibleInMemory("user4", isAdmin: false, followedUserIds: notFollowerIds, memberGroupIds: null));
    }

    [Fact]
    public void IsTagVisibleInMemory_WhenGroupOnly_ReturnsTrueOnlyForGroupMembers()
    {
        var tag = new Tag { Name = "GroupTag", OwnerId = "user1", IsPrivate = true, TargetUserGroupId = 42 };
        var memberGroupIds = new HashSet<int> { 42 };
        Assert.True(tag.IsTagVisibleInMemory("member", isAdmin: false, followedUserIds: null, memberGroupIds: memberGroupIds));

        var otherGroupIds = new HashSet<int> { 99 };
        Assert.False(tag.IsTagVisibleInMemory("non_member", isAdmin: false, followedUserIds: null, memberGroupIds: otherGroupIds));
    }

    [Fact]
    public void IsTagVisibleInMemory_WhenSystemTag_AlwaysReturnsTrue()
    {
        var rootTag = new Tag { Name = Tag.RootTagName, OwnerId = "user1", IsPrivate = true };
        Assert.True(rootTag.IsTagVisibleInMemory(null, isAdmin: false, followedUserIds: null, memberGroupIds: null));

        var goodTag = new Tag { Name = "good", OwnerId = "user1", IsPrivate = true };
        Assert.True(goodTag.IsTagVisibleInMemory(null, isAdmin: false, followedUserIds: null, memberGroupIds: null));
    }
}