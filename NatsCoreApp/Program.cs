using NATS.Client.Core;
using System.Text;

Console.WriteLine("=== NATS Core Console App ===");
Console.WriteLine("Connecting to NATS at nats://localhost:4222 ...");
Console.WriteLine();

var opts = new NatsOpts { Url = "nats://localhost:4222" };

while (true)
{
    Console.WriteLine("Choose a demo:");
    Console.WriteLine("  1. Publish / Subscribe");
    Console.WriteLine("  2. Request / Reply");
    Console.WriteLine("  3. Queue Group (load balancing)");
    Console.WriteLine("  4. Wildcard Subscriptions");
    Console.WriteLine("  0. Exit");
    Console.Write("> ");

    var choice = Console.ReadLine()?.Trim();
    Console.WriteLine();

    switch (choice)
    {
        case "1": await RunPubSubDemo(opts); break;
        case "2": await RunRequestReplyDemo(opts); break;
        case "3": await RunQueueGroupDemo(opts); break;
        case "4": await RunWildcardDemo(opts); break;
        case "0": return;
        default: Console.WriteLine("Unknown option.\n"); break;
    }
}

// ─── Demo 1: Pub / Sub ──────────────────────────────────────────────────────

static async Task RunPubSubDemo(NatsOpts opts)
{
    Console.WriteLine("--- Pub/Sub Demo ---");
    Console.WriteLine("Subject: demo.messages");
    Console.WriteLine("The subscriber will print each message. Type messages to publish (blank = stop).");
    Console.WriteLine();

    await using var nats = new NatsConnection(opts);
    await nats.ConnectAsync();

    // Start subscriber on a background task
    using var cts = new CancellationTokenSource();
    var subTask = Task.Run(async () =>
    {
        await foreach (var msg in nats.SubscribeAsync<string>("demo.messages", cancellationToken: cts.Token))
        {
            Console.WriteLine($"  [SUB] Received: \"{msg.Data}\" (subject: {msg.Subject})");
        }
    });

    // Publish loop
    while (true)
    {
        Console.Write("Publish (blank to stop): ");
        var text = Console.ReadLine();
        if (string.IsNullOrEmpty(text)) break;
        await nats.PublishAsync("demo.messages", text);
    }

    await cts.CancelAsync();
    try { await subTask; } catch (OperationCanceledException) { }

    Console.WriteLine("Pub/Sub demo done.\n");
}

// ─── Demo 2: Request / Reply ────────────────────────────────────────────────

static async Task RunRequestReplyDemo(NatsOpts opts)
{
    Console.WriteLine("--- Request / Reply Demo ---");
    Console.WriteLine("A replier listens on 'demo.request' and echoes back an uppercase response.");
    Console.WriteLine("Type requests (blank = stop).");
    Console.WriteLine();

    await using var nats = new NatsConnection(opts);
    await nats.ConnectAsync();

    using var cts = new CancellationTokenSource();

    // Replier
    var replierTask = Task.Run(async () =>
    {
        await foreach (var msg in nats.SubscribeAsync<string>("demo.request", cancellationToken: cts.Token))
        {
            var response = msg.Data?.ToUpperInvariant() ?? string.Empty;
            Console.WriteLine($"  [REPLIER] Got \"{msg.Data}\" → replying \"{response}\"");
            await msg.ReplyAsync(response);
        }
    });

    // Requester loop
    while (true)
    {
        Console.Write("Request (blank to stop): ");
        var text = Console.ReadLine();
        if (string.IsNullOrEmpty(text)) break;

        var reply = await nats.RequestAsync<string, string>("demo.request", text);
        Console.WriteLine($"  [REQUESTER] Response: \"{reply.Data}\"");
    }

    await cts.CancelAsync();
    try { await replierTask; } catch (OperationCanceledException) { }

    Console.WriteLine("Request/Reply demo done.\n");
}

// ─── Demo 3: Queue Groups ───────────────────────────────────────────────────

static async Task RunQueueGroupDemo(NatsOpts opts)
{
    Console.WriteLine("--- Queue Group Demo ---");
    Console.WriteLine("Three consumers share queue group 'workers' on 'demo.work'.");
    Console.WriteLine("Each message is delivered to exactly ONE consumer.");
    Console.WriteLine("Publishing 10 messages automatically...");
    Console.WriteLine();

    await using var nats = new NatsConnection(opts);
    await nats.ConnectAsync();

    using var cts = new CancellationTokenSource();

    // Start 3 queue-group subscribers
    var workers = Enumerable.Range(1, 3).Select(id => Task.Run(async () =>
    {
        await foreach (var msg in nats.SubscribeAsync<string>("demo.work", queueGroup: "workers", cancellationToken: cts.Token))
        {
            Console.WriteLine($"  [WORKER-{id}] Handled: \"{msg.Data}\"");
            await Task.Delay(50); // simulate work
        }
    })).ToArray();

    // Small delay so subscribers are ready
    await Task.Delay(200);

    // Publish 10 messages
    for (int i = 1; i <= 10; i++)
        await nats.PublishAsync("demo.work", $"task-{i}");

    // Wait for messages to be processed
    await Task.Delay(500);
    await cts.CancelAsync();

    try { await Task.WhenAll(workers); } catch (OperationCanceledException) { }

    Console.WriteLine("Queue Group demo done.\n");
}

// ─── Demo 4: Wildcard Subscriptions ────────────────────────────────────────

static async Task RunWildcardDemo(NatsOpts opts)
{
    Console.WriteLine("--- Wildcard Subscription Demo ---");
    Console.WriteLine("Subscriptions:");
    Console.WriteLine("  'sensor.*'   matches one token  (e.g. sensor.temp)");
    Console.WriteLine("  'sensor.>'   matches all tokens (e.g. sensor.room1.humidity)");
    Console.WriteLine("Publishing sample messages...");
    Console.WriteLine();

    await using var nats = new NatsConnection(opts);
    await nats.ConnectAsync();

    using var cts = new CancellationTokenSource();

    var singleTask = Task.Run(async () =>
    {
        await foreach (var msg in nats.SubscribeAsync<string>("sensor.*", cancellationToken: cts.Token))
            Console.WriteLine($"  [sensor.*] subject={msg.Subject,-30} data={msg.Data}");
    });

    var multiTask = Task.Run(async () =>
    {
        await foreach (var msg in nats.SubscribeAsync<string>("sensor.>", cancellationToken: cts.Token))
            Console.WriteLine($"  [sensor.>] subject={msg.Subject,-30} data={msg.Data}");
    });

    await Task.Delay(200); // let subscribers register

    string[] subjects =
    [
        "sensor.temp",
        "sensor.humidity",
        "sensor.room1.temp",
        "sensor.room1.humidity",
        "sensor.building.floor2.temp",
        "other.topic",
    ];

    foreach (var subject in subjects)
    {
        Console.WriteLine($"  Publishing to '{subject}'");
        await nats.PublishAsync(subject, $"value from {subject}");
        await Task.Delay(50);
    }

    await Task.Delay(300);
    await cts.CancelAsync();

    try { await Task.WhenAll(singleTask, multiTask); } catch (OperationCanceledException) { }

    Console.WriteLine("Wildcard demo done.\n");
}
