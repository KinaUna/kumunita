using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// SEC M3 — the pinned tests for the anonymous-write rate-limit + size-cap
/// enforcement (design doc §2.2/§2.3, U15; invariant S-5 "every anonymous
/// write has a rate limit and a size cap").
/// <para>
/// The four anonymous state-storing POSTs that shipped without a limiter
/// (re-verified in U2 §2.1 — none carried <c>[EnableRateLimiting]</c> or
/// <c>[RequestSizeLimit]</c>) each gained, in U15:
/// <list type="bullet">
/// <item><see cref="HomeController.ErrorReport"/> — <c>POST /Home/Error/Report</c>,
/// policy <c>"report"</c> (Program.cs <c>AddWindow</c> "report", 10/1 h per IP).</item>
/// <item><see cref="IssueController.PostNew"/> — <c>POST /issues/new</c>,
/// policy <c>"write"</c> (Program.cs <c>AddWindow</c> "write", 30/15 min per IP).</item>
/// <item><see cref="EscalationInboundController.Inbound"/> — <c>POST /escalations/inbound</c>,
/// policy <c>"report"</c> (token-auth but still an anonymous write).</item>
/// <item><see cref="MailSuppressionController.Suppress"/> — <c>POST /api/mail/suppress</c>,
/// policy <c>"report"</c> (token-auth but still an anonymous write).</item>
/// </list>
/// plus a <c>[RequestSizeLimit(512 MB)]</c> on each (the U14 idiom, reused here
/// — above any legitimate body for these text/JSON endpoints).
/// </para>
/// <para>
/// The <see cref="ErrorReportFormModel.Description"/> free-text gained
/// <c>[MaxLength(4000)]</c> (it had only <c>[Required]</c> — the M3 "no length
/// cap" finding). The <see cref="IssueFormModel.Description"/> already carried
/// the stricter <c>[StringLength(2000)]</c> — already bounded, left untouched
/// (recorded in the U15 handoff note).
/// </para>
/// <para>
/// Pure reflection + data-annotation validation over the shipped types — no
/// app boot, no Postgres. If a new anonymous write lands unthrottled, or a
/// bound model's free-text loses its cap, these tests fail (the drift-guard
/// the §2.3 row pins).
/// </para>
/// </summary>
public sealed class AnonymousWriteLimitsTests
{
    /// <summary>
    /// Every U15-listed anonymous state-storing action carries both
    /// <c>[EnableRateLimiting(policy)]</c> (the pinned policy name) and
    /// <c>[RequestSizeLimit]</c> (a size cap &gt; 0). A missing limiter — or a
    /// policy-name drift — is a coverage hole.
    /// </summary>
    [Fact]
    public void AnonymousWrites_HaveRateLimitPolicy()
    {
        // action → pinned policy (design doc §2.2, U15)
        var expected = new (Type Controller, string Action, string Policy)[]
        {
            (typeof(HomeController), "ErrorReport", "report"),
            (typeof(IssueController), "PostNew", "write"),
            (typeof(EscalationInboundController), "Inbound", "report"),
            (typeof(MailSuppressionController), "Suppress", "report"),
        };

        foreach (var (controller, actionName, policy) in expected)
        {
            var action = controller
                .GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"U15 drift: {controller.Name}.{actionName} not found (renamed?); re-pin the §2.1 register.");

            var limiter = action.GetCustomAttribute<EnableRateLimitingAttribute>();
            Assert.True(limiter is not null,
                $"{controller.Name}.{actionName} lacks [EnableRateLimiting] (S-5).");
            Assert.True(policy == limiter!.PolicyName,
                $"{controller.Name}.{actionName} carries policy \"{limiter.PolicyName}\", expected \"{policy}\".");

            // The presence of [RequestSizeLimit] is the S-5 drift-guard; the
            // byte value is a literal at each call site (512 MB per U14's idiom)
            // and is not the surface a future change would silently drop.
            var sizeLimit = action.GetCustomAttribute<RequestSizeLimitAttribute>();
            Assert.True(sizeLimit is not null,
                $"{controller.Name}.{actionName} lacks [RequestSizeLimit] (S-5).");
        }
    }

    /// <summary>
    /// The 500-page report free-text is capped: a 4,001-char description is
    /// invalid (<c>[MaxLength(4000)]</c> surfaces in <see cref="ModelState"/>),
    /// so the write lane is never called with an unbounded body.
    /// </summary>
    [Fact]
    public void ErrorReportDescription_Over4000_IsInvalid()
        => AssertDescriptionInvalid(new string('a', 4001));

    /// <summary>
    /// The 4,000-char boundary is the valid side of the cap: exactly 4,000
    /// characters passes model validation (the cap is exclusive, not
    /// inclusive of the 4,001st char).
    /// </summary>
    [Fact]
    public void ErrorReportDescription_At4000_IsValid()
        => AssertDescriptionValid(new string('a', 4000));

    // ── helpers ─────────────────────────────────────────────────────────

    private static void AssertDescriptionInvalid(string description)
    {
        var model = new ErrorReportFormModel { Description = description };
        var errors = Validate(model).Where(e => e.MemberNames.Contains("Description")).ToList();
        Assert.NotEmpty(errors);
        // The cap (not [Required]/blank) is the rejection we're pinning.
        Assert.Contains(errors, e => e.ErrorMessage != null
            && e.ErrorMessage!.Contains("4000"));
    }

    private static void AssertDescriptionValid(string description)
    {
        var model = new ErrorReportFormModel { Description = description };
        var errors = Validate(model).Where(e => e.MemberNames.Contains("Description")).ToList();
        Assert.Empty(errors);
    }

    private static IEnumerable<ValidationResult> Validate(object model)
    {
        var context = new ValidationContext(model, serviceProvider: null, items: null);
        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        if (!valid)
        {
            return results;
        }
        return Array.Empty<ValidationResult>();
    }
}
