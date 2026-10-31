using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// SEC M1 — the pinned tests for the global anti-forgery enforcement
/// (design doc §2.3, U12; invariant S-4 "every state-changing request
/// validates an anti-forgery token unless explicitly token-authenticated").
/// <para>
/// U12 registered <c>AutoValidateAntiforgeryTokenAttribute</c> as a global
/// MVC filter (Program.cs, <c>AddControllersWithViews</c> options) so every
/// <c>POST/PUT/DELETE/PATCH</c> controller action validates a token unless it
/// carries <c>[IgnoreAntiforgeryToken]</c>. The closed allow-list of actions
/// exempt from anti-forgery is <b>exactly two</b> — the M2M / provider ingest
/// endpoints whose credential is a token, not a cookie session:
/// <list type="bullet">
/// <item><see cref="EscalationInboundController.Inbound"/> —
/// <c>POST /escalations/inbound</c>, credential <c>Authorization: Bearer &lt;token&gt;</c>.</item>
/// <item><see cref="MailSuppressionController.Suppress"/> —
/// <c>POST /api/mail/suppress</c>, credential <c>X-Suppression-Token</c> header.</item>
/// </list>
/// </para>
/// <para>
/// Pure reflection over the shipped assembly — no app boot, no Postgres.
/// If a new unsafe action lands without a token mechanism, or a third action
/// claims <c>[IgnoreAntiforgeryToken]</c>, these tests fail (the drift-guard
/// the §2.3 row pins).
/// </para>
/// </summary>
public sealed class AntiforgeryCoverageTests
{
    /// <summary>
    /// Every non-abstract controller action that handles an unsafe verb
    /// (POST/PUT/DELETE/PATCH) must be covered: either it carries
    /// <c>[ValidateAntiForgeryToken]</c> itself, or it is covered by the
    /// global <c>AutoValidateAntiforgeryTokenAttribute</c> filter — i.e. it
    /// does <em>not</em> carry <c>[IgnoreAntiforgeryToken]</c> — unless it is
    /// one of the exactly-two token-authenticated allow-listed actions.
    /// </summary>
    [Fact]
    public void EveryUnsafeAction_IsCoveredByAntiforgery()
    {
        var unsafeActions = UnsafeActions().ToList();

        // Sanity: the scan is meaningful (the seven M1 actions and the two
        // token-auth endpoints must all be among them).
        Assert.Contains(unsafeActions, a => a.Controller == typeof(CommunityController) && a.Action.Name == "SetMandatory");
        Assert.Contains(unsafeActions, a => a.Controller == typeof(EscalationInboundController) && a.Action.Name == "Inbound");

        var uncovered = unsafeActions
            .Where(a => !HasAttribute<ValidateAntiForgeryTokenAttribute>(a.Action)
                && HasAttribute<IgnoreAntiforgeryTokenAttribute>(a.Action)
                && !AllowedExemptions.Contains(a))
            .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
            .ToList();

        Assert.Empty(uncovered);
    }

    /// <summary>
    /// The closed allow-list of <c>[IgnoreAntiforgeryToken]</c> actions is
    /// <b>exactly two</b>: the Bearer-token escalation inbound and the
    /// X-Suppression-Token mail-suppress ingest. A third exemption — or a
    /// missing one — is a coverage hole.
    /// </summary>
    [Fact]
    public void OnlyTwoActionsIgnoreAntiforgery()
    {
        var exempt = UnsafeActions()
            .Where(a => HasAttribute<IgnoreAntiforgeryTokenAttribute>(a.Action))
            .ToList();

        Assert.Equal(2, exempt.Count);
        Assert.Contains(exempt, a => a.Controller == typeof(EscalationInboundController) && a.Action.Name == "Inbound");
        Assert.Contains(exempt, a => a.Controller == typeof(MailSuppressionController) && a.Action.Name == "Suppress");
    }

    // ── scan helpers ────────────────────────────────────────────────────

    private static bool HasAttribute<T>(MemberInfo m) where T : Attribute
        => m.GetCustomAttribute<T>() is not null;

    private static IEnumerable<ActionRef> UnsafeActions()
    {
        var unsafeVerbAttributes = new[]
        {
            typeof(HttpPostAttribute),
            typeof(HttpPutAttribute),
            typeof(HttpDeleteAttribute),
            typeof(HttpPatchAttribute),
        };

        // The Kumunita.Web assembly (CommunityController's assembly — Program
        // is internal to the web app and not reachable from this test project).
        foreach (var type in typeof(CommunityController).Assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract || !typeof(Controller).IsAssignableFrom(type))
                continue;

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!unsafeVerbAttributes.Any(t => method.GetCustomAttribute(t) is not null))
                    continue;

                yield return new ActionRef(type, method);
            }
        }
    }

    private sealed record ActionRef(Type Controller, MethodInfo Action)
    {
        public override string ToString() => $"{Controller.Name}.{Action.Name}";
    }

    /// <summary>
    /// The closed allow-list (design doc §2.2 U12; U11's table is the ground
    /// truth for the seven M1 actions — none of which is exempted): exactly
    /// the two token-authenticated M2M/provider ingest endpoints.
    /// </summary>
    private static readonly IReadOnlyList<ActionRef> AllowedExemptions = new List<ActionRef>
    {
        new(typeof(EscalationInboundController), typeof(EscalationInboundController).GetMethod(nameof(EscalationInboundController.Inbound))!),
        new(typeof(MailSuppressionController), typeof(MailSuppressionController).GetMethod(nameof(MailSuppressionController.Suppress))!),
    };
}
