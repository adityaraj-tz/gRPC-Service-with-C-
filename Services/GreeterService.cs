using Grpc.Core;
using GrpcServiceProject;
using GrpcServiceProject.Models;
using Marten;

namespace GrpcServiceProject.Services;

public class GreeterService : Greeter.GreeterBase
{
    private readonly ILogger<GreeterService> _logger;
    private readonly IDocumentSession _session;

    public GreeterService(ILogger<GreeterService> logger, IDocumentSession session)
    {
        _logger = logger;
        _session = session;
    }

    public override async Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        var message = $"Hello {request.Name}";
        var greeting = new Greeting
        {
            Name = request.Name,
            Message = message
        };
            _session.Store(greeting);
        await _session.SaveChangesAsync();

        _logger.LogInformation("Stored greeting for {Name} with Id {Id}", request.Name, greeting.Id);

        return new HelloReply { Message = message };
    }
}
