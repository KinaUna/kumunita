namespace Kumunita.Core.UserInfo;

/// <summary>
/// The directory read seam (IMPROVE-CODE-2 C2-03 — the
/// <see cref="DirectoryService"/>'s three cross-module read methods, extracted
/// onto a narrow interface so the crossing modules (<c>Mentions</c>,
/// <c>Web</c> controllers, the <c>MentionRenderer</c>) code against a stable
/// seam rather than the <c>DirectoryService</c> concrete class — the
/// C03 <see cref="Kumunita.Core.Notifications.INotificationEmitter"/>
/// pattern, the ADR 0006-D "route + authz + shape" rule).
/// <para>
/// The three methods are the cross-module surface the callers actually use
/// (the part-vs-whole rule — only the true surface, no ceremony):
/// <see cref="ListAsync"/> (the directory list),
/// <see cref="DetailAsync"/> (a single profile's directory read),
/// <see cref="PreviewAsAsync"/> (the U5 preview-as composition read).
/// </para>
/// <para>
/// The <see cref="DirectoryService"/> implementation is unchanged (the
/// standing decision, the contact-block resolution, the audit row all stay in
/// the concrete). This interface adds no new behaviour — it names the
/// surface the crossing modules already call.
/// </para>
/// </summary>
public interface IDirectoryService
{
    /// <summary>
    /// The directory list read (the <c>/directory</c> page's
    /// <see cref="Kumunita.Web.Controllers.DirectoryController"/>'s
    /// <c>Index</c> action's read): the visible profiles in the viewer's
    /// directory order. The <see cref="DirectoryService"/> implementation is
    /// unchanged (the CanSeeAsync aggregate audit row + the
    /// <c>ShowContactBlock</c> resolution stay in the concrete).
    /// </summary>
    Task<DirectoryList> ListAsync(string viewerSubjectId);

    /// <summary>
    /// A single profile's directory read (the <see
    /// cref="Kumunita.Web.Controllers.DirectoryController"/>'s
    /// <c>Detail</c> action's read + the <see
    /// cref="Kumunita.Web.Controllers.ProfileController"/>'s preview
    /// composition). The <see cref="DirectoryService"/> implementation is
    /// unchanged (the <c>ShowContactBlock</c> flag + the
    /// <c>Profile</c> shape stay in the concrete).
    /// </summary>
    Task<DirectoryDetail> DetailAsync(string viewerSubjectId, string targetSubjectId);

    /// <summary>
    /// The U5 preview-as composition read (the <see
    /// cref="Kumunita.Web.Controllers.ProfileController"/>'s preview decision
    /// — a <b>composition</b> read, not a gate: the decision "may I preview
    /// this profile as the viewer" is the ADR 0006-D shape, the read returns
    /// the <c>PreviewRow</c> the Web maps onto its view-model). The
    /// <see cref="DirectoryService"/> implementation is unchanged.
    /// </summary>
    Task<PreviewRow> PreviewAsAsync(string authorSubjectId, string asSubjectId);
}
