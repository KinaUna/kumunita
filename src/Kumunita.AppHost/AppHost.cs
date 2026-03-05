IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres("postgres")
    .WithPgAdmin(); // Adds pgAdmin UI in local dev

IResourceBuilder<PostgresDatabaseResource> postgresDb = postgres.AddDatabase("kumunitadb");

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.Kumunita_Host>("api")
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Kumunita_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
