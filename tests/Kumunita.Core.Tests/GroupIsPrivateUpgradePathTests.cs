using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0010 — the one claim the fresh-DB tests cannot cover: the
/// <em>upgrade path</em> for a deployment that already has rows in the
/// group document table. Marten 9 stores documents as a JSONB
/// <c>data</c> column (there is no per-property column table), so the
/// ADR's "no manual migration" position rests on two things an old
/// deployment must survive when it reboots with the new feature shape:
/// </summary>
public class GroupIsPrivateUpgradePathTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Old-shape docs (the <c>data</c> JSONB of a group written before
    //    the feature landed) read back as public (flag = false) through the
    //    new service, membership still resolves, and the privacy lane works
    //    on the surviving rows — with no DDL migration in between.
    [Fact]
    public async Task Upgrade_LegacyGroupDocsReadBackPublic_AndPrivacyLaneWorks()
    {
        var ct = TestContext.Current.CancellationToken;

        var conn = await fixture.NewDatabaseAsync(ct);
        var store = await BootStoreAsync(conn);
        var svc = new UserInfoService(store);

        // 1. Seed a pre-existing group the way the OLD code would have:
        //    the row exists, its <c>data</c> JSONB has no privacy key, and
        //    the owner membership is in place.
        var group = await svc.CreateGroupAsync(
            "u-legacy-owner", "Legacy building 12", "Pre-upgrade group");
        var data = await GroupDataJsonAsync(conn, group.Id, ct);
        Assert.Contains("IsPrivate", data);
        await StripKeyAsync(conn, group.Id, "IsPrivate", ct);

        // The stripped row is now what a deployment that predates the
        // feature has on disk for this document.
        data = await GroupDataJsonAsync(conn, group.Id, ct);
        Assert.DoesNotContain("IsPrivate", data);

        // 2. Reboot the new code against it (a fresh store = a fresh
        //    process): delta detection runs against the live catalog and
        //    must not fail on the populated table.
        store.Dispose();
        store = await BootStoreAsync(conn);
        var rebooted = new UserInfoService(store);
        await using (var survivor = new NpgsqlConnection(conn))
        {
            await survivor.OpenAsync(ct);
            await using var count = survivor.CreateCommand();
            count.CommandText = "SELECT count(*) FROM mt.mt_doc_group WHERE id = @id";
            count.Parameters.AddWithValue("@id", group.Id);
            Assert.Equal(1L, count.ExecuteScalar()); // no data loss on the (re)apply
        }

        // 3. The legacy doc reads back with the new property on its C#
        //    default (false ⇒ public): it is on the public picker lane,
        //    and the owner's membership resolves (the privacy flag must not
        //    have touched the authorization read).
        var upgraded = await rebooted.GetGroupAsync(group.Id);
        Assert.NotNull(upgraded);
        Assert.Equal("Legacy building 12", upgraded!.Name);
        Assert.Equal("u-legacy-owner", upgraded.OwnerId);
        Assert.False(upgraded.IsPrivate, "a legacy doc without the key must read as public (false)");
        Assert.Contains(group.Id, (await rebooted.GetPublicGroupsAsync()).Select(g => g.Id));
        Assert.Contains(group.Id, await rebooted.GetGroupIdsAsync("u-legacy-owner"));

        // 4. The privacy lane works on the surviving row: the owner can
        //    still make it private, and the flip is live for the picker
        //    lane and lands in the <c>data</c> JSONB (the persisted shape).
        await rebooted.SetGroupPrivacyAsync(group.Id, true, "u-legacy-owner");
        Assert.True((await rebooted.GetGroupAsync(group.Id))!.IsPrivate);
        Assert.DoesNotContain(group.Id, (await rebooted.GetPublicGroupsAsync()).Select(g => g.Id));
        Assert.Contains(
            "IsPrivate",
            await GroupDataJsonAsync(conn, group.Id, ct));
    }

    // ── Shared helper (mirror the group tests' BootStoreAsync) ───────────
    private async Task<IDocumentStore> BootStoreAsync(string conn)
    {
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    // ── JSONB-shape helpers (the upgrade surface is the <c>data</c> column)

    private static async Task<string> GroupDataJsonAsync(string conn, string groupId, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(conn);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT data::text FROM mt.mt_doc_group WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", groupId);
        return (string)cmd.ExecuteScalar()!;
    }

    private static async Task StripKeyAsync(string conn, string groupId, string key, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(conn);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            $"UPDATE mt.mt_doc_group SET data = data - '{key}' WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", groupId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
