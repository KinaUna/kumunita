using Kumunita.Core.Authorization;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// IP · U01 — pins the <see cref="AccessAuditFactory.SingleTarget"/> contract
/// (C-IP·1: the audit row is one contract). The factory is a <b>pure static
/// helper</b> — it returns a fully-populated single-target
/// <see cref="AccessAudit"/> with <c>EffectivePrincipalId = actorId</c> (no
/// delegation in write lanes), the counts left <c>null</c> (the single-target
/// shape, not the aggregate row), and a default <see cref="AccessOutcome.Allow"/>
/// that callers can override. No DI, no session, no storage here — the caller
/// stores the row in their own <c>IDocumentSession</c> (C3 same-transaction).
/// </summary>
public sealed class AccessAuditFactoryTests
{
    [Fact]
    public void SingleTarget_SetsAllFields()
    {
        var row = AccessAuditFactory.SingleTarget(
            actorId: "actor-1",
            action: "event.create",
            targetKind: "event",
            targetId: "evt-1",
            via: AccessVia.Owner);

        Assert.Equal("actor-1", row.ActorId);
        Assert.Equal("actor-1", row.EffectivePrincipalId);
        Assert.Equal("event.create", row.Action);
        Assert.Equal("event", row.TargetKind);
        Assert.Equal("evt-1", row.TargetId);
        Assert.Equal(AccessVia.Owner, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);   // the default
        Assert.NotNull(row.Id);
        Assert.NotEmpty(row.Id);
        Assert.True(row.At > DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Null(row.VisibleCount);
        Assert.Null(row.HiddenCount);
    }

    [Fact]
    public void SingleTarget_ExplicitDeny_SetsOutcome()
    {
        var row = AccessAuditFactory.SingleTarget(
            "actor-2", "inventory.checkout", "inventory", "item-1",
            AccessVia.Admin, AccessOutcome.Deny);

        Assert.Equal(AccessOutcome.Deny, row.Outcome);
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal("item-1", row.TargetId);
    }
}
