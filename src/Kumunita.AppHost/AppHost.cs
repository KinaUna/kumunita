var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin(); // Adds pgAdmin UI in local dev

var postgresDb = postgres.AddDatabase("kumunitadb");

var apiService = builder.AddProject<Projects.Kumunita_Host>("api")
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Kumunita_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
