using Kumunita.Authorization.Domain;
using Marten;

namespace Kumunita.Authorization.Infrastructure;

public static class AuthorizationMartenConfiguration
{
    public static StoreOptions AddAuthorizationMartenSchema(this StoreOptions opts)
    {
        opts.Schema.For<UserAuthorizationState>()
            .DatabaseSchemaName("authz")
            .Identity(x => x.Id);

        opts.Schema.For<VisibilityPolicy>()
            .DatabaseSchemaName("authz")
            .Index(x => x.OwnerId)
            .Index(x => x.ResourceTypeName);

        opts.Schema.For<CapabilityToken>()
            .DatabaseSchemaName("authz")
            .Index(x => x.RequesterId)
            .Index(x => x.OwnerId)
            .Index(x => x.Status)
            .Index(x => x.ExpiresAt);

        opts.Schema.For<AuditEntry>()
            .DatabaseSchemaName("authz")
            .Index(x => x.OwnerId)
            .Index(x => x.RequesterId)
            .Index(x => x.OccurredAt);

        return opts;
    }
}