using NATS.Client.Core;

Console.WriteLine("=== NATS Publisher ===");
Console.WriteLine("Connecting to NATS at nats://localhost:4222 ...");

var opts = new NatsOpts { Url = "nats://localhost:4222" };
await using var nats = new NatsConnection(opts);
await nats.ConnectAsync();

Console.WriteLine("Connected!\n");

while (true)
{
    Console.WriteLine("Choose a demo:");
    Console.WriteLine("  1. Publish messages (Pub/Sub)");
    Console.WriteLine("  2. Send requests (Request/Reply)");
    Console.WriteLine("  3. Publish to queue group workers");
    Console.WriteLine("  4. Publish to wildcard subjects");
    Console.WriteLine("  0. Exit");
    Console.Write("> ");

    var choice = Console.ReadLine()?.Trim();
    Console.WriteLine();

    switch (choice)
    {
        case "1": await PublishMessages(nats); break;
        case "2": await SendRequests(nats); break;
        case "3": await PublishToQueueGroup(nats); break;
        case "4": await PublishWildcard(nats); break;
        case "0": return;
        default: Console.WriteLine("Unknown option.\n"); break;
    }
}

// ─── 1. Publish messages ────────────────────────────────────────────────────

static async Task PublishMessages(NatsConnection nats)
{
    Console.WriteLine("--- Publish Messages (Pub/Sub) ---");
    Console.WriteLine("Subject: demo.messages");
    Console.WriteLine("Type messages to publish (blank line to stop).\n");

    while (true)
    {
        Console.Write("Message: ");
        var text = Console.ReadLine();
        if (string.IsNullOrEmpty(text)) break;

        await nats.PublishAsync("demo.messages", text);
        Console.WriteLine($"  [PUB] Published: \"{text}\"\n");
    }

    Console.WriteLine("Done.\n");
}

// ─── 2. Request / Reply ─────────────────────────────────────────────────────

static async Task SendRequests(NatsConnection nats)
{
    Console.WriteLine("--- Send Requests (Request/Reply) ---");
    Console.WriteLine("Subject: demo.request");
    Console.WriteLine("Make sure the Subscriber app is running with the Reply handler.");
    Console.WriteLine("Type messages to send as requests (blank line to stop).\n");

    while (true)
    {
        Console.Write("Request: ");
        var text = Console.ReadLine();
        if (string.IsNullOrEmpty(text)) break;

        try
        {
            var reply = await nats.RequestAsync<string, string>(
                "demo.request", text, cancellationToken: new CancellationTokenSource(5000).Token);
            Console.WriteLine($"  [REQ] Response: \"{reply.Data}\"\n");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("  [REQ] Timed out — is the Subscriber running?\n");
        }
    }

    Console.WriteLine("Done.\n");
}

// ─── 3. Queue Group ─────────────────────────────────────────────────────────

static async Task PublishToQueueGroup(NatsConnection nats)
{
    Console.WriteLine("--- Publish to Queue Group Workers ---");
    Console.WriteLine("Subject: demo.work");
    Console.WriteLine("Make sure the Subscriber app is running with Queue Group consumers.");
    Console.Write("How many messages to publish? (default 10): ");

    var input = Console.ReadLine()?.Trim();
    int count = int.TryParse(input, out var n) && n > 0 ? n : 10;

    Console.WriteLine();

    for (int i = 1; i <= count; i++)
    {
        await nats.PublishAsync("demo.work", $"task-{i}");
        Console.WriteLine($"  [PUB] Published: \"task-{i}\"");
    }

    Console.WriteLine($"\nPublished {count} messages to 'demo.work'.\n");
}

// ─── 4. Wildcard Subjects ───────────────────────────────────────────────────

static async Task PublishWildcard(NatsConnection nats)
{
    Console.WriteLine("--- Publish to Wildcard Subjects ---");
    Console.WriteLine("Make sure the Subscriber app is running with wildcard subscriptions.\n");

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
        await nats.PublishAsync(subject, $"value from {subject}");
        Console.WriteLine($"  [PUB] Published to '{subject}'");
    }

    Console.WriteLine("\nAll wildcard messages published.\n");
}
