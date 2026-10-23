namespace Kumunita.Web.Models;

/// <summary>
/// The session management surface view model (F5, AUTH-04 — the kill-all +
/// invalidation lane; F5.U3 surface). The pin's §3 shape: the
/// <c>GET /settings/security/sessions</c> list shows "this session" +
/// "other active sessions (revoked if a kill-all has run)" —
/// <b>server-side state</b>, <b>not</b> a per-browser cookie read (a server
/// cannot enumerate another browser's cookies). The <c>POST …/kill-all</c>
/// action calls the Core seam
/// (<see cref="Kumunita.Core.Identity.IIdentityService.KillAllSessionsAsync"/>)
/// + signs out the local browser (the <c>AccountController.Logout</c> shape)
/// + redirects to the login page. The model is intentionally minimal: the
/// list is a static, server-side-state description (the pin's minimum
/// viable), not a per-browser enumeration.
/// </summary>
public sealed class SessionSettingsViewModel
{
}
