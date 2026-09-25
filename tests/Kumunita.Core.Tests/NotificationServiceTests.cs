using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bootstrap;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The <see cref="NotificationService"/> (M6 — the **shared awareness** arrow,
/// ADR 0076) business-logic harness: the **12 FACES** from design doc §6.4
/// (F1–F12), one <c>Method_Face_Expectation</c> test each, run against a live
/// scratch-Postgres <see cref="IDocumentStore"/> (<see cref="PostgresFixture"/>)
/// with the frozen <see cref="IMailerStage"/> stand-in that *records* the staged
/// emails (key + recipient + subject + body) rather than dispatching over SMTP
/// (the <see cref="EventReminderServiceTests"/> recording-mailer precedent), and
/// a <see cref="ITranslationProvider"/> stand-in that resolves the
/// <c>notification.{kind}.subject</c> / <c>notification.{kind}.body</c> keys in
/// the *recipient's* <c>Profile.EmailLanguage</c> (the ADR 0061 seam).
/// <para>
/// The frozen six-method surface (U03 — design doc §6.2) is exercised
/// verbatim: <c>EmitAsync</c> (the writer — D4 / D5 / D7 + the F10 service-side
/// <c>IdempotencyKey</c> dedup), <c>ListInboxAsync</c> /
/// <c>CountUnreadAsync</c> / <c>MarkAllReadAsync</c> (the read + state lanes),
/// and <c>GetPreferencesAsync</c> / <c>SetPreferencesAsync</c> (the
/// preference lanes). No <c>IAuthorizationService</c> anywhere (C-M6·3 — a
/// notification is a *personal read*, not an <c>AccessAction</c> decision);
/// the F11 test asserts that directly (zero <see cref="AccessAudit"/> rows).
/// </para>
/// <para>
/// **F7 (U04's recorded drift):** the design doc §6.4 name
/// <c>Emit_ReportResolved_Stores_InboxRow_And_Stages_Email_For_Resident</c>
/// reads "the resident" generically; U04 shipped only the **filer** emission
/// (the <c>notification:report.resolved:{reportId}:filed</c> key — the frozen
/// <c>Report</c> entity has no assigned-moderator field, so the assignee
/// emission is a follow-on lane). This test codes against **what shipped**:
/// one row for the filer. Recorded as a drift note for U10 to settle at gate
/// time — the test name is kept verbatim (a rename is a breaking change to §6.4).
/// </para>
/// </summary>
public class NotificationServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── F1 — Emit_PostReply_Stores_InboxRow_And_Stages_Email_For_Author ─────
    //
    // C-M6·2 (the closed kind set — the stored <c>Kind</c> is the code-owned
    // <c>post.reply</c> constant) + C-M6·7 (the inbox row is stored
    // unconditionally; the email is staged because the recipient's preference
    // is the lean-default = all enabled). One row for the post's author, with
    // the §6.3 key shape <c>notification:post.reply:{replyId}</c>.

    [Fact]
    public async Task Emit_PostReply_Stores_InboxRow_And_Stages_Email_For_Author()
    {
        var (store, svc, mailer, staged) = await BootAsync();

        const string author = "u-f1-author";
        const string replier = "u-f1-replier";
        await PlantProfile(store, author, "f1-author@kumunita", emailLanguage: "de");

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await Emit(svc, session, author,
            NotificationKinds.PostReply,
            "notification:post.reply:reply-f1", "Neuer Kommentar von " + replier);

        // The inbox row is the durable record (D5) — stored once, unread.
        Assert.Equal(author, row.RecipientId);
        Assert.Equal(NotificationKinds.PostReply, row.Kind);
        Assert.Equal("reply-f1", row.SourceId);                     // derived from the §6.3 key
        Assert.Null(row.ReadAt);
        Assert.Equal(1, await CountNotifications(store, author));
        // C-M6·7: lean-default → the email is staged exactly once, same key.
        var email = Assert.Single(staged);
        Assert.Equal("notification:post.reply:reply-f1", email.Key);
        Assert.Equal("f1-author@kumunita", email.Recipient);
    }

    // ── F2 — Emit_GroupPost_Stores_InboxRow_And_Stages_Email_For_Member ─────
    //
    // C-M6·2 (the <c>group.post</c> constant) + C-M6·7 (inbox unconditional;
    // email gated by the preference). A group post notifies **each member** —
    // the emitter (U04) calls <c>EmitAsync</c> once per member with the
    // per-member §6.3 key <c>notification:group.post:{postId}:{member}</c>; the
    // service stores one row per call. Here the service is exercised for the
    // *member* recipient: one row + one staged email for that member.

    [Fact]
    public async Task Emit_GroupPost_Stores_InboxRow_And_Stages_Email_For_Member()
    {
        var (store, svc, _, staged) = await BootAsync();

        const string author = "u-f2-author";
        const string member = "u-f2-member";
        await PlantProfile(store, member, "f2-member@kumunita");

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await Emit(svc, session, member,
            NotificationKinds.GroupPost,
            "notification:group.post:post-f2:" + member, "Neuer Gruppenbeitrag");

        Assert.Equal(member, row.RecipientId);
        Assert.Equal(NotificationKinds.GroupPost, row.Kind);
        Assert.Equal($"post-f2:{member}", row.SourceId);            // the per-member stable source id
        Assert.Null(row.ReadAt);
        Assert.Equal(1, await CountNotifications(store, member));
        var email = Assert.Single(staged);
        Assert.Equal("notification:group.post:post-f2:" + member, email.Key);
        Assert.Equal("f2-member@kumunita", email.Recipient);
        // The author is not this member — no row for the author in this call.
        Assert.Equal(0, await CountNotifications(store, author));
    }

    // ── F3 — Emit_EventRsvp_Stores_InboxRow_And_Stages_Email_For_Author ─────
    //
    // C-M6·2 (the <c>event.rsvp</c> constant) + C-M6·7. A resident RSVP'd to an
    // event the resident authored → one row for the event author, key
    // <c>notification:event.rsvp:{rsvpId}</c>.

    [Fact]
    public async Task Emit_EventRsvp_Stores_InboxRow_And_Stages_Email_For_Author()
    {
        var (store, svc, _, staged) = await BootAsync();

        const string author = "u-f3-author";
        await PlantProfile(store, author, "f3-author@kumunita");

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await Emit(svc, session, author,
            NotificationKinds.EventRsvp, "notification:event.rsvp:rsvp-f3", "Hat sich angemeldet");

        Assert.Equal(author, row.RecipientId);
        Assert.Equal(NotificationKinds.EventRsvp, row.Kind);
        Assert.Equal("rsvp-f3", row.SourceId);
        Assert.Null(row.ReadAt);
        Assert.Equal(1, await CountNotifications(store, author));
        var email = Assert.Single(staged);
        Assert.Equal("notification:event.rsvp:rsvp-f3", email.Key);
        Assert.Equal("f3-author@kumunita", email.Recipient);
    }

    // ── F4 — Emit_EventReminder_Stores_InboxRow_Email_Is_M4s ────────────────
    //
    // C-M6·2 (the <c>event.reminder</c> constant) + C-M6·5 (the inbox row is
    // M6's; the *email* is **M4's**). The M4 reminder email is staged directly
    // by <c>EventReminderService</c> with the frozen
    // <c>remind:{eventId}:{userId}</c> key (ADR 0054) — it does **not** flow
    // through <c>EmitAsync</c>. If M6 staged an email here too, a reminder
    // recipient would get two (the keys differ, so the outbox dedup can't
    // catch the second — F10), and a resident who disabled the kind (F9)
    // would still get the M4 email. So this M6 emit stores the inbox row and
    // stages **no** email.

    [Fact]
    public async Task Emit_EventReminder_Stores_InboxRow_Email_Is_M4s()
    {
        var (store, svc, _, staged) = await BootAsync();

        const string recipient = "u-f4-recipient";
        await PlantProfile(store, recipient, "f4@kumunita");

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        // The M6 inbox row (this test's subject) — the §6.3 shape with the
        // recipient's formatted {date} (U04's recorded superset of {date}).
        var m6Row = await Emit(svc, session, recipient,
            NotificationKinds.EventReminder,
            "notification:event.reminder:ev-f4:2026-09-24", "Event morgen");

        // The inbox row is M6's durable record (C-M6·5 / F4) …
        Assert.Equal(NotificationKinds.EventReminder, m6Row.Kind);
        Assert.Equal("ev-f4:2026-09-24", m6Row.SourceId);
        Assert.Null(m6Row.ReadAt);
        Assert.Equal(1, await CountNotifications(store, recipient));
        // … and this M6 emit stages **no** email (the email is M4's — see
        // the F4 header; double-staging here would send it twice).
        Assert.Empty(staged);
    }

    // ── F5 — Emit_ReportFiled_Stores_InboxRow_And_Stages_Email_For_Author ───
    //
    // C-M6·2 (the <c>report.filed</c> constant) + C-M6·7. A report is filed
    // against content the resident authored → one row for the author, key
    // <c>notification:report.filed:{reportId}</c>.

    [Fact]
    public async Task Emit_ReportFiled_Stores_InboxRow_And_Stages_Email_For_Author()
    {
        var (store, svc, _, staged) = await BootAsync();

        const string author = "u-f5-author";
        await PlantProfile(store, author, "f5-author@kumunita");

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await Emit(svc, session, author,
            NotificationKinds.ReportFiled,
            "notification:report.filed:report-f5", "Dein Beitrag wurde gemeldet");

        Assert.Equal(author, row.RecipientId);
        Assert.Equal(NotificationKinds.ReportFiled, row.Kind);
        Assert.Equal("report-f5", row.SourceId);
        Assert.Null(row.ReadAt);
        Assert.Equal(1, await CountNotifications(store, author));
        var email = Assert.Single(staged);
        Assert.Equal("notification:report.filed:report-f5", email.Key);
        Assert.Equal("f5-author@kumunita", email.Recipient);
    }

    // ── F6 — Emit_ReportAssigned_Stores_InboxRow_And_Stages_Email_For_Moderator
    //
    // C-M6·2 (the <c>report.assigned</c> constant) + C-M6·7. A report is
    // assigned to a moderator → one row for that moderator, key
    // <c>notification:report.assigned:{reportId}</c>.

    [Fact]
    public async Task Emit_ReportAssigned_Stores_InboxRow_And_Stages_Email_For_Moderator()
    {
        var (store, svc, _, staged) = await BootAsync();

        const string moderator = "u-f6-moderator";
        await PlantProfile(store, moderator, "f6-mod@kumunita");

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await Emit(svc, session, moderator,
            NotificationKinds.ReportAssigned,
            "notification:report.assigned:report-f6", "Ihnen zugewiesen");

        Assert.Equal(moderator, row.RecipientId);
        Assert.Equal(NotificationKinds.ReportAssigned, row.Kind);
        Assert.Equal("report-f6", row.SourceId);
        Assert.Null(row.ReadAt);
        Assert.Equal(1, await CountNotifications(store, moderator));
        var email = Assert.Single(staged);
        Assert.Equal("notification:report.assigned:report-f6", email.Key);
        Assert.Equal("f6-mod@kumunita", email.Recipient);
    }

    // ── F7 — Emit_ReportResolved_Stores_InboxRow_And_Stages_Email_For_Resident
    //
    // C-M6·2 (the <c>report.resolved</c> constant) + C-M6·7. **Shipped shape
    // (U04's drift, recorded for U10):** U04 wired **only the filer**
    // emission — the frozen <c>Report</c> entity (M3b) has no
    // assigned-moderator field, so the assignee emission is a follow-on lane.
    // This test codes against the shipped form: **one** row for the filer,
    // keyed <c>notification:report.resolved:{reportId}:filed</c>. The test
    // name is kept verbatim from §6.4 (a rename is a breaking change).
    //
    // The §6.4 name reads "…For_Resident" — the shipped "resident" is the
    // filer. The assignee row (the design doc's "two rows" reading) is NOT
    // asserted here; see the handoff-note drift record.

    [Fact]
    public async Task Emit_ReportResolved_Stores_InboxRow_And_Stages_Email_For_Resident()
    {
        var (store, svc, _, staged) = await BootAsync();

        const string filer = "u-f7-filer";
        await PlantProfile(store, filer, "f7-filer@kumunita");

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await Emit(svc, session, filer,
            NotificationKinds.ReportResolved,
            "notification:report.resolved:report-f7:filed", "Die Meldung zu deinem Beitrag wurde aufgelöst");

        Assert.Equal(filer, row.RecipientId);
        Assert.Equal(NotificationKinds.ReportResolved, row.Kind);
        Assert.Equal("report-f7:filed", row.SourceId);
        Assert.Null(row.ReadAt);
        Assert.Equal(1, await CountNotifications(store, filer));
        var email = Assert.Single(staged);
        Assert.Equal("notification:report.resolved:report-f7:filed", email.Key);
        Assert.Equal("f7-filer@kumunita", email.Recipient);
    }

    // ── F8 — Emit_TodoAssign_Stores_InboxRow_And_Stages_Email_For_Resident ──
    //
    // C-M6·2 (the <c>todo.assign</c> constant) + C-M6·7. A to-do is assigned to
    // a resident (person-only — the group / community assign is a follow-on
    // lane, not a test failure here) → one row for the assignee, key
    // <c>notification:todo.assign:{todoId}</c>.

    [Fact]
    public async Task Emit_TodoAssign_Stores_InboxRow_And_Stages_Email_For_Resident()
    {
        var (store, svc, _, staged) = await BootAsync();

        const string assignee = "u-f8-assignee";
        await PlantProfile(store, assignee, "f8-assignee@kumunita");

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await Emit(svc, session, assignee,
            NotificationKinds.TodoAssign,
            "notification:todo.assign:todo-f8", "Eine Aufgabe wurde dir zugewiesen");

        Assert.Equal(assignee, row.RecipientId);
        Assert.Equal(NotificationKinds.TodoAssign, row.Kind);
        Assert.Equal("todo-f8", row.SourceId);
        Assert.Null(row.ReadAt);
        Assert.Equal(1, await CountNotifications(store, assignee));
        var email = Assert.Single(staged);
        Assert.Equal("notification:todo.assign:todo-f8", email.Key);
        Assert.Equal("f8-assignee@kumunita", email.Recipient);
    }

    // ── F9 — Emit_DisabledKind_Stores_InboxRow_But_Not_Email ────────────────
    //
    // C-M6·7 (the D7 email gate): a resident who has **disabled** a kind in
    // their <see cref="NotificationPreference"/> **still gets the inbox row**
    // (the inbox is the durable record) but **not the email** (the preference
    // governs the *email*, not the *inbox*). The service resolves the
    // preference on the caller's session before staging; a non-null,
    // non-empty <c>KindsEnabled</c> that omits the kind suppresses the email.

    [Fact]
    public async Task Emit_DisabledKind_Stores_InboxRow_But_Not_Email()
    {
        var (store, svc, _, staged) = await BootAsync();

        const string recipient = "u-f9-recipient";
        await PlantProfile(store, recipient, "f9@kumunita");

        // The recipient has disabled <c>post.reply</c> (a non-empty enabled
        // set that does NOT contain the kind) — the D7 gate's suppression case.
        await svc.SetPreferencesAsync(
            recipient,
            new[] { NotificationKinds.TodoAssign },          // enabled: todo.assign only
            TestContext.Current.CancellationToken);

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await Emit(svc, session, recipient,
            NotificationKinds.PostReply,               // the disabled kind
            "notification:post.reply:reply-f9", "Deaktiviert");

        // The inbox row is stored **unconditionally** (D5 / F9).
        Assert.Equal(NotificationKinds.PostReply, row.Kind);
        Assert.Equal(1, await CountNotifications(store, recipient));
        // ...but the email is NOT staged (the D7 gate suppresses it).
        Assert.Empty(staged);
    }

    // ── F10 — Emit_DuplicateKey_SameLogicalEvent_Is_NoOp ────────────────────
    //
    // C-M6·4 (D4 / F10): a **re-emission of the same logical event** (the same
    // <c>idempotencyKey</c>) is a **no-op** — no second inbox row, no second
    // email. The service-side dedup is the <c>IdempotencyKey</c> look-up in
    // <c>EmitAsync</c> (design doc §6.3's two-layer pin, layer 1): a second
    // <c>EmitAsync</c> call with the same key returns the existing row without
    // storing a second one and without calling <c>StageAsync</c> again.

    [Fact]
    public async Task Emit_DuplicateKey_SameLogicalEvent_Is_NoOp()
    {
        var (store, svc, _, staged) = await BootAsync();

        const string recipient = "u-f10-recipient";
        await PlantProfile(store, recipient, "f10@kumunita");

        const string key = "notification:post.reply:reply-f10";

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var first = await Emit(svc, session, recipient,
            NotificationKinds.PostReply, key, "Erste");

        // The re-emission — same key, same logical event.
        var second = await Emit(svc, session, recipient,
            NotificationKinds.PostReply, key, "Zweite");

        // ...returns the **same** row (the existing one), not a new one.
        Assert.Equal(first.Id, second.Id);
        // Exactly ONE inbox row — no second row.
        Assert.Equal(1, await CountNotifications(store, recipient));
        // Exactly ONE staged email — no second <c>StageAsync</c> call.
        Assert.Single(staged, s => s.Key == key);
    }

    // ── F11 — ListInbox_Does_Not_Emit_AuditRow ───────────────────────────────
    //
    // C-M6·3 (D3 / F11): the inbox read (<c>ListInboxAsync</c> +
    // <c>CountUnreadAsync</c>) does **not** emit an audit row — a *personal
    // read*, not an <c>AccessAction</c> decision (the recipient reads their
    // own rows; the <c>RecipientId</c> is the whole access story). Asserted
    // against the live store: zero <see cref="AccessAudit"/> rows after the
    // read + the unread count (the <see cref="EventReminderServiceTests"/>
    // no-audit-row precedent).

    [Fact]
    public async Task ListInbox_Does_Not_Emit_AuditRow()
    {
        var (store, svc, _, staged) = await BootAsync();

        const string recipient = "u-f11-recipient";
        await PlantProfile(store, recipient, "f11@kumunita");

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        await Emit(svc, session, recipient,
            NotificationKinds.PostReply,
            "notification:post.reply:reply-f11", "Ein Eintrag");

        // The personal read — no <c>IAuthorizationService</c> call, no audit row.
        var inbox = await svc.ListInboxAsync(recipient, TestContext.Current.CancellationToken);
        var unread = await svc.CountUnreadAsync(recipient, TestContext.Current.CancellationToken);

        Assert.Single(inbox);
        Assert.Equal(1, unread);
        // The no-audit-row pin: zero AccessAudit rows after the read.
        Assert.Empty(await AuditRows(store));
    }

    // ── F12 — Emit_Email_In_Recipients_Language_UgcSnippet_In_Senders_Language
    //
    // C-M6·6 (D6 / F12): the **subject + body templates** are resolved in the
    // **recipient's** <c>Profile.EmailLanguage</c> (the ADR 0061 seam — the
    // <see cref="ITranslationProvider"/> receives the recipient's language
    // code, not the sender's); the **UGC snippet** (the
    // <c>body</c> parameter — the sender's authored content, ADR 0018) is
    // appended verbatim in the **sender's** authored language. The
    // <c>Notification.Subject</c> / <c>Body</c> stored on the row (and the
    // staged email's subject + body) reflect exactly that split.

    [Fact]
    public async Task Emit_Email_In_Recipients_Language_UgcSnippet_In_Senders_Language()
    {
        var (store, svc, translator, staged) = await BootAsync();

        const string recipient = "u-f12-recipient";
        // The recipient's outbound-channel language is **German** (ADR 0061 —
        // the recipient's choice is the authority; the sender's UI language is
        // irrelevant).
        await PlantProfile(store, recipient, "f12@kumunita", emailLanguage: "de");

        const string snippet = "English-authored UGC snippet";   // the sender's authored language (ADR 0018)

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await Emit(svc, session, recipient,
            NotificationKinds.PostReply, "notification:post.reply:reply-f12", snippet);

        // The subject + body templates were resolved in the **recipient's**
        // language — the <c>ITranslationProvider</c> was called with "de".
        await translator.Received(2).GetAsync(
            Arg.Any<string>(), "de");                             // subject + body, both in de

        // The staged email's subject is the recipient-language template (no
        // UGC snippet — the subject is template-only).
        var email = Assert.Single(staged);
        Assert.Equal("SUBJ-de", email.Subject);
        // The staged email's body = the recipient-language template + " " + the
        // **sender-authored** UGC snippet (verbatim, not re-translated).
        Assert.Equal("BODY-de " + snippet, email.Body);
        // The stored row carries the same display values (the inbox shows them).
        Assert.Equal("SUBJ-de", row.Subject);
        Assert.Equal("BODY-de " + snippet, row.Body);
    }

    // ── ADR 0078 — sample-account suppression gate ────────────────────────────
    //
    // When SuppressForSampleAccountsInProduction is true (the Production /
    // Staging host binding) and the recipient's profile e-mail is a code-owned
    // sample address, EmitAsync is a complete no-op: returns null, stores no
    // row, stages no email, and does not even call the ITranslationProvider.
    // When the flag is false (the Development binding, or a test harness that
    // does not register the option), a sample account is treated like any
    // other resident.

    [Fact]
    public async Task Emit_SampleAccount_Returns_Null_Stores_No_Row_Stages_No_Email_When_Flag_True()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            M6DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);

        var translator = Substitute.For<ITranslationProvider>();
        translator.GetAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.FromResult("ignored"));

        var (mailer, staged) = RecordingMailer();
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(Arg.Any<string>())
            .Returns(ci => PlantProfileReadAsync(store, (string)ci[0]));

        // The production binding: suppress sample accounts.
        var svc = new NotificationService(store, userInfo, translator, mailer,
            Options.Create(new NotificationOptions { SuppressForSampleAccountsInProduction = true }));

        const string sampleRecipient = "u-0078-anna";
        await PlantProfile(store, sampleRecipient, "anna@examplium.com", emailLanguage: "de");

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await svc.EmitAsync(session, sampleRecipient,
            NotificationKinds.PostReply, "notification:post.reply:reply-0078", "snippet", TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Null(row);
        Assert.Equal(0, await CountNotifications(store, sampleRecipient));
        Assert.Empty(staged);
    }

    [Fact]
    public async Task Emit_SampleAccount_Stores_Row_When_Flag_False()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            M6DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);

        var translator = Substitute.For<ITranslationProvider>();
        translator.GetAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(ci => Task.FromResult((string)ci[0]));

        var (mailer, staged) = RecordingMailer();
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(Arg.Any<string>())
            .Returns(ci => PlantProfileReadAsync(store, (string)ci[0]));

        // The Development binding (or absent option): flag false, sample
        // accounts behave like real residents.
        var svc = new NotificationService(store, userInfo, translator, mailer,
            Options.Create(new NotificationOptions { SuppressForSampleAccountsInProduction = false }));

        const string sampleRecipient = "u-0078-ben";
        await PlantProfile(store, sampleRecipient, "ben@examplium.com", emailLanguage: "en");

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await svc.EmitAsync(session, sampleRecipient,
            NotificationKinds.PostReply, "notification:post.reply:reply-0078b", "snippet", TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(row);
        Assert.Equal(1, await CountNotifications(store, sampleRecipient));
        var email = Assert.Single(staged);
        Assert.Equal("ben@examplium.com", email.Recipient);
    }

    // ── ADR 0084 — per-target subscription lanes ────────────────────────────
    //
    // The <see cref="NotificationService"/> gains three public lanes in ADR
    // 0084 (the per-target subscription lane): <c>GetSubscriptionsAsync</c>
    // (the settings-page list), <c>SetSubscriptionAsync</c> (the upsert —
    // the row is the record of the resident's last explicit choice and is
    // never deleted), and <c>IsSubscriptionEnabledForAsync</c> (the gate —
    // resolves a stored row verbatim or falls back to the kind's default
    // from the <c>NotificationKinds.OptInKinds</c> table: opt-IN kinds
    // default to disabled, opt-OUT kinds default to enabled). The 7-arg
    // <c>EmitAsync</c> overload consults the gate **first** when
    // <c>targetId</c> is provided — a disabled recipient short-circuits
    // with <c>null</c> (no inbox row, no email) before dedup, before the
    // profile read, before staging (the ADR 0078 sample-suppression shape).
    //
    // F13 — opt-IN (announcement) no-row default suppresses the emission.
    // F14 — opt-IN (announcement) row Enabled=true fires.
    // F15 — opt-OUT (community.post) no-row default fires.
    // F16 — opt-OUT (community.post) row Enabled=false suppresses.
    // F17 — opt-IN (page.child) no-row default suppresses.
    // F18 — opt-IN (page.child) row Enabled=true fires.
    // F19 — opt-OUT (group.post) no-row default fires (the legacy shape).
    // F20 — opt-OUT (group.post) row Enabled=false suppresses.
    // F21 — legacy 6-arg <c>EmitAsync</c> (targetId absent) skips the
    //      gate entirely — a disabled (kind, target) row does not affect
    //      a kind without a per-target scope (or a legacy call site).
    // F22 — <c>SetSubscriptionAsync</c> upserts on the (recipient, kind,
    //      target) business key: a second call with the opposite
    //      <c>Enabled</c> value flips the same row (no duplicate row,
    //      no delete).
    // F23 — <c>GetSubscriptionsAsync</c> returns the recipient's rows in
    //      (Kind, TargetId) order — the settings page's stable display
    //      order (and only that recipient's rows — another recipient's
    //      row does not leak).

    [Fact]
    public async Task ADR0084_F13_OptIn_NoRow_DefaultsDisabled_Suppresses()
    {
        var (store, svc, _, staged) = await BootAsync();
        const string recipient = "u-f13";
        await PlantProfile(store, recipient, "f13@kumunita");

        // announcement is opt-IN (in OptInKinds): no row → default disabled
        // → gate short-circuits with null before dedup / profile / staging.
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await svc.EmitAsync(session, recipient,
            NotificationKinds.Announcement,
            "notification:announcement:ann-f13:comm-f13",
            "snippet", targetId: "comm-f13",
            ct: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Null(row);
        Assert.Equal(0, await CountNotifications(store, recipient));
        Assert.Empty(staged);
    }

    [Fact]
    public async Task ADR0084_F14_OptIn_RowEnabledTrue_Fires()
    {
        var (store, svc, _, staged) = await BootAsync();
        const string recipient = "u-f14";
        await PlantProfile(store, recipient, "f14@kumunita");
        await svc.SetSubscriptionAsync(recipient, NotificationKinds.Announcement,
            "comm-f14", enabled: true, TestContext.Current.CancellationToken);

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await svc.EmitAsync(session, recipient,
            NotificationKinds.Announcement,
            "notification:announcement:ann-f14:comm-f14",
            "snippet", targetId: "comm-f14",
            ct: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(row);
        Assert.Equal(NotificationKinds.Announcement, row!.Kind);
        Assert.Equal(1, await CountNotifications(store, recipient));
        var email = Assert.Single(staged);
        Assert.Equal("f14@kumunita", email.Recipient);
    }

    [Fact]
    public async Task ADR0084_F15_OptOut_NoRow_DefaultsEnabled_Fires()
    {
        var (store, svc, _, staged) = await BootAsync();
        const string recipient = "u-f15";
        await PlantProfile(store, recipient, "f15@kumunita");

        // community.post is opt-OUT (not in OptInKinds): no row → default
        // enabled → the gate lets the emission through (the legacy shape).
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await svc.EmitAsync(session, recipient,
            NotificationKinds.CommunityPost,
            "notification:community.post:post-f15:comm-f15",
            "snippet", targetId: "comm-f15",
            ct: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(row);
        Assert.Equal(NotificationKinds.CommunityPost, row!.Kind);
        Assert.Equal(1, await CountNotifications(store, recipient));
        var email = Assert.Single(staged);
        Assert.Equal("f15@kumunita", email.Recipient);
    }

    [Fact]
    public async Task ADR0084_F16_OptOut_RowEnabledFalse_Suppresses()
    {
        var (store, svc, _, staged) = await BootAsync();
        const string recipient = "u-f16";
        await PlantProfile(store, recipient, "f16@kumunita");
        await svc.SetSubscriptionAsync(recipient, NotificationKinds.CommunityPost,
            "comm-f16", enabled: false, TestContext.Current.CancellationToken);

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await svc.EmitAsync(session, recipient,
            NotificationKinds.CommunityPost,
            "notification:community.post:post-f16:comm-f16",
            "snippet", targetId: "comm-f16",
            ct: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Null(row);
        Assert.Equal(0, await CountNotifications(store, recipient));
        Assert.Empty(staged);
    }

    [Fact]
    public async Task ADR0084_F17_PageChild_OptIn_NoRow_Suppresses()
    {
        var (store, svc, _, staged) = await BootAsync();
        const string recipient = "u-f17";
        await PlantProfile(store, recipient, "f17@kumunita");

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await svc.EmitAsync(session, recipient,
            NotificationKinds.PageChild,
            "notification:page.child:page-f17:parent-f17",
            "snippet", targetId: "parent-f17",
            ct: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Null(row);
        Assert.Equal(0, await CountNotifications(store, recipient));
        Assert.Empty(staged);
    }

    [Fact]
    public async Task ADR0084_F18_PageChild_OptIn_RowEnabledTrue_Fires()
    {
        var (store, svc, _, staged) = await BootAsync();
        const string recipient = "u-f18";
        await PlantProfile(store, recipient, "f18@kumunita");
        await svc.SetSubscriptionAsync(recipient, NotificationKinds.PageChild,
            "parent-f18", enabled: true, TestContext.Current.CancellationToken);

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await svc.EmitAsync(session, recipient,
            NotificationKinds.PageChild,
            "notification:page.child:page-f18:parent-f18",
            "snippet", targetId: "parent-f18",
            ct: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(row);
        Assert.Equal(NotificationKinds.PageChild, row!.Kind);
        Assert.Equal(1, await CountNotifications(store, recipient));
        var email = Assert.Single(staged);
        Assert.Equal("f18@kumunita", email.Recipient);
    }

    [Fact]
    public async Task ADR0084_F19_GroupPost_OptOut_NoRow_DefaultsEnabled_Fires()
    {
        var (store, svc, _, staged) = await BootAsync();
        const string recipient = "u-f19";
        await PlantProfile(store, recipient, "f19@kumunita");

        // group.post is opt-OUT (same shape as community.post): no row →
        // default enabled. This is the legacy behavior, now gated on the
        // (kind, target) pair — the row's absence still means "enabled" for
        // opt-OUT kinds, so existing residents see no behavior change.
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await svc.EmitAsync(session, recipient,
            NotificationKinds.GroupPost,
            "notification:group.post:post-f19:grp-f19",
            "snippet", targetId: "grp-f19",
            ct: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(row);
        Assert.Equal(NotificationKinds.GroupPost, row!.Kind);
        Assert.Equal(1, await CountNotifications(store, recipient));
        var email = Assert.Single(staged);
        Assert.Equal("f19@kumunita", email.Recipient);
    }

    [Fact]
    public async Task ADR0084_F20_GroupPost_OptOut_RowEnabledFalse_Suppresses()
    {
        var (store, svc, _, staged) = await BootAsync();
        const string recipient = "u-f20";
        await PlantProfile(store, recipient, "f20@kumunita");
        await svc.SetSubscriptionAsync(recipient, NotificationKinds.GroupPost,
            "grp-f20", enabled: false, TestContext.Current.CancellationToken);

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await svc.EmitAsync(session, recipient,
            NotificationKinds.GroupPost,
            "notification:group.post:post-f20:grp-f20",
            "snippet", targetId: "grp-f20",
            ct: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Null(row);
        Assert.Equal(0, await CountNotifications(store, recipient));
        Assert.Empty(staged);
    }

    [Fact]
    public async Task ADR0084_F21_Legacy_SixArgOverload_SkipsGate()
    {
        var (store, svc, _, staged) = await BootAsync();
        const string recipient = "u-f21";
        await PlantProfile(store, recipient, "f21@kumunita");

        // An explicit row for (announcement, comm-f21) saying "disabled" —
        // the legacy 6-arg EmitAsync (targetId absent) must not consult
        // it: the gate is skipped when targetId is null/empty, so the
        // emission proceeds on the kind's existing (lean-default-enabled)
        // preference path.
        await svc.SetSubscriptionAsync(recipient, NotificationKinds.Announcement,
            "comm-f21", enabled: false, TestContext.Current.CancellationToken);

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await svc.EmitAsync(session, recipient,
            NotificationKinds.Announcement,
            "notification:announcement:ann-f21-legacy",
            "snippet", TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(row);
        Assert.Equal(1, await CountNotifications(store, recipient));
        var email = Assert.Single(staged);
        Assert.Equal("f21@kumunita", email.Recipient);
    }

    [Fact]
    public async Task ADR0084_F22_SetSubscription_Upserts_On_BusinessKey_NeverDeletes()
    {
        var (store, svc, _, _) = await BootAsync();
        const string recipient = "u-f22";
        const string kind = NotificationKinds.Announcement;
        const string target = "comm-f22";

        var ct = TestContext.Current.CancellationToken;
        await svc.SetSubscriptionAsync(recipient, kind, target, enabled: true, ct);
        await svc.SetSubscriptionAsync(recipient, kind, target, enabled: false, ct);
        await svc.SetSubscriptionAsync(recipient, kind, target, enabled: true, ct);

        // Exactly one row for the (recipient, kind, target) pair — the
        // upsert flipped Enabled rather than deleting + re-inserting (the
        // row is the record of the last explicit choice, never absent).
        var rows = await svc.GetSubscriptionsAsync(recipient, ct);
        var mine = rows.Where(s => s.Kind == kind && s.TargetId == target).ToList();
        Assert.Single(mine);
        Assert.True(mine[0].Enabled);

        // A different (kind, target) for the same recipient is a separate
        // row — the business key is (recipient, kind, target), not
        // (recipient, kind).
        await svc.SetSubscriptionAsync(recipient, kind, "other-target", enabled: false, ct);
        rows = await svc.GetSubscriptionsAsync(recipient, ct);
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task ADR0084_F23_GetSubscriptions_Returns_Only_Own_Rows_In_Stable_Order()
    {
        var (store, svc, _, _) = await BootAsync();
        const string recipientA = "u-f23-a";
        const string recipientB = "u-f23-b";
        const string kind = NotificationKinds.Announcement;

        var ct = TestContext.Current.CancellationToken;
        await svc.SetSubscriptionAsync(recipientA, kind, "zeta", enabled: true, ct);
        await svc.SetSubscriptionAsync(recipientA, kind, "alpha", enabled: false, ct);
        await svc.SetSubscriptionAsync(recipientA, NotificationKinds.GroupPost, "grp-a", enabled: true, ct);
        // recipientB's row must not leak into recipientA's read.
        await svc.SetSubscriptionAsync(recipientB, kind, "alpha", enabled: true, ct);

        var rows = await svc.GetSubscriptionsAsync(recipientA, ct);
        Assert.Equal(3, rows.Count);
        // (Kind, TargetId) order — the settings page's stable display order:
        //   announcement/alpha  <  announcement/zeta  <  group.post/grp-a
        Assert.Equal(
            new[] { (NotificationKinds.Announcement, "alpha"),
                    (NotificationKinds.Announcement, "zeta"),
                    (NotificationKinds.GroupPost, "grp-a") },
            rows.Select(r => (r.Kind, r.TargetId)).ToArray());
    }

    // ── ADR 0085 — the item-link lane (the content/reply notification
    //    surface) ──────────────────────────────────────────────────────────
    //
    // When an emitter supplies a <c>LinkPath</c> (a same-origin relative path
    // like <c>/posts/{id}#reply-{id}</c>), the stored inbox row carries it
    // verbatim (the inbox renders it as its own clickable "View" link) and the
    // **email** body has it appended as an **absolute** link (the instance
    // BaseUrl + the relative path — the
    // <see cref="Kumunita.Core.Identity.VerificationOptions.BaseUrl"/>
    // precedent, the M1 verification email's one-time-link shape), prefixed by
    // the localized <c>notifications.view</c> label. A kind with no
    // <c>LinkPath</c> (the non-content lanes) appends nothing.

    [Fact]
    public async Task Emit_WithLinkPath_Stores_Relative_Link_And_Appends_Absolute_Link_To_Email()
    {
        const string baseUrl = "http://localhost:5123";
        var (store, svc, _, staged) = await BootAsync(baseUrl);

        const string author = "u-adr85-author";
        await PlantProfile(store, author, "adr85-author@kumunita", emailLanguage: "en");

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        const string linkPath = "/posts/post-adr85#reply-reply-adr85";
        var row = await svc.EmitAsync(session, author,
            NotificationKinds.PostReply,
            "notification:post.reply:reply-adr85", "New reply on your post",
            targetId: null,
            linkPath: linkPath,
            TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        // A stored row is the contract (the synthetic recipient is not a
        // sample account and no NotificationOptions is registered, so the
        // ADR 0078 suppression gate never fires).
        Assert.NotNull(row);
        // The inbox row carries the **relative** link verbatim (the inbox
        // renders it as its own same-origin clickable "View" link).
        Assert.Equal(linkPath, row!.LinkPath);

        // The email body carries the **absolute** link (BaseUrl + relative
        // path), prefixed by the localized "view" label — the
        // RecordingTranslator resolves notifications.view to the key-derived
        // marker (en floor here).
        var email = Assert.Single(staged);
        Assert.Contains("http://localhost:5123" + linkPath, email.Body);
        Assert.Contains("notifications.view-en", email.Body);
        Assert.Contains("http://localhost:5123/posts/post-adr85", email.Body);
    }

    [Fact]
    public async Task Emit_WithoutLinkPath_Appends_No_Link_To_Email()
    {
        const string baseUrl = "http://localhost:5123";
        var (store, svc, _, staged) = await BootAsync(baseUrl);

        const string author = "u-adr85b-author";
        await PlantProfile(store, author, "adr85b-author@kumunita", emailLanguage: "en");

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var row = await svc.EmitAsync(session, author,
            NotificationKinds.PostReply,
            "notification:post.reply:reply-adr85b", "New reply on your post",
            TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(row);
        // No LinkPath → no link on the row and no link appended to the email.
        Assert.Null(row!.LinkPath);
        var email = Assert.Single(staged);
        Assert.DoesNotContain("http://localhost:5123", email.Body);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Boots the scratch store with the M1 + M3 + M6 surfaces (the
    /// <see cref="EventReminderServiceTests"/> / <see cref="ProjectServiceTests"/>
    /// shape) so the <see cref="Notification"/> /
    /// <see cref="NotificationPreference"/> / <see cref="Profile"/> /
    /// <see cref="AccessAudit"/> tables exist, and builds the
    /// <see cref="NotificationService"/> over the frozen seams: a **real**
    /// <c>IUserInfoService</c> stand-in (the <see cref="IUserInfoService
    /// .GetProfileAsync"/> read lane for the recipient's
    /// <c>Profile.EmailLanguage</c> + <c>Email</c>), a
    /// <see cref="ITranslationProvider"/> stand-in (resolving the
    /// <c>notification.{kind}.subject</c> / <c>.body</c> keys in the
    /// recipient's language), and the <see cref="RecordingMailer"/>.
    /// </summary>
    private async Task<(IDocumentStore store, NotificationService svc,
        ITranslationProvider translator, List<(string Key, string Recipient, string Subject, string Body)> staged)>
        BootAsync(string? baseUrl = null)
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            M6DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);

        var translator = RecordingTranslator();
        var (mailer, staged) = RecordingMailer();
        var userInfo = Substitute.For<IUserInfoService>();
        // GetProfileAsync reads the **planted** Profile (the recipient's
        // EmailLanguage + Email) from the live store — the ADR 0061 seam.
        userInfo.GetProfileAsync(Arg.Any<string>())
            .Returns(ci => PlantProfileReadAsync(store, (string)ci[0]));

        // ADR 0085 — the item-link lane: when a BaseUrl is supplied, bind it
        // to VerificationOptions so the service prefixes each notification's
        // LinkPath (the email's absolute view link). null = no BaseUrl (the
        // relative-path fallback the M1 verification link uses).
        IOptions<Identity.VerificationOptions>? baseUrlOptions = baseUrl is null
            ? null
            : Options.Create(new Identity.VerificationOptions { BaseUrl = baseUrl });
        var svc = new NotificationService(store, userInfo, translator, mailer, null, baseUrlOptions);
        return (store, svc, translator, staged);
    }

    /// <summary>A <b>frozen</b> <see cref="IMailerStage"/> stand-in that records
    /// the staged (key, recipient, subject, body) tuples — the
    /// <see cref="EventReminderServiceTests"/> recording-mailer precedent,
    /// extended to capture subject + body for the F12 language pin.</summary>
    private static (IMailerStage mailer, List<(string Key, string Recipient, string Subject, string Body)> staged)
        RecordingMailer()
    {
        var staged = new List<(string Key, string Recipient, string Subject, string Body)>();
        var mailer = Substitute.For<IMailerStage>();
        mailer.StageAsync(
                Arg.Any<IDocumentSession>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                staged.Add(((string)callInfo[1], (string)callInfo[2], (string)callInfo[3], (string)callInfo[4]));
                return Task.CompletedTask;
            });
        return (mailer, staged);
    }

    /// <summary>A <see cref="ITranslationProvider"/> stand-in that resolves the
    /// <c>notification.{kind}.subject</c> / <c>.body</c> keys to a fixed
    /// per-language marker (so F12 can assert the recipient's language was
    /// used) and returns the key as a floor for anything else.</summary>
    private static ITranslationProvider RecordingTranslator()
    {
        var translator = Substitute.For<ITranslationProvider>();
        translator.GetAsync(
                Arg.Any<string>(), Arg.Any<string?>())
            .Returns(ci =>
            {
                var key = (string)ci[0];
                var lang = (string?)ci[1] ?? "en";
                // The post.reply templates resolve to fixed markers (so F12
                // can pin the recipient's language); anything else returns a
                // key-derived marker — the floor, not the subject of a pin.
                if (key == "notification.post.reply.subject") return Task.FromResult($"SUBJ-{lang}");
                if (key == "notification.post.reply.body") return Task.FromResult($"BODY-{lang}");
                return Task.FromResult($"{key}-{lang}");
            });
        return translator;
    }

    /// <summary>The <see cref="IUserInfoService.GetProfileAsync"/> read lane
    /// (the ADR 0061 seam) — reads the planted <see cref="Profile"/> from the
    /// live store; <c>null</c> when no profile exists for the subject.</summary>
    private static async Task<Profile?> PlantProfileReadAsync(IDocumentStore store, string subjectId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.LoadAsync<Profile>(subjectId, ct);
    }

    private static async Task PlantProfile(IDocumentStore store, string subjectId, string email,
        string? emailLanguage = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(new Profile
        {
            SubjectId = subjectId,
            DisplayName = subjectId,
            Verified = true,
            Email = email,
            EmailLanguage = emailLanguage,
        });
        await w.SaveChangesAsync(ct);
    }

    /// <summary>Emits a notification **and commits** the session — the
    /// caller's responsibility per the design-doc contract ("the caller
    /// commits the session — the service does not").</summary>
    private static async Task<Notification> Emit(
        NotificationService svc, IDocumentSession session,
        string recipientId, string kind, string key, string? body)
    {
        var row = await svc.EmitAsync(session, recipientId, kind, key, body,
            TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        // ADR 0078 — EmitAsync returns null only for a sample account in a
        // production environment; these harnesses register no
        // NotificationOptions (flag defaults false) and use synthetic
        // recipients, so a stored row is the contract here.
        return row!;
    }

    private static async Task<int> CountNotifications(IDocumentStore store, string recipientId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<Notification>()
            .Where(n => n.RecipientId == recipientId)
            .CountAsync(ct);
    }

    private static async Task<IReadOnlyList<AccessAudit>> AuditRows(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<AccessAudit>().ToListAsync(ct);
    }
}
