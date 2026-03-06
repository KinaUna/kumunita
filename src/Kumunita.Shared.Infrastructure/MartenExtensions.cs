using Marten;

namespace Kumunita.Shared.Infrastructure
{
    public static class MartenExtensions
    {
        public static StoreOptions ConfigureModuleSchemas(this StoreOptions opts)
        {
            // Each module will add its own configuration here as it's built
            // For example, the Announcements module will add:
            // opts.Schema.For<Announcement>().DatabaseSchemaName("announcements");

            // Wolverine schema is handled by IntegrateWithWolverine above
            // Identity schema is handled by EF Core separately

            return opts;
        }
    }
}
