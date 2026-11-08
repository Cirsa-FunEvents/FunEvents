var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var database = postgres.AddDatabase("funevents");

var api = builder.AddProject<Projects.FunEvents_Api>("funevents-api")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.FunEvents_Worker>("funevents-worker")
    .WithReference(database)
    .WaitFor(api);

builder.Build().Run();
