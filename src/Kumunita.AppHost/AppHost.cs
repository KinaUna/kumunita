IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres("postgres")
    .WithPgAdmin(); // Adds pgAdmin UI in local dev

IResourceBuilder<PostgresDatabaseResource> postgresDb = postgres.AddDatabase("kumunitadb");

// MailDev — fake SMTP server with web UI for local development
IResourceBuilder<ContainerResource> maildev = builder.AddContainer("maildev", "maildev/maildev")
    .WithHttpEndpoint(port: 1080, targetPort: 1080, name: "ui")
    .WithEndpoint(port: 1025, targetPort: 1025, name: "smtp");

// Host serves both the API and the Blazor WASM client
IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.Kumunita_Host>("kumunitahost")
    .WithHttpHealthCheck("/health")
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WithEnvironment("Smtp__Host", "localhost")
    .WithEnvironment("Smtp__Port", "1025");

builder.Build().Run();
