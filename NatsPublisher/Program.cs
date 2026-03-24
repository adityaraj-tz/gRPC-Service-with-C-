using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

Console.WriteLine("=== NATS JetStream Publisher ===");
Console.WriteLine("Connecting to NATS at nats://localhost:4222 ...");

var opts = new NatsOpts { Url = "nats://localhost:4222" };
await using var nats = new NatsConnection(opts);
await nats.ConnectAsync();

var js = new NatsJSContext(nats);

Console.WriteLine("Connected!\n");

await EnsureStream(js);

while (true)
{
    Console.WriteLine("Choose a demo:");
    Console.WriteLine("  1. Publish messages (with ack)");
    Console.WriteLine("  2. Publish with headers");
    Console.WriteLine("  3. Publish batch of messages");
    Console.WriteLine("  4. View stream info");
    Console.WriteLine("  0. Exit");
    Console.Write("> ");

    var choice = Console.ReadLine()?.Trim();
    Console.WriteLine();

    switch (choice)
    {
        case "1": await PublishMessages(js); break;
        case "2": await PublishWithHeaders(js); break;
        case "3": await PublishBatch(js); break;
        case "4": await ViewStreamInfo(js); break;
        case "0": return;
        default: Console.WriteLine("Unknown option.\n"); break;
    }
}

// ─── Stream setup ──────────────────────────────────────────────────────────────

static async Task EnsureStream(NatsJSContext js)
{
    var config = new StreamConfig("ORDERS", ["orders.>"])
    {
        Storage = StreamConfigStorage.File,         // persist to disk (default)
        MaxMsgs = 10_000,                           // max 10k messages retained
        MaxAge = TimeSpan.FromHours(24),            // auto-delete after 24 hours
        Retention = StreamConfigRetention.Limits,   // delete oldest when limits hit
    };

    try
    {
        await js.CreateStreamAsync(config);
        Console.WriteLine("[STREAM] Created stream 'ORDERS' (subjects: orders.>)");
    }
    catch (NatsJSApiException)
    {
        await js.UpdateStreamAsync(config);
        Console.WriteLine("[STREAM] Stream 'ORDERS' already exists (config updated)");
    }

    Console.WriteLine();
}

// ─── 1. Publish messages with ack ──────────────────────────────────────────────

static async Task PublishMessages(NatsJSContext js)
{
    Console.WriteLine("--- Publish Messages (JetStream with Ack) ---");
    Console.WriteLine("Subject: orders.new");
    Console.WriteLine("Type messages to publish (blank line to stop).\n");

    while (true)
    {
        Console.Write("Order: ");
        var text = Console.ReadLine();
        if (string.IsNullOrEmpty(text)) break;

        var ack = await js.PublishAsync("orders.new", text);
        ack.EnsureSuccess();
        Console.WriteLine($"  [PUB] Acked! Stream: {ack.Stream}, Seq: {ack.Seq}\n");
    }

    Console.WriteLine("Done.\n");
}

// ─── 2. Publish with headers ───────────────────────────────────────────────────

static async Task PublishWithHeaders(NatsJSContext js)
{
    Console.WriteLine("--- Publish with Headers ---");
    Console.WriteLine("Subject: orders.processed");
    Console.WriteLine("Publishing 5 messages with priority headers...\n");

    for (int i = 1; i <= 5; i++)
    {
        var headers = new NatsHeaders
        {
            { "Priority", i <= 2 ? "high" : "normal" },
            { "Source", "publisher-demo" },
        };

        var ack = await js.PublishAsync("orders.processed", $"order-{i}", headers: headers);
        ack.EnsureSuccess();
        Console.WriteLine($"  [PUB] Seq: {ack.Seq} | Priority: {(i <= 2 ? "high" : "normal")} | Data: order-{i}");
    }

    Console.WriteLine("\nAll messages published with headers.\n");
}

// ─── 3. Publish batch ──────────────────────────────────────────────────────────

static async Task PublishBatch(NatsJSContext js)
{
    Console.Write("How many messages? (default 10): ");
    var input = Console.ReadLine()?.Trim();
    int count = int.TryParse(input, out var n) && n > 0 ? n : 10;

    Console.WriteLine($"\n--- Publishing {count} messages to orders.batch ---\n");

    for (int i = 1; i <= count; i++)
    {
        var ack = await js.PublishAsync("orders.batch", $"batch-item-{i}");
        ack.EnsureSuccess();
        Console.WriteLine($"  [PUB] Seq: {ack.Seq} | batch-item-{i}");
    }

    Console.WriteLine($"\nPublished {count} messages.\n");
}

// ─── 4. View stream info ──────────────────────────────────────────────────────

static async Task ViewStreamInfo(NatsJSContext js)
{
    Console.WriteLine("--- Stream Info ---\n");

    try
    {
        var stream = await js.GetStreamAsync("ORDERS");
        var info = stream.Info;

        Console.WriteLine($"  Stream Name   : {info.Config.Name}");
        Console.WriteLine($"  Subjects      : {string.Join(", ", info.Config.Subjects ?? [])}");
        Console.WriteLine($"  Storage       : {info.Config.Storage}");
        Console.WriteLine($"  Messages      : {info.State.Messages}");
        Console.WriteLine($"  Bytes         : {info.State.Bytes}");
        Console.WriteLine($"  First Seq     : {info.State.FirstSeq}");
        Console.WriteLine($"  Last Seq      : {info.State.LastSeq}");
        Console.WriteLine($"  Consumer Cnt  : {info.State.ConsumerCount}");
        Console.WriteLine($"  Retention     : {info.Config.Retention}");
        Console.WriteLine($"  Max Age       : {info.Config.MaxAge}");
        Console.WriteLine($"  Max Msgs      : {info.Config.MaxMsgs}");
    }
    catch (NatsJSApiException e)
    {
        Console.WriteLine($"  Error: {e.Error.Description}");
        Console.WriteLine("  (Run the publisher first to create the stream.)");
    }

    Console.WriteLine();
}
