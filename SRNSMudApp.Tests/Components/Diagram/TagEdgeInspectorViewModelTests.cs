using SRNSMudApp.Components.Diagram;
using SRNSMudApp.Data;

using Xunit;

namespace SRNSMudApp.Tests.Components.Diagram;

public class TagEdgeInspectorViewModelTests
{
    [Fact]
    public void CanManageEdge_WhenOwnerMatchesCurrentUser_ReturnsTrue()
    {
        var vm = new TagEdgeInspectorViewModel
        {
            CurrentUserId = "user-123",
            Edge = new TagEdge
            {
                Id = 1,
                SourceTagId = 10,
                TargetTagId = 20,
                OwnerId = "user-123"
            }
        };

        Assert.True(vm.CanManageEdge);
    }

    [Theory]
    [InlineData("user-123", "user-456", false)]
    [InlineData("", "user-123", false)]
    [InlineData(null, "user-123", false)]
    public void CanManageEdge_WhenOwnerMismatchOrEmpty_ReturnsFalse(string? currentUserId, string edgeOwnerId, bool expected)
    {
        var vm = new TagEdgeInspectorViewModel
        {
            CurrentUserId = currentUserId ?? string.Empty,
            Edge = new TagEdge
            {
                Id = 1,
                SourceTagId = 10,
                TargetTagId = 20,
                OwnerId = edgeOwnerId
            }
        };

        Assert.Equal(expected, vm.CanManageEdge);
    }

    [Fact]
    public void CanManageEdge_WhenEdgeIsNull_ReturnsFalse()
    {
        var vm = new TagEdgeInspectorViewModel
        {
            CurrentUserId = "user-123",
            Edge = null
        };

        Assert.False(vm.CanManageEdge);
    }

    [Fact]
    public void CanManageAttachment_WhenOwnerMatchesCurrentUser_ReturnsTrue()
    {
        var vm = new TagEdgeInspectorViewModel
        {
            CurrentUserId = "user-123"
        };
        var attachment = new TagEdgeTagAttachment
        {
            Id = 1,
            TagEdgeId = 1,
            TagId = 5,
            OwnerId = "user-123"
        };

        Assert.True(vm.CanManageAttachment(attachment));
    }

    [Fact]
    public void CanManageAttachment_WhenAttachmentNullOrMismatch_ReturnsFalse()
    {
        var vm = new TagEdgeInspectorViewModel
        {
            CurrentUserId = "user-123"
        };
        var otherAttachment = new TagEdgeTagAttachment
        {
            Id = 1,
            TagEdgeId = 1,
            TagId = 5,
            OwnerId = "other-user"
        };

        Assert.False(vm.CanManageAttachment(null));
        Assert.False(vm.CanManageAttachment(otherAttachment));

        vm.CurrentUserId = "";
        Assert.False(vm.CanManageAttachment(new TagEdgeTagAttachment { Id = 1, TagEdgeId = 1, TagId = 5, OwnerId = "" }));
    }

    [Fact]
    public void ToggleTree_TogglesOpenTagId()
    {
        var vm = new TagEdgeInspectorViewModel();
        Assert.Null(vm.OpenTreeTagId);

        vm.ToggleTree(10);
        Assert.Equal(10, vm.OpenTreeTagId);

        // 同じ ID で再度呼ぶと閉じる
        vm.ToggleTree(10);
        Assert.Null(vm.OpenTreeTagId);

        // 別の ID で呼ぶと切り替わる
        vm.ToggleTree(20);
        Assert.Equal(20, vm.OpenTreeTagId);
        vm.ToggleTree(30);
        Assert.Equal(30, vm.OpenTreeTagId);
    }

    [Fact]
    public void CloseTree_ResetsOpenTreeTagIdToNull()
    {
        var vm = new TagEdgeInspectorViewModel();
        vm.ToggleTree(15);
        Assert.Equal(15, vm.OpenTreeTagId);

        vm.CloseTree();
        Assert.Null(vm.OpenTreeTagId);
    }
}