var builder = DistributedApplication.CreateBuilder(args);

var valkey = builder.AddValkey("Valkey", port: 6379);

builder.AddProject<Projects.Vela>("Vela")
  .WithReference(valkey);

builder.Build().Run();
