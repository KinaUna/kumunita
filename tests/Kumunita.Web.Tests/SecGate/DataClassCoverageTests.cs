using System.Reflection;
using Kumunita.Core.Identity;
using Kumunita.Core.Portability;
using Kumunita.Core.Privacy;
using Xunit;

namespace Kumunita.Web.Tests.SecGate;

/// <summary>
/// IS24 (IMPROVE-SEC) — the "erasure and export agree with the attributes"
/// gate (S2/E2, the plan's DoD item 2). The <see cref="DataClassAttribute"/>
/// rows (IS17–IS20, 81/81 — the spec) are the single source of truth; this
/// gate reads them via <see cref="DataClassCatalog"/> and asserts the
/// execution agrees:
/// <list type="bullet">
///   <item><see cref="EveryDeleteOrPseudonymizeType_IsHandledByAnEraser"/> —
///         every <c>Delete</c>/<c>Pseudonymize</c> row appears in
///         <b>exactly one</b> of the three eraser seams'
///         <c>HandledTypes</c> sets (<see cref="ResidentContentEraser.HandledTypes"/>,
///         <see cref="ResidentThreadPseudonymizer.HandledTypes"/>,
///         <see cref="ResidentAccountRowsEraser.HandledTypes"/>).</item>
///   <item><see cref="EveryResidentExportType_IsInUserScopeInventory"/> —
///         every <c>ExportScope.Resident</c> row is a key in
///         <see cref="UserScopeInventory.Entries"/> (the resident export).</item>
///   <item><see cref="NoHandledType_IsMarkedKeep"/> — the union of the three
///         <c>HandledTypes</c> sets is disjoint from the <c>Keep</c> rows (a
///         "kept" row is never eraser-handled).</item>
/// </list>
/// <para>
/// <b>Expected state (a drift this lane surfaces, not a bug in the gate):</b>
/// the two <c>Delete</c>/<c>Pseudonymize</c> rows <b>not</b> in any eraser
/// seam — the composer (<c>DeleteCoreAsync</c> steps 5–8b) handles
/// <c>Profile</c> / <c>AccessAudit</c> / <c>GroupMembership</c> /
/// <c>ComponentMembership</c> / <c>ModeratorAssignment</c> directly, and
/// <c>GuardianLink</c> / <c>KioskPlaylist</c> / <c>SuppressedAddress</c> /
/// <c>GroupInvitation</c> / <c>GroupJoinRequest</c> are handled nowhere —
/// fail gate 1; the <c>Resident</c>-export rows IS31/IS32 add
/// (<c>Announcement</c> / <c>AnnouncementTranslation</c> /
/// <c>AnnouncementComment</c> / <c>BookmarkFolder</c> / <c>PersonalNote</c> /
/// <c>NoteFolder</c> / <c>PageTranslation</c> / <c>GroupConversation</c> /
/// <c>GroupMessage</c>) fail gate 2. Per the SEC protocol (rules 1–9), a red
/// gate here is a <b>drift found in a previous unit</b> — pause and record it
/// in the handoff, do not edit the gate to pass, do not invent a new erasure
/// rule. The gate is the pin, not the place to fix.
/// </para>
/// <para>
/// <b>Assembly:</b> the <c>[DataClass]</c> rows live in <c>Kumunita.Core</c>;
/// <see cref="DataClassCatalog.All"/> is called with that same assembly, so
/// this gate and the IS20c <c>EveryRegisteredDocType_HasADataClass</c> gate
/// (the <c>Kumunita.Core</c> test project) agree on the 81-type set.
/// </para>
/// </summary>
public class DataClassCoverageTests
{
/// <summary>The 82 <c>[DataClass]</c> rows (the spec) in <c>Kumunita.Core</c>
/// (81 pre-R11; the ADR 0301 <c>DeletionRequest</c> row is the 82nd).</summary>
    private static IReadOnlyList<DataClassEntry> Spec()
        => DataClassCatalog.All(typeof(DataClassAttribute).Assembly);

