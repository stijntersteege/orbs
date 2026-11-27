using Orbs.Domain.Common;
using Orbs.Domain.Conversations;
using Orbs.Domain.Orbs;

namespace Orbs.Domain.Tests;

public class ToolAccessTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Tools_ask_first_unless_the_orb_says_otherwise()
    {
        var orb = Orb.Create("Pip", Now);
        orb.PermissionFor("calculator").ShouldBe(ToolAccess.Ask);

        orb.SetToolPermission("calculator", ToolAccess.Allow, Now);
        orb.SetToolPermission("send_email", ToolAccess.Block, Now);

        orb.PermissionFor("calculator").ShouldBe(ToolAccess.Allow);
        orb.PermissionFor("send_email").ShouldBe(ToolAccess.Block);
    }

    [Fact]
    public void Setting_the_default_again_removes_the_rule()
    {
        var orb = Orb.Create("Pip", Now);
        orb.SetToolPermission("calculator", ToolAccess.Allow, Now);

        orb.SetToolPermission("calculator", ToolAccess.Ask, Now);

        orb.ToolPermissions.ShouldBeEmpty();
    }

    [Fact]
    public void An_approval_is_answered_once()
    {
        var approval = ToolApproval.Request(Guid.NewGuid(), Guid.NewGuid(), "c1", "calculator", "{}", "{}", Now);

        approval.Decide(approve: true, Now);

        approval.Status.ShouldBe(ApprovalStatus.Approved);
        Should.Throw<DomainException>(() => approval.Decide(approve: false, Now));
    }

    [Fact]
    public void Only_pending_approvals_expire()
    {
        var approval = ToolApproval.Request(Guid.NewGuid(), Guid.NewGuid(), "c1", "calculator", "{}", "{}", Now);
        approval.Decide(approve: false, Now);

        approval.Expire(Now);

        approval.Status.ShouldBe(ApprovalStatus.Denied);
    }
}
