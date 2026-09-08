namespace Kumunita.Core.Identity;

/// <summary>
/// The outcome of <see cref="IIdentityService.ResendVerificationEmailAsync"/>.
/// <see cref="Success"/> = true when a fresh verification email was staged. A
/// <c>false</c> result carries a user-presentable <see cref="Reason"/> (the account
/// is unknown, already verified — sign in, or the attempt bound from
/// <see cref="VerificationOptions.MaxVerifyAttempts"/> is exhausted — the admin
/// manual-verify valve is the path forward). Never throws for these states.
/// </summary>
public sealed record ResendVerificationResult(bool Success, string? Reason = null);
