namespace Kumunita.Core.Portability;

/// <summary>
/// The import mode (ADR 0193).
/// <list type="bullet">
/// <item><see cref="CleanSlate"/> (default) — the import assumes a clean/fresh
/// instance. If any principals from the archive already exist in the target
/// identity store, the import is <b>refused</b> with a per-account failure
/// code (<c>principal.exists:{email}</c>) — a clean, renderable refusal, not
/// an unhandled 500. The operator should either remove the conflicting
/// accounts first, or choose <see cref="KeepExisting"/>.</item>
/// <item><see cref="KeepExisting"/> — merge mode. Existing principals and
/// content documents are <b>kept intact</b>; only new (non-existent)
/// principals and content documents from the archive are added. Existing
/// content is never overwritten. The <c>LocaleSettings</c> singleton and
/// <c>LanguageCatalog</c> rows are also skipped if they already exist.</item>
/// </list>
/// </summary>
public enum PortabilityImportMode
{
    /// <summary>The default — the import assumes a clean/fresh instance.</summary>
    CleanSlate,

    /// <summary>Merge mode — existing content is kept intact; only new content is added.</summary>
    KeepExisting,
}
