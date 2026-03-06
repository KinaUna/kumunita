IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres("postgres")
    .WithPgAdmin(); // Adds pgAdmin UI in local dev

IResourceBuilder<PostgresDatabaseResource> postgresDb = postgres.AddDatabase("kumunitadb");

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.Kumunita_Host>("kumunitahost")
    .WithHttpHealthCheck("/health")
    .WithReference(postgresDb)
    .WaitFor(postgresDb);
    

builder.AddProject<Projects.Kumunita_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