    /// <summary>
    /// How many of the three eraser seams' <c>HandledTypes</c> sets contain
    /// <paramref name="type"/> (0 / 1 / 2 / 3). The three sets are disjoint by
    /// construction; a count of 2+ is the "exactly one" gate's double-listing
    /// failure, a count of 0 is the "unhandled" failure.
    /// </summary>
    private static int HandlerCount(Type type)
        => (ResidentContentEraser.HandledTypes.Contains(type) ? 1 : 0)
         + (ResidentThreadPseudonymizer.HandledTypes.Contains(type) ? 1 : 0)
         + (ResidentAccountRowsEraser.HandledTypes.Contains(type) ? 1 : 0);

    /// <summary>The union of the three <c>HandledTypes</c> sets.</summary>
    private static HashSet<Type> HandledUnion()
        => new HashSet<Type>(ResidentContentEraser.HandledTypes)
            .Union(ResidentThreadPseudonymizer.HandledTypes)
            .Union(ResidentAccountRowsEraser.HandledTypes)
            .ToHashSet();

    /// <summary>
    /// Every <c>Delete</c>/<c>Pseudonymize</c> row must be in exactly one of
    /// the three <c>HandledTypes</c> sets. A <c>Keep</c> row is never handled
    /// (a separate gate asserts that); a row in no set is an un-erased spec
    /// row; a row in two sets is a double-listing (one fact in one place).
    /// </summary>
    [Fact]
    public void EveryDeleteOrPseudonymizeType_IsHandledByAnEraser()
    {
        List<(string Type, int Handlers)> violations = new();
        foreach (DataClassEntry entry in Spec())
        {
            if (entry.Policy.Erasure is not (Erasure.Delete or Erasure.Pseudonymize))
                continue;
            int handlers = HandlerCount(entry.Type);
            if (handlers != 1)
                violations.Add((entry.Type.FullName ?? entry.Type.Name, handlers));
        }
        violations.Sort((a, b) => string.Compare(a.Type, b.Type, StringComparison.Ordinal));

        Assert.True(violations.Count == 0,
            "Every Erasure.Delete / Erasure.Pseudonymize [DataClass] row must be in " +
            "exactly one of ResidentContentEraser.HandledTypes, " +
            "ResidentThreadPseudonymizer.HandledTypes, ResidentAccountRowsEraser.HandledTypes " +
            "(one fact lives in one place). " +
            $"Violations (type: handlers-count; 0 = no eraser handles it, 2+ = listed in two): " +
            string.Join("; ", violations.Select(v => $"{v.Type} ({v.Handlers})")));
    }

    /// <summary>
    /// Every <c>ExportScope.Resident</c> row must be a key in
    /// <see cref="UserScopeInventory.Entries"/> (the resident export,
    /// <c>UserPortabilityService</c>). A <c>Resident</c>-export row absent from
    /// the inventory is an incomplete GDPR Art. 15/20 export (the E6 gap
    /// IS31/IS32 close).
    /// </summary>
    [Fact]
    public void EveryResidentExportType_IsInUserScopeInventory()
    {
        List<string> missing = Spec()
            .Where(e => e.Policy.Export == ExportScope.Resident)
            .Where(e => !UserScopeInventory.Entries.ContainsKey(e.Type.Name))
            .Select(e => e.Type.FullName ?? e.Type.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "Every ExportScope.Resident [DataClass] row must be a key in " +
            "UserScopeInventory.Entries (the resident export). Missing: " +
            string.Join(", ", missing));
    }

    /// <summary>
    /// The union of the three <c>HandledTypes</c> sets must be disjoint from
    /// the <c>Erasure.Keep</c> rows — a "kept" row is a decision to let it
    /// survive, so an eraser touching it is a contradiction.
    /// </summary>
    [Fact]
    public void NoHandledType_IsMarkedKeep()
    {
        HashSet<Type> keepTypes = Spec()
            .Where(e => e.Policy.Erasure == Erasure.Keep)
            .Select(e => e.Type)
            .ToHashSet();

        HashSet<Type> handled = HandledUnion();
        List<string> contradiction = handled
            .Where(keepTypes.Contains)
            .Select(t => t.FullName ?? t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(contradiction.Count == 0,
            "An Erasure.Keep [DataClass] row must not be in any eraser seam's " +
            "HandledTypes (a kept row survives; an eraser touching it is a " +
            "contradiction). Keep rows listed by an eraser: " +
            string.Join(", ", contradiction));
    }
}
