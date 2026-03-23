using NATS.Client.Core;

Console.WriteLine("=== NATS Subscriber ===");
Console.WriteLine("Connecting to NATS at nats://localhost:4222 ...");

var opts = new NatsOpts { Url = "nats://localhost:4222" };
await using var nats = new NatsConnection(opts);
await nats.ConnectAsync();

Console.WriteLine("Connected!\n");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\nShutting down...");
};

while (true)
{
    Console.WriteLine("Choose a demo:");
    Console.WriteLine("  1. Subscribe to messages (Pub/Sub)");
    Console.WriteLine("  2. Reply to requests (Request/Reply)");
    Console.WriteLine("  3. Queue group consumer (load balancing)");
    Console.WriteLine("  4. Wildcard subscriptions");
    Console.WriteLine("  0. Exit");
    Console.Write("> ");

    var choice = Console.ReadLine()?.Trim();
    Console.WriteLine();

    switch (choice)
    {
        case "1": await SubscribeMessages(nats, cts.Token); break;
        case "2": await ReplyToRequests(nats, cts.Token); break;
        case "3": await QueueGroupConsumer(nats, cts.Token); break;
        case "4": await WildcardSubscriptions(nats, cts.Token); break;
        case "0": return;
        default: Console.WriteLine("Unknown option.\n"); break;
    }
}

// ─── 1. Subscribe to messages ───────────────────────────────────────────────

static async Task SubscribeMessages(NatsConnection nats, CancellationToken ct)
{
    Console.WriteLine("--- Subscribe Messages (Pub/Sub) ---");
    Console.WriteLine("Subject: demo.messages");
    Console.WriteLine("Waiting for messages... (press Ctrl+C to stop)\n");

    try
    {
        await foreach (var msg in nats.SubscribeAsync<string>("demo.messages", cancellationToken: ct))
        {
            Console.WriteLine($"  [SUB] Received: \"{msg.Data}\" (subject: {msg.Subject})");
        }
    }
    catch (OperationCanceledException) { }

    Console.WriteLine("Subscription stopped.\n");
}

// ─── 2. Reply to requests ───────────────────────────────────────────────────

static async Task ReplyToRequests(NatsConnection nats, CancellationToken ct)
{
    Console.WriteLine("--- Reply to Requests (Request/Reply) ---");
    Console.WriteLine("Subject: demo.request");
    Console.WriteLine("Listening for requests and replying with UPPERCASE... (press Ctrl+C to stop)\n");

    try
    {
        await foreach (var msg in nats.SubscribeAsync<string>("demo.request", cancellationToken: ct))
        {
            var response = msg.Data?.ToUpperInvariant() ?? string.Empty;
            Console.WriteLine($"  [REPLIER] Got \"{msg.Data}\" -> replying \"{response}\"");
            await msg.ReplyAsync(response);
        }
    }
    catch (OperationCanceledException) { }

    Console.WriteLine("Replier stopped.\n");
}

// ─── 3. Queue group consumer ────────────────────────────────────────────────

static async Task QueueGroupConsumer(NatsConnection nats, CancellationToken ct)
{
    Console.Write("Enter worker ID (e.g. 1, 2, 3): ");
    var idInput = Console.ReadLine()?.Trim();
    var workerId = string.IsNullOrEmpty(idInput) ? "1" : idInput;

    Console.WriteLine($"\n--- Queue Group Consumer (Worker-{workerId}) ---");
    Console.WriteLine("Subject: demo.work | Queue group: workers");
    Console.WriteLine("Waiting for tasks... (press Ctrl+C to stop)\n");

    try
    {
        await foreach (var msg in nats.SubscribeAsync<string>("demo.work", queueGroup: "workers", cancellationToken: ct))
        {
            Console.WriteLine($"  [WORKER-{workerId}] Handled: \"{msg.Data}\"");
            await Task.Delay(50, ct); // simulate work
        }
    }
    catch (OperationCanceledException) { }

    Console.WriteLine($"Worker-{workerId} stopped.\n");
}

// ─── 4. Wildcard subscriptions ──────────────────────────────────────────────

static async Task WildcardSubscriptions(NatsConnection nats, CancellationToken ct)
{
    Console.WriteLine("--- Wildcard Subscriptions ---");
    Console.WriteLine("  'sensor.*'  -> matches one token  (e.g. sensor.temp)");
    Console.WriteLine("  'sensor.>'  -> matches all tokens (e.g. sensor.room1.humidity)");
    Console.WriteLine("Listening... (press Ctrl+C to stop)\n");

    var singleTask = Task.Run(async () =>
    {
        await foreach (var msg in nats.SubscribeAsync<string>("sensor.*", cancellationToken: ct))
            Console.WriteLine($"  [sensor.*] subject={msg.Subject,-30} data={msg.Data}");
    }, ct);

    var multiTask = Task.Run(async () =>
    {
        await foreach (var msg in nats.SubscribeAsync<string>("sensor.>", cancellationToken: ct))
            Console.WriteLine($"  [sensor.>] subject={msg.Subject,-30} data={msg.Data}");
    }, ct);

    try
    {
        await Task.WhenAll(singleTask, multiTask);
    }
    catch (OperationCanceledException) { }

    Console.WriteLine("Wildcard subscriptions stopped.\n");
}
