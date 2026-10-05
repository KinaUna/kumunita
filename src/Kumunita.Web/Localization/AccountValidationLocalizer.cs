using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Threading.Tasks;
using Kumunita.Core.Localization;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Kumunita.Web.Localization;

/// <summary>
/// Localizes the <b>four Account forms'</b> DataAnnotations validation
/// messages (P1-5 of the translation audit — Login, Signup,
/// ResendVerification, ChangePassword).
///
/// <para>
/// <b>Why a helper (and not the default ASP.NET localizer).</b> The platform's
/// closed translation surface is the <see cref="KnownTranslationKeys"/>
/// registry (ADR 0015) — every server-resolved UI string resolves through
/// <see cref="ITranslationProvider"/> against it. The four <c>account.err.*</c>
/// keys live there (en/de/fr/da). The default .NET path (an
/// <see cref="IStringLocalizerFactory"/> reading resource files) would be a
/// second, divergent translation surface this repo deliberately avoids — so we
/// wire the <i>existing</i> registry seam to the model-state the
/// <c>asp-validation-for</c> / <c>asp-validation-summary</c> helpers already
/// render, instead of adding a resource-file localizer.
/// </para>
///
/// <para>
/// <b>How it works.</b> The AccountController calls <see cref="ApplyAsync"/>
/// at the top of each POST lane (before the <c>!ModelState.IsValid</c>
/// early-return), passing the bound POCO. The helper reflects over the POCO's
/// properties, reads each property's validation attributes, and for every
/// <see cref="ModelStateEntry"/> that failed, rewrites the
/// <see cref="ModelError.ErrorMessage"/> from the responsible attribute's
/// <c>account.err.*</c> key (resolved in the request's effective language)
/// with the field's <c>[Display(Name)]</c> substituted for <c>{0}</c> (and
/// the <c>[MinLength]</c> value for <c>{1}</c>). The mapping is by
/// <b>attribute type</b> (deterministic — <c>[Required]</c>, <c>
/// [EmailAddress]</c>, <c>[MinLength]</c>, <c>[Compare]</c>), not by message
/// text, so it is locale-independent. Any attribute with no key (the Account
/// forms ship none) leaves the platform default — a no-op.
/// </para>
/// </summary>
public static class AccountValidationLocalizer
{
    /// <summary>
    /// Rewrites the <see cref="ModelError.ErrorMessage"/> of every failed
    /// <see cref="ModelStateEntry"/> in <paramref name="modelState"/> from the
    /// bound POCO's <paramref name="model"/> validation attributes, resolved in
    /// the request's effective language (the <see cref="EffectiveLanguageCode"/>
    /// chain: cookie preference → <c>Accept-Language</c> → instance default →
    /// <c>en</c> floor). A <c>null</c> provider (a non-HTTP render, a unit
    /// test) or an unmapped attribute leaves the platform default in place —
    /// a no-op, never an error.
    /// </summary>
    public static async Task ApplyAsync(
        object model,
        ModelStateDictionary? modelState,
        HttpRequest? request,
        ILocalizationService localization,
        ITranslationProvider provider)
    {
        if (model is null || modelState is null || modelState.Count == 0 ||
            provider is null || localization is null)
        {
            return;
        }

        string effectiveLanguage = await EffectiveLanguageCode.ResolveAsync(
            request, localization, provider);

        // The model's validation surface: property name → the (key, {1} value)
        // map for each failure the property's attributes can produce, plus the
        // display name (the <c>{0}</c> slot) and the compare-target name (the
        // <c>[Compare]</c> <c>{1}</c> slot).
        var type = model.GetType();
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var display = prop.GetCustomAttribute<DisplayAttribute>();
            names[prop.Name] = string.IsNullOrWhiteSpace(display?.Name) ? prop.Name : display!.Name;
        }

        foreach (var (key, entry) in modelState)
        {
            if (entry?.Errors is not { Count: > 0 })
            {
                continue;
            }

            if (!names.TryGetValue(key, out var fieldName))
            {
                continue;
            }

            // <see cref="ModelErrorCollection"/> is read-only (no reassignment)
            // but exposes <see cref="ModelErrorCollection.InsertErrorAtIndex"/>
            // (internal) — the public surface is to clear + re-add. Clear the
            // failed messages and re-add them (localized where the POCO's
            // attribute maps to an <c>account.err.*</c> key, unchanged where
            // not), preserving order.
            var originals = entry.Errors.ToList();
            entry.Errors.Clear();
            foreach (var e in originals)
            {
                entry.Errors.Add(Rewrite(e, fieldName, key, names, type, effectiveLanguage, provider));
            }
        }
    }

    /// <summary>
    /// Rewrites a single <see cref="ModelError"/> if its attribute (read from
    /// the POCO property) has a registered <c>account.err.*</c> key —
    /// otherwise returns it unchanged.
    /// </summary>
    private static ModelError Rewrite(
        ModelError error,
        string fieldName,
        string propertyName,
        System.Collections.Generic.IReadOnlyDictionary<string, string> names,
        Type modelType,
        string effectiveLanguage,
        ITranslationProvider provider)
    {
        var prop = modelType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop is null)
        {
            return error;
        }

        // The responsible attribute is the one whose platform-default message
        // matches the error's (the binder attaches one ModelError per failed
        // attribute, in attribute order). Walk the property's attributes and
        // find the one that produced this message.
        (string? key, object? slotOne)? mapped = null;
        foreach (Attribute attr in prop.GetCustomAttributes())
        {
            if (attr is not ValidationAttribute va)
            {
                continue;
            }

            string? platformDefault = va.FormatErrorMessage(fieldName);
            if (platformDefault == error.ErrorMessage)
            {
                mapped = MapAttribute(va, names);
                if (mapped is not null)
                {
                    break;
                }
            }
        }

        if (mapped is not { } map || map.key is null)
        {
            return error;
        }

        string? raw;
        try
        {
            raw = provider.GetAsync(map.key, effectiveLanguage).GetAwaiter().GetResult();
        }
        catch
        {
            return error;
        }

        if (string.IsNullOrWhiteSpace(raw) || raw == map.key)
        {
            return error;
        }

        string message = raw
            .Replace("{0}", fieldName)
            .Replace("{1}", map.slotOne?.ToString() ?? string.Empty);
        return new ModelError(message);
    }

    /// <summary>
    /// Maps a <see cref="ValidationAttribute"/> to its <c>account.err.*</c>
    /// key + the <c>{1}</c> slot value (the <c>[MinLength]</c> minimum, or the
    /// <c>[Compare]</c> target name for the <c>{1}</c> slot). A type with no
    /// key (the Account forms ship none) returns <c>null</c> — the caller
    /// leaves the platform default.
    /// </summary>
    private static (string? key, object? slotOne)? MapAttribute(
        ValidationAttribute va,
        System.Collections.Generic.IReadOnlyDictionary<string, string> names)
    {
        return va switch
        {
            RequiredAttribute => ("account.err.required", null),
            EmailAddressAttribute => ("account.err.email", null),
            MinLengthAttribute min => ("account.err.password_min", min.Length),
            CompareAttribute cmp =>
                ("account.err.password_mismatch",
                 names.TryGetValue(cmp.OtherProperty, out var other) ? other : cmp.OtherProperty),
            _ => null,
        };
    }
}
