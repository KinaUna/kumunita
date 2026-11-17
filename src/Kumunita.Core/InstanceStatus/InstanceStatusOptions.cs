namespace Kumunita.Core.InstanceStatus;

/// <summary>
/// The two host-posture facts that Core itself cannot read: <see cref="IsProduction"/>
/// (the <c>IWebHostEnvironment.EnvironmentName</c> — Core is a plain class library with no
/// ASP.NET Core framework reference, so it cannot inject <c>IWebHostEnvironment</c>
/// directly) and <see cref="DataProtectionKeysPersisted"/> (whether the host set
/// <c>DataProtection__KeysDirectory</c> to a persistent directory — the
/// <c>Program.cs</c> <see cref="Microsoft.AspNetCore.DataProtection"/> branch; Core
/// cannot observe that wiring from inside).
/// <para>
/// The other warning inputs are read straight from their existing
/// <c>IOptions&lt;T&gt;</c> bindings in Core (the <see cref="Kumunita.Core.Identity.SeedAdminOptions"/>
/// / <see cref="Kumunita.Core.Identity.SampleDataOptions"/> / <see cref="Kumunita.Core.Identity.HealthOptions"/>
/// precedent — all three already have a Core-side POCO with a <c>SectionName</c>
/// constant the host binds). This POCO is the one extra the host must bind, next to the
/// existing <c>builder.Services.Configure&lt;SampleDataOptions&gt;</c> call in
/// <c>Kumunita.Web/Program.cs</c>.
/// </para>
/// <para>
/// Read-only: the values are consumed by
/// <see cref="ConfigWarningsPolicy.Evaluate"/> to decide two of the four
/// <see cref="ConfigWarning"/> rules (the <c>sampledata</c> and
/// <c>dataprotection</c> rows) and are never echoed back in the
/// <see cref="InstanceStatus"/> POCO (the "no secret values" invariant — the
/// <see cref="DataProtectionKeysPersisted"/> input is a boolean posture, not the
/// directory path).
/// </para>
/// </summary>
public sealed class InstanceStatusOptions
{
    /// <summary>The <c>SectionName</c> the host binds this POCO from (<c>InstanceStatus:*</c>).</summary>
    public const string SectionName = "InstanceStatus";

    /// <summary>
    /// <c>true</c> when the host is running with
    /// <c>IWebHostEnvironment.EnvironmentName == "Production"</c> (the
    /// <c>Program.cs</c> <c>builder.Environment.IsProduction()</c> read at startup,
    /// injected into this POCO — the Core class library cannot inject
    /// <c>IWebHostEnvironment</c> itself, ADR 0006-D). Drives the
    /// <c>sampledata</c> warning rule (the <c>SampleData__Enabled</c> flag in a
    /// real deployment).
    /// </summary>
    public bool IsProduction { get; set; }

    /// <summary>
    /// <c>true</c> when the host set <c>DataProtection__KeysDirectory</c> to a
    /// persistent directory (the <c>Program.cs</c>
    /// <see cref="Microsoft.AspNetCore.DataProtection"/> branch's
    /// <c>!string.IsNullOrWhiteSpace(dataProtectionKeysDir)</c> read, injected here).
    /// <c>false</c> = in-memory keyring (the default, dev / one-shot containers) —
    /// the <c>dataprotection</c> warning rule fires. Carries a boolean posture,
    /// never the directory path (the "no secret values" invariant).
    /// </summary>
    public bool DataProtectionKeysPersisted { get; set; }
}
