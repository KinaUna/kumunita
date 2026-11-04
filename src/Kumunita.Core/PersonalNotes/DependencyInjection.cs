using Kumunita.Core.Targets;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>PersonalNotes</c> bounded context's DI
/// registration, extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The personal-notes
/// service seam (M37, ADR 0167). Core stays HTTP-free (ADR 0006-D).
/// </summary>
public static class PersonalNotesModule
{
    /// <summary>Registers the <c>PersonalNotes</c> bounded context's seam.</summary>
    public static IServiceCollection AddPersonalNotes(this IServiceCollection services)
    {
        services.AddTransient<PersonalNotes.IPersonalNoteService>(sp => new PersonalNotes.PersonalNoteService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<IContentTargetResolver>()));

        return services;
    }
}
