using Kumunita.Core.Notifications;
using Kumunita.Core.Translations;
using Marten;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// Concrete <see cref="ICommunityMembershipService"/> (C07, ADR 0286): the
/// community-management cluster split from <see cref="UserInfoService"/>.
/// </summary>

public sealed class CommunityMembershipService(IDocumentStore store, IServiceProvider? services = null) : ICommunityMembershipService
{
    private readonly UserInfoStore _shared = new(store, services);

    private readonly TranslationLane<Component, CommunityTranslation> _communityTranslationLane =
        new(new TranslationDescriptor<Component, CommunityTranslation>
        {
            TargetKind = "component",
            ActionPrefix = "communitytranslation",
            Noun = "Community",
            ValidateContent = UserInfoStore.NameOrDescriptionRequired,
            LoadStandingExtra = (_, _) => Task.FromResult<object?>(null),
            ResolveStanding = (Component _, object? __, string actorId, IReadOnlySet<string> actorRoles)
                => ResolveCommunityTranslationStanding(actorId, actorRoles),
            LoadParent = (IDocumentSession session, string id) => session.LoadAsync<Component>(id),
            Find = (IDocumentSession session, string id, string lang) => session.Query<CommunityTranslation>()
                .Where(t => t.ComponentId == id && t.LanguageCode == lang)
                .FirstOrDefaultAsync(),
            Create = (string id, string lang, string? name, string? description, string actorId, DateTimeOffset created) => new CommunityTranslation
            {
                Id = Guid.NewGuid().ToString("N"),
                ComponentId = id,
                LanguageCode = lang,
                Name = string.IsNullOrWhiteSpace(name) ? null : name,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                AuthorId = actorId,
                Created = created
            },
            ApplyUpdate = (CommunityTranslation row, string? name, string? description, string actorId, DateTimeOffset now) =>
            {
                row.Name = string.IsNullOrWhiteSpace(name) ? null : name;
                row.Description = string.IsNullOrWhiteSpace(description) ? null : description;
            }
        });

    /// <inheritdoc />
    public async Task<Component> CreateCommunityAsync(string name, string? description, string actorId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Community name is required.", nameof(name));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("Actor id is required.", nameof(actorId));

        // Slug the name and append a short suffix for collision safety. The
        // slug itself is still stable and human-readable (safety, maintenance,
        // social, governance keep their hand-set ids — the slug form never
        // collides with them because we add the suffix).
        var id = NewCommunityId(name);

        // SortOrder: append after the current maximum so the new community
        // lands at the end of the existing list (a sensible default the admin
        // can re-sort via UpdateCommunityAsync).
        int maxSort;
        await using (var q = store.QuerySession())
        {
            var existing = await q.Query<Component>().ToListAsync().ConfigureAwait(false);
            maxSort = existing.Count == 0 ? -1 : existing.Select(c => c.SortOrder).Max();
        }

        var now = DateTimeOffset.UtcNow;
        var component = new Component
        {
            Id = id,
            Name = name,
            Description = description,
            // Icon stays null — the /admin form does not expose it; a future
            // UI (e.g. a picker) can write it via SetIconAsync-style extension.
            SortOrder = maxSort + 1,
            Enabled = true,
            ModeratorAccess = false // C5: OFF by default; only UpdateCommunityAsync may flip it on later.
        };

        await using var session = store.OpenSession(new SessionOptions());
        session.Store(component);
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "community.add",
            TargetKind = "component",
            TargetId = id,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync().ConfigureAwait(false);
        return component;
    }

