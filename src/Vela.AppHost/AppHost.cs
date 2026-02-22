var builder = DistributedApplication.CreateBuilder(args);

var valkeyPassword = builder.AddParameter("valkeyPassword", "password");
var valkey = builder.AddValkey("Valkey", password: valkeyPassword)
  .WithEndpoint(port: 6379, targetPort: 6379, name: "valkey");

var pgPassword = builder.AddParameter("pgPassword", "password");
var postgres = builder.AddPostgres("postgres", password: pgPassword)
  .WithDataVolume("vela-pg-data")
  .WithHostPort(5200);

var velaDb = postgres.AddDatabase("VelaDb");

builder.AddProject<Projects.Vela>("Vela")
  .WithReference(valkey)
  .WithReference(velaDb)
  .WaitFor(postgres);

builder.Build().Run();
