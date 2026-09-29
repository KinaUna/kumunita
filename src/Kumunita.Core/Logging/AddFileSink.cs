using Microsoft.Extensions.Logging;

namespace Kumunita.Core.Logging;

/// <summary>
/// The D6 wiring extension (ADR 0114). Registers the
/// <see cref="FileLoggerProvider"/> as a provider on the
/// <paramref name="b"/> (the <see cref="ILoggingBuilder"/> the host's
/// <c>AddLogging()</c> call produces, the <c>Program.cs</c> lines 39–50
/// shape), and runs the one <see cref="RollingFileSink.Retain"/> boot pass
/// (the "delete at boot" convention, the D6 housekeeping). The sink is
/// <b>additive</b>: the console sink stays (the <c>docker logs</c> surface
/// is unchanged); the file sink is a second <see cref="ILoggerProvider"/>
/// on the same <see cref="ILoggerFactory"/>.
/// </summary>
public static class AddFileSinkExtensions
{
    /// <summary>
    /// The D6 wiring extension (ADR 0114). Registers the
    /// <see cref="FileLoggerProvider"/> as a provider on the
    /// <paramref name="b"/>, and runs the one
    /// <see cref="RollingFileSink.Retain"/> boot pass (the "delete at
    /// boot" convention, the D6 housekeeping).
    /// </summary>
    public static ILoggingBuilder AddFileSink(this ILoggingBuilder b, string dir, int retentionDays)
    {
        ArgumentNullException.ThrowIfNull(b);
        RollingFileSink.Retain(dir, retentionDays, DateTimeOffset.UtcNow);
        return b.AddProvider(new FileLoggerProvider(dir, retentionDays));
    }
}
