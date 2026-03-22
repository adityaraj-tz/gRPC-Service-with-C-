using GrpcServiceProject.Services;
using JasperFx;
using Marten;
using Weasel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection(); 

builder.Services.AddMarten(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? "Host=localhost;Database=test_gRPC;Username=postgres;Password=Intel007@";

    options.Connection(connectionString);
    options.AutoCreateSchemaObjects = AutoCreate.All;
});

var app = builder.Build();

app.MapGrpcService<GreeterService>();
app.MapGrpcService<BookServiceImpl>();
app.MapGrpcReflectionService();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

app.Run();