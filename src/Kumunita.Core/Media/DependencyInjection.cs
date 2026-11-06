using Kumunita.Core.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Media</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The byte-I/O
/// seams (IMediaFileStore, IMediaStore) + the antivirus seam (IVirusScanner)
/// + their per-instance options (MediaOptions, VirusScanOptions). Core stays
/// HTTP-free (ADR 0006-D): LocalVolumeFileStore is a BCL file-store behind
/// IMediaFileStore; ClamAvScanner drives a local daemon over a loopback socket.
/// </summary>
public static class MediaModule
{
    /// <summary>Registers the <c>Media</c> bounded context's seams.</summary>
    public static IServiceCollection AddMedia(this IServiceCollection services)
    {
        services.AddOptions<MediaOptions>();
        services.AddTransient<IMediaFileStore, LocalVolumeFileStore>();

        services.AddTransient<IMediaStore, LocalVolumeMediaStore>();

        services.AddOptions<VirusScanOptions>();
        services.AddTransient<IVirusScanner, ClamAvScanner>();

        return services;
    }
}
