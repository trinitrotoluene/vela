var builder = DistributedApplication.CreateBuilder(args);

var valkeyPassword = builder.AddParameter("valkeyPassword", "password");
var valkey = builder.AddValkey("Valkey", port: 6379, password: valkeyPassword);

builder.AddProject<Projects.Vela>("Vela")
  .WithReference(valkey);

builder.Build().Run();
