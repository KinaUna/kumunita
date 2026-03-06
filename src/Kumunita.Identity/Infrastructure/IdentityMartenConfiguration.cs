using Kumunita.Identity.Domain;
using Marten;

namespace Kumunita.Identity.Infrastructure;

public static class IdentityMartenConfiguration
{
    public static StoreOptions AddIdentityMartenSchema(this StoreOptions opts)
    {
        opts.Schema.For<UserProfile>()
            .DatabaseSchemaName("identity")
            .Identity(x => x.Id)
            .Index(x => x.PreferredLanguage);

        opts.Schema.For<DirectoryEntry>()
            .DatabaseSchemaName("identity")
            .Identity(x => x.Id)
            .Index(x => x.IsVisible)
            .Index(x => x.BlockOrArea);

        opts.Schema.For<UserGroup>()
            .DatabaseSchemaName("identity")
            .Index(x => x.OwnerId)
            .Index(x => x.ParentGroupId);

        opts.Schema.For<UserGroupMembership>()
            .DatabaseSchemaName("identity")
            .Index(x => x.UserId)
            .Index(x => x.GroupId);

        // Soft delete for UserProfile — deleted users' data is retained
        // for compliance but hidden from queries
        opts.Schema.For<UserProfile>()
            .SoftDeleted();

        return opts;
    }
}