using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// M1's Marten-native document registration surface (ADR 0004 §B.1). M1 domain documents
/// are <em>Marten-native</em>: Marten derives their <c>mt</c> tables from the POCO shapes,
/// and this class centralises the small handful of conventions that Marten's defaults
/// would NOT pick up out of the box.
/// <para>
/// Conventions this surface pins:
/// </para>
/// <list type="bullet">
/// <item>
/// <see cref="Profile"/> — its identity is <c>SubjectId</c>, not <c>Id</c>; the
/// <c>Schema.For&lt;T&gt;().Identity(...)</c> call pins that (Marten 9 has no
/// <c>[Id]</c> attribute — identity is a mapping-configured property).
/// </item>
/// <item>
/// <see cref="GroupMembership"/> — one row per (group, user) pair. The unique index
/// on (<c>GroupId</c>, <c>UserId</c>) enforces the business key at the database layer
/// (Marten's document identity uses the surrogate <c>Id</c>).
/// </item>
/// </list>
/// <para>
/// Every other M1 POCO (<c>Group</c>, <c>DelegationGrant</c>, <c>Component</c>,
/// <c>ModeratorAssignment</c>, <c>IdentityToken</c>, <c>AccessAudit</c>,
/// <c>AuditPurgeSummary</c>) is a conventional <c>Id</c> string identity and needs
/// no explicit mapping here — Marten's defaults apply.
/// </para>
/// <para>
/// Wire it up by calling <see cref="Configure"/> on the <see cref="StoreOptions"/>
/// after <c>AddMarten(...)</c> in the host startup path; the boot block then applies
/// the delta via the existing <c>ApplyAllConfiguredChangesToDatabaseAsync()</c>.
/// </para>
/// </summary>
public static class M1DocTypes
{
    /// <summary>
    /// Registers the M1 domain documents and pins the small non-default conventions
    /// (identity + business-key) they need. Idempotent: calling twice is safe —
    /// Marten's <c>Schema.For&lt;T&gt;()</c> returns the same builder each time.
    /// </summary>
    public static void Configure(StoreOptions opts)
    {
        // UserInfo
        opts.Schema.For<Profile>().Identity(p => p.SubjectId);
        opts.Schema.For<Group>();
        opts.Schema.For<GroupMembership>()
               .UniqueIndex(m => m.GroupId, m => m.UserId);   // business key
        // M2b (owner-invited group membership): the invitation row, the same
        // one-row-per-(group, user) business-key convention as GroupMembership —
        // re-inviting after a resolution resets the existing Pending row rather
        // than inserting a second one (C-M2b·3).
        opts.Schema.For<GroupInvitation>()
               .UniqueIndex(i => i.GroupId, i => i.UserId);   // business key
        opts.Schema.For<DelegationGrant>();
        opts.Schema.For<Component>();
        opts.Schema.For<ModeratorAssignment>();
        // Posting right: one row per (component, user) pair, the same business-key
        // convention as GroupMembership. The unique index enforces the pair at the
        // DB layer (the surrogate Id is the Marten document identity).
        opts.Schema.For<ComponentMembership>()
               .UniqueIndex(m => m.ComponentId, m => m.UserId);

        // Identity
        opts.Schema.For<IdentityToken>();

        // Outbox (plan M1 step 6/7): the staged email row the domain write commits with
        // (IMailerStage / OutboxEmailStager, invariant C3) and the domain dead-letter
        // document the durable handler writes on final failure (ARCHITECTURE.md §5/§6.2).
        // Both are Marten-native documents (conventional string Id). Registered here so
        // the mt.tables for them are created on first boot / forward migration, the same
        // way the step-6 seeder's `session.Store<OutboxEmail>` expects them.
        opts.Schema.For<OutboxEmail>();
        opts.Schema.For<EmailDeadLetter>();

        // Localization (ADR 0005, the ML lane): the language catalog + the
        // per-instance default (the seeder materializes the `en` row and a default
        // of `en` on first boot — already shipped), plus the two content documents
        // this lane ships (the ADR's module surface on top of that seed).
        // The pair idiom (surrogate Id + unique index on the business-key pair) is
        // the GroupMembership / ComponentMembership convention: one text per key
        // per language, one page per slug per language. Rows are **retained** when
        // a language is removed (M·7) — that retention is the ILocalizationService's
        // job (U3), not a delete here.
        opts.Schema.For<LanguageCatalog>();
        opts.Schema.For<LocaleSettings>();
        opts.Schema.For<TranslationResource>()
               .UniqueIndex(t => t.Key, t => t.LanguageCode);   // business key (one text per key per language)
        opts.Schema.For<LocalizedPage>()
               .UniqueIndex(p => p.Slug, p => p.LanguageCode);  // business key (one page per slug per language)

        // Authorization (audit only — AdminOverride is hand-rolled, see AuthorizationFeature)
        opts.Schema.For<AccessAudit>();
        opts.Schema.For<AuditPurgeSummary>();
    }
}