    /// <inheritdoc />
    public async Task UpdateCommunityAsync(string componentId,
        string? name, string? description,
        int? sortOrder, bool? moderatorAccess, bool? enabled,
        string actorId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
            throw new ArgumentException("Component id is required.", nameof(componentId));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("Actor id is required.", nameof(actorId));
        if (name is not null && string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be blank.", nameof(name));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        if (component is null)
            throw new InvalidOperationException($"Community not found: {componentId}");

        // Patch semantics: a null argument is "keep as-is", not "clear". This
        // is what lets the /admin "edit" form round-trip safely — an unchecked
        // checkbox doesn't erase the current value.
        if (name is not null) component.Name = name;
        if (description is not null) component.Description = description;
        if (sortOrder is not null) component.SortOrder = sortOrder.Value;
        if (moderatorAccess is not null) component.ModeratorAccess = moderatorAccess.Value;
        if (enabled is not null) component.Enabled = enabled.Value;

        session.Store(component);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "community.update",
            TargetKind = "component",
            TargetId = componentId,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetCommunityEnabledAsync(string componentId, bool enabled, string actorId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
            throw new ArgumentException("Component id is required.", nameof(componentId));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("Actor id is required.", nameof(actorId));

        var now = DateTimeOffset.UtcNow;

        await using var session = store.OpenSession(new SessionOptions());

        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        if (component is null)
            throw new InvalidOperationException($"Community not found: {componentId}");

        component.Enabled = enabled;
        session.Store(component);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = enabled ? "community.enable" : "community.disable",
            TargetKind = "component",
            TargetId = componentId,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ComponentMembership>> GetCommunityMembersAsync(string componentId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
            throw new ArgumentException("Component id is required.", nameof(componentId));

        // Live-row read (invariant C4), the GetGroupMembersAsync analog on
        // the component axis: the explicit membership rows of one Component,
        // as an access decision. No audit row (a read, not a decision).
        await using var session = store.QuerySession();
        return await session
            .Query<ComponentMembership>()
            .Where(m => m.ComponentId == componentId)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommunityTranslation>> GetCommunityTranslationsAsync(string componentId)
    {
        if (string.IsNullOrEmpty(componentId)) throw new ArgumentException("A component id is required.", nameof(componentId));

        await using var session = store.QuerySession();
        return await session
            .Query<CommunityTranslation>()
            .Where(t => t.ComponentId == componentId)
            .OrderBy(t => t.LanguageCode)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CommunityTranslation> AddCommunityTranslationAsync(
        string componentId, string languageCode, string? name, string? description,
        string actorId, IReadOnlySet<string> actorRoles, Marten.IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return await _communityTranslationLane.AddAsync(session, componentId, languageCode, name, description, actorId, actorRoles).ConfigureAwait(false);
    }

    /// <summary>
    /// Session-owning overload (C2-04 — the Web controller no longer opens its
    /// own session; the service owns the write + the single
    /// <c>SaveChangesAsync</c>). Behaviour is identical to the
    /// <c>IDocumentSession</c> overload — the only difference is that the service
    /// opens the <c>LightweightSession</c> internally (the C3 single-write-lane is
    /// unchanged: the same <see cref="Authorization.AccessAudit"/> row + the same
    /// <c>CommunityTranslation</c> row commit atomically in one session).
    /// </summary>
    public async Task<CommunityTranslation> AddCommunityTranslationAsync(
        string componentId, string languageCode, string? name, string? description,
        string actorId, IReadOnlySet<string> actorRoles)
    {
        await using var session = store.LightweightSession();
        return await AddCommunityTranslationAsync(componentId, languageCode, name, description, actorId, actorRoles, session).ConfigureAwait(false);
    }

    /// <summary>
    /// **Updates** the existing <see cref="CommunityTranslation"/> row for
    /// (<paramref name="componentId"/>, <paramref name="languageCode"/>) in the
    /// <b>caller's</b> in-flight session (C3). Standing is the same as the ADR
    /// 0026 add lane (GlobalAdmin / Translator → Admin; the community has no
    /// owner). A missing component or row is a <see cref="KeyNotFoundException"/>;
    /// a denied actor throws <see cref="UnauthorizedAccessException"/>. The new
    /// name / description must have at least one non-blank field. One
    /// <c>SaveChangesAsync</c>.
    /// </summary>
    public async Task<CommunityTranslation> UpdateCommunityTranslationAsync(
        string componentId, string languageCode, string? name, string? description,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return await _communityTranslationLane.UpdateAsync(session, componentId, languageCode, name, description, actorId, actorRoles).ConfigureAwait(false);
    }

    /// <summary>
    /// Session-owning overload (C2-04) — the Web controller no longer opens its
    /// own session; the service owns the write + the single
    /// <c>SaveChangesAsync</c>. Identical behaviour to the <c>IDocumentSession</c>
    /// overload.
    /// </summary>
    public async Task<CommunityTranslation> UpdateCommunityTranslationAsync(
        string componentId, string languageCode, string? name, string? description,
        string actorId, IReadOnlySet<string> actorRoles)
    {
        await using var session = store.LightweightSession();
        return await UpdateCommunityTranslationAsync(componentId, languageCode, name, description, actorId, actorRoles, session).ConfigureAwait(false);
    }

    /// <summary>
    /// **Removes** the existing <see cref="CommunityTranslation"/> row for
    /// (<paramref name="componentId"/>, <paramref name="languageCode"/>) in the
    /// <b>caller's</b> in-flight session (C3). Standing is the same as the ADR
    /// 0026 add lane. A hard <c>session.Delete</c>; the trail is preserved by
    /// the <see cref="Authorization.AccessAudit"/> row (action
    /// <c>communitytranslation.remove</c>). A missing component or row is a
    /// <see cref="KeyNotFoundException"/>. One <c>SaveChangesAsync</c>.
    /// </summary>
    public async Task RemoveCommunityTranslationAsync(
        string componentId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        await _communityTranslationLane.RemoveAsync(session, componentId, languageCode, actorId, actorRoles).ConfigureAwait(false);
    }

    /// <summary>
    /// Session-owning overload (C2-04) — the Web controller no longer opens its
    /// own session; the service owns the write + the single
    /// <c>SaveChangesAsync</c>. Identical behaviour to the <c>IDocumentSession</c>
    /// overload.
    /// </summary>
    public async Task RemoveCommunityTranslationAsync(
        string componentId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles)
    {
        await using var session = store.LightweightSession();
        await RemoveCommunityTranslationAsync(componentId, languageCode, actorId, actorRoles, session).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public bool CanTranslateCommunity(string actorId, IReadOnlySet<string> actorRoles)
        => ResolveCommunityTranslationStanding(actorId, actorRoles) is not null;

    /// <summary>
    /// The ADR 0026 community-translation standing resolver (shared by
    /// <see cref="AddCommunityTranslationAsync"/> and <see
    /// cref="CanTranslateCommunity"/>). Returns the <see cref="Authorization
    /// .AccessVia"/> the actor qualifies under, or <c>null</c> to deny. A
    /// community has **no owner** (no <see cref="Authorization.AccessVia
    /// .Owner"/> branch) — its name is a GlobalAdmin artifact (the
    /// <c>/admin</c> create/update lane, <c>Via: Admin</c>) — so the admitted
    /// standings are a <b>GlobalAdmin</b> or a <b>Translator</b> (both
    /// <see cref="Authorization.AccessVia.Admin"/>). A component-moderator is
    /// not (a moderator governs a community's *members*, ADR 0012, not its
    /// name).
    /// </summary>
    private static Authorization.AccessVia? ResolveCommunityTranslationStanding(
        string actorId, IReadOnlySet<string> actorRoles)
    {
        if (actorRoles.Contains(Identity.Roles.GlobalAdmin))
            return Authorization.AccessVia.Admin;
        if (actorRoles.Contains(Identity.Roles.Translator))
            return Authorization.AccessVia.Admin;
        return null;
    }

    /// <summary>
    /// The <c>Component.Id</c> for a new admin-created community: the lowercased
    /// name, non-alphanumeric runs collapsed to a single "<c>-</c>", with a short
    /// 4-hex-digit random suffix appended to guarantee uniqueness against
    /// concurrent admin adds. (The four seeded ids — <c>safety</c>,
    /// <c>maintenance</c>, <c>social</c>, <c>governance</c> — keep their
    /// hand-set identity; this slug form can never collide with them because
    /// it always carries the trailing "-xxxx" suffix.)
    /// </summary>
    private static string NewCommunityId(string name)
    {
        const char dash = '-';
        var sb = new System.Text.StringBuilder(name.Length + 5);
        var lastDash = false;
        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastDash = false;
            }
            else if (!lastDash)
            {
                sb.Append(dash);
                lastDash = true;
            }
        }
        if (sb.Length > 0 && sb[^1] == dash) sb.Length--;
        if (sb.Length == 0) sb.Append("x"); // guard against an all-punctuation name.

        var suffix = Guid.NewGuid().ToString("N")[..4];
        return $"{sb.ToString()}-{suffix}";
    }

}
