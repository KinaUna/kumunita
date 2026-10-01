using Kumunita.Core.Authorization;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// IP · U02 — pins the <see cref="StandingMatrix.AuditVia"/> two-branch
/// contract (C-IP·1: the standing matrix is one mapping). The matrix is a
/// <b>pure static helper</b> — it maps <c>actorId</c> vs. <c>ownerId</c> to
/// <see cref="AccessVia.Owner"/> / <see cref="AccessVia.Admin"/>; the caller
/// passes the resource's author id. No DI, no session, no storage here.
/// </summary>
public sealed class StandingMatrixTests
{
    [Fact]
    public void AuditVia_Owner_ReturnsOwner()
        => Assert.Equal(AccessVia.Owner, StandingMatrix.AuditVia(actorId: "alice", ownerId: "alice"));

    [Fact]
    public void AuditVia_NonOwner_ReturnsAdmin()
        => Assert.Equal(AccessVia.Admin, StandingMatrix.AuditVia(actorId: "bob", ownerId: "alice"));

    [Fact]
    public void AuditVia_IsOrdinalCaseSensitive()
        => Assert.Equal(AccessVia.Admin, StandingMatrix.AuditVia(actorId: "Alice", ownerId: "alice"));
}
