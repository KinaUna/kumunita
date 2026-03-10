IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres("postgres")
    .WithPgAdmin(); // Adds pgAdmin UI in local dev

IResourceBuilder<PostgresDatabaseResource> postgresDb = postgres.AddDatabase("kumunitadb");

// Host serves both the API and the Blazor WASM client
IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.Kumunita_Host>("kumunitahost")
    .WithHttpHealthCheck("/health")
    .WithReference(postgresDb)
    .WaitFor(postgresDb);
    

builder.Build().Run();
