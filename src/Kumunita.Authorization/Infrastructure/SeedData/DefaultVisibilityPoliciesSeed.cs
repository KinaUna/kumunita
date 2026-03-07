using Kumunita.Authorization.Domain;
using Marten;
using Marten.Schema;

namespace Kumunita.Authorization.Infrastructure.SeedData;

public class DefaultVisibilityPoliciesSeed : IInitialData
{
    // Mirrors the defaults applied at registration in IdentityEventHandlers
    private static readonly (string ResourceTypeName, VisibilityLevel DefaultVisibility)[] Defaults =
    [
        (ResourceType.ProfileBio.Name,              VisibilityLevel.Members),
        (ResourceType.ProfilePhoneNumber.Name,      VisibilityLevel.Private),
        (ResourceType.ProfileAlternativeEmail.Name, VisibilityLevel.Private),
        (ResourceType.ProfileAddress.Name,          VisibilityLevel.Private),
        (ResourceType.DirectoryEntryLocation.Name,  VisibilityLevel.Members),
        (ResourceType.GroupMembership.Name,         VisibilityLevel.Members),
    ];

    public async Task Populate(IDocumentStore store, CancellationToken ct)
    {
        await using var session = store.LightweightSession();

        var allUsers = await session
            .Query<UserAuthorizationState>()
            .ToListAsync(ct);

        if (allUsers.Count == 0) return;

        foreach (var user in allUsers)
        {
            var existingResourceNames = (await session
                .Query<VisibilityPolicy>()
                .Where(p => p.OwnerId == user.Id)
                .Select(p => p.ResourceTypeName)
                .ToListAsync(ct))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var (resourceTypeName, defaultVisibility) in Defaults)
            {
                if (existingResourceNames.Contains(resourceTypeName))
                    continue;

                session.Store(VisibilityPolicy.CreateDefault(
                    user.Id, resourceTypeName, defaultVisibility));
            }
        }

        await session.SaveChangesAsync(ct);
    }
}