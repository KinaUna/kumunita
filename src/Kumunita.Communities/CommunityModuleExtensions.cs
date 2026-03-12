using Kumunita.Communities.Domain;
using Kumunita.Shared.Kernel.Communities;
using Marten;

namespace Kumunita.Communities;

public static class CommunityModuleExtensions
{
    /// <summary>
    /// Registers the Communities module with Marten.
    /// Call this from the host's AddMarten() configuration block.
    /// </summary>
    public static StoreOptions AddCommunitiesModule(this StoreOptions opts)
    {
        opts.Schema.For<Community>()
            .DatabaseSchemaName("communities")
            .UniqueIndex(c => c.Slug);

        opts.Schema.For<CommunityMembership>()
            .DatabaseSchemaName("communities")
            .Index(m => m.UserId)
            .Index(m => m.CommunityId);

        opts.Schema.For<CommunityInvitation>()
            .DatabaseSchemaName("communities")
            .Index(i => i.Token)
            .Index(i => i.InvitedEmail);

        return opts;
    }
}