using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

Console.WriteLine("=== NATS JetStream Subscriber ===");
Console.WriteLine("Connecting to NATS at nats://localhost:4222 ...");

var opts = new NatsOpts { Url = "nats://localhost:4222" };
await using var nats = new NatsConnection(opts);
await nats.ConnectAsync();

var js = new NatsJSContext(nats);

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
    Console.WriteLine("  1. Durable consumer (survives restarts)");
    Console.WriteLine("  2. Ephemeral consumer (temporary, replays all)");
    Console.WriteLine("  3. Fetch messages in batch (pull N at a time)");
    Console.WriteLine("  4. View stream & consumer info");
    Console.WriteLine("  0. Exit");
    Console.Write("> ");

    var choice = Console.ReadLine()?.Trim();
    Console.WriteLine();

    switch (choice)
    {
        case "1": await DurableConsumer(js, cts.Token); break;
        case "2": await EphemeralConsumer(js, cts.Token); break;
        case "3": await FetchMessages(js, cts.Token); break;
        case "4": await ViewInfo(js); break;
        case "0": return;
        default: Console.WriteLine("Unknown option.\n"); break;
    }
}

// ─── 1. Durable consumer ──────────────────────────────────────────────────────

static async Task DurableConsumer(NatsJSContext js, CancellationToken ct)
{
    Console.WriteLine("--- Durable Consumer ---");
    Console.WriteLine("Consumer: order-processor (durable)");
    Console.WriteLine("This consumer remembers its position across restarts.");
    Console.WriteLine("Listening on: orders.> (press Ctrl+C to stop)\n");

    var consumer = await js.CreateOrUpdateConsumerAsync("ORDERS", new ConsumerConfig("order-processor")
    {
        DurableName = "order-processor",
        DeliverPolicy = ConsumerConfigDeliverPolicy.All,
        AckPolicy = ConsumerConfigAckPolicy.Explicit,
        FilterSubject = "orders.>",
        AckWait = TimeSpan.FromSeconds(30),
        MaxDeliver = 3,
    });

    Console.WriteLine("[CONSUMER] Durable consumer 'order-processor' ready.\n");

    try
    {
        await foreach (var msg in consumer.ConsumeAsync<string>(cancellationToken: ct))
        {
            Console.WriteLine($"  [RECV] Subject: {msg.Subject,-25} | Data: {msg.Data}");

            if (msg.Headers?.Count > 0)
            {
                foreach (var h in msg.Headers)
                    Console.WriteLine($"         Header: {h.Key} = {string.Join(", ", h.Value.ToArray())}");
            }

            await msg.AckAsync(cancellationToken: ct);
            Console.WriteLine("         -> Acked\n");
        }
    }
    catch (OperationCanceledException) { }

    Console.WriteLine("Durable consumer stopped. Position is saved — restart to continue where you left off.\n");
}

// ─── 2. Ephemeral consumer ────────────────────────────────────────────────────

static async Task EphemeralConsumer(NatsJSContext js, CancellationToken ct)
{
    Console.WriteLine("--- Ephemeral Consumer ---");
    Console.WriteLine("This consumer is temporary and does NOT survive restarts.");
    Console.WriteLine("It replays ALL messages from the beginning of the stream.");
    Console.WriteLine("Listening on: orders.> (press Ctrl+C to stop)\n");

    var consumer = await js.CreateOrUpdateConsumerAsync("ORDERS", new ConsumerConfig
    {
        DeliverPolicy = ConsumerConfigDeliverPolicy.All,
        AckPolicy = ConsumerConfigAckPolicy.Explicit,
        FilterSubject = "orders.>",
        InactiveThreshold = TimeSpan.FromSeconds(30),
    });

    Console.WriteLine("[CONSUMER] Ephemeral consumer created (auto-deletes after 30s inactivity).\n");

    int count = 0;
    try
    {
        await foreach (var msg in consumer.ConsumeAsync<string>(cancellationToken: ct))
        {
            count++;
            Console.WriteLine($"  [RECV #{count}] Subject: {msg.Subject,-25} | Data: {msg.Data}");
            await msg.AckAsync(cancellationToken: ct);
        }
    }
    catch (OperationCanceledException) { }

    Console.WriteLine($"Ephemeral consumer stopped. Received {count} messages total.\n");
}

