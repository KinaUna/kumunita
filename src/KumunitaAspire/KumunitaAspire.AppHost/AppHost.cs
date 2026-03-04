var builder = DistributedApplication.CreateBuilder(args);

// Configure the Docker Compose environment.
builder.AddDockerComposeEnvironment("env");

var apiService = builder.AddProject<Projects.KumunitaAspire_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.KumunitaAspire_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