// ─── 3. Fetch messages in batch ───────────────────────────────────────────────

static async Task FetchMessages(NatsJSContext js, CancellationToken ct)
{
    Console.Write("How many messages to fetch? (default 5): ");
    var input = Console.ReadLine()?.Trim();
    int maxMsgs = int.TryParse(input, out var n) && n > 0 ? n : 5;

    Console.WriteLine($"\n--- Fetch {maxMsgs} Messages ---");
    Console.WriteLine("Using durable consumer 'batch-fetcher'.\n");

    var consumer = await js.CreateOrUpdateConsumerAsync("ORDERS", new ConsumerConfig("batch-fetcher")
    {
        DurableName = "batch-fetcher",
        DeliverPolicy = ConsumerConfigDeliverPolicy.All,
        AckPolicy = ConsumerConfigAckPolicy.Explicit,
        FilterSubject = "orders.>",
    });

    int count = 0;
    try
    {
        await foreach (var msg in consumer.FetchAsync<string>(new NatsJSFetchOpts { MaxMsgs = maxMsgs }, cancellationToken: ct))
        {
            count++;
            Console.WriteLine($"  [FETCH #{count}] Subject: {msg.Subject,-25} | Data: {msg.Data}");
            await msg.AckAsync(cancellationToken: ct);
        }
    }
    catch (OperationCanceledException) { }

    Console.WriteLine($"\nFetched {count} messages (requested {maxMsgs}).\n");
}

// ─── 4. View stream & consumer info ───────────────────────────────────────────

static async Task ViewInfo(NatsJSContext js)
{
    Console.WriteLine("--- Stream & Consumer Info ---\n");

    try
    {
        var stream = await js.GetStreamAsync("ORDERS");
        var info = stream.Info;

        Console.WriteLine("  STREAM: ORDERS");
        Console.WriteLine($"    Subjects      : {string.Join(", ", info.Config.Subjects ?? [])}");
        Console.WriteLine($"    Storage       : {info.Config.Storage}");
        Console.WriteLine($"    Messages      : {info.State.Messages}");
        Console.WriteLine($"    Bytes         : {info.State.Bytes}");
        Console.WriteLine($"    First Seq     : {info.State.FirstSeq}");
        Console.WriteLine($"    Last Seq      : {info.State.LastSeq}");
        Console.WriteLine($"    Consumer Cnt  : {info.State.ConsumerCount}");
        Console.WriteLine($"    Retention     : {info.Config.Retention}");
        Console.WriteLine($"    Max Age       : {info.Config.MaxAge}");
        Console.WriteLine($"    Max Msgs      : {info.Config.MaxMsgs}");
        Console.WriteLine();

        Console.WriteLine("  CONSUMERS:");
        await foreach (var c in js.ListConsumersAsync("ORDERS"))
        {
            var ci = c.Info;
            var name = ci.Config.DurableName ?? ci.Name;
            var kind = ci.Config.DurableName != null ? "durable" : "ephemeral";

            Console.WriteLine($"    - {name} ({kind})");
            Console.WriteLine($"      Pending      : {ci.NumPending}");
            Console.WriteLine($"      Ack Pending  : {ci.NumAckPending}");
            Console.WriteLine($"      Redelivered  : {ci.NumRedelivered}");
            Console.WriteLine($"      Delivered    : {ci.Delivered.ConsumerSeq}");
            Console.WriteLine();
        }
    }
    catch (NatsJSApiException e)
    {
        Console.WriteLine($"  Error: {e.Error.Description}");
        Console.WriteLine("  (Is the ORDERS stream created? Run the Publisher first.)\n");
    }
}
