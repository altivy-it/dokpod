using System.Text.Json;
using Dokpod.Agent;
using Dokpod.Agent.Application.Commands;
using Dokpod.Agent.Application.Containers;
using Dokpod.Agent.Application.Protocol;
using Dokpod.Agent.Infrastructure.Commands;
using Dokpod.Agent.Infrastructure.Secrets;
using Dokpod.Domain.Commands;
using Dokpod.LoadSim;
using Microsoft.Extensions.Logging;

// Spec 005 T013: N agentes reais (AgentControlWorker) com engine em memória contra o plano de controle.
// Uso: Dokpod.LoadSim <agents.json> <endpoint> <ca.crt> [containers=20] [churnMediaSegundos=120] [dataDir=/tmp/loadsim]
// agents.json: [{ "environmentId": "...", "pfx": "/caminho/agente.pfx" }]
if (args.Length < 3)
{
    Console.Error.WriteLine("Uso: Dokpod.LoadSim <agents.json> <endpoint> <ca.crt> [containers] [churnMediaSegundos] [dataDir]");
    return 2;
}

var agents = JsonSerializer.Deserialize<AgentSpec[]>(File.ReadAllText(args[0]), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
var endpoint = new Uri(args[1]);
var ca = args[2];
var containers = args.Length > 3 ? int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 20;
var churnMean = args.Length > 4 ? double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 120;
var dataRoot = args.Length > 5 ? args[5] : Path.Combine(Path.GetTempPath(), "loadsim");
using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; }).SetMinimumLevel(LogLevel.Warning));
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
AppDomain.CurrentDomain.ProcessExit += (_, _) => stop.Cancel();

if (Environment.GetEnvironmentVariable("LOADSIM_EXEC_LOG") is { Length: > 0 } execLog)
{
    FakeEngine.ExecutionLog = new StreamWriter(execLog, append: true) { AutoFlush = true };
}

var engines = new List<FakeEngine>();
var workers = new List<AgentControlWorker>();
for (var i = 0; i < agents.Length; i++)
{
    var random = new Random(i + 1);
    var engine = new FakeEngine(i + 1, containers, random);
    var data = Path.Combine(dataRoot, agents[i].EnvironmentId.ToString("N"));
    Directory.CreateDirectory(data);
    var options = new AgentOptions(data, "/nonexistent/docker.sock", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30), false, endpoint,
        agents[i].EnvironmentId, agents[i].Pfx, null, ca, TimeSpan.FromSeconds(15));
    var journal = new FileCommandJournal(data);
    var processor = new AgentCommandProcessor(new AgentCommandGate(journal, TimeProvider.System), journal, engine, TimeProvider.System);
    var worker = new AgentControlWorker(options, engine, new AgentCommandProtocolHandler(processor), new HostProbe(random),
        new HostSecretFileStore(Path.Combine(data, "secrets")), new ContainerCreationPolicy([], null), loggerFactory.CreateLogger<AgentControlWorker>());
    engines.Add(engine);
    workers.Add(worker);
}

Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} iniciando {agents.Length} agentes x {containers} containers (churn médio {churnMean}s por host)");
foreach (var worker in workers)
{
    await worker.StartAsync(stop.Token);
    await Task.Delay(50, stop.Token);
}

// Churn: cada host reinicia um container em intervalos exponenciais com a média informada.
var churn = engines.Select((engine, i) => Task.Run(async () =>
{
    var random = new Random(10_000 + i);
    while (!stop.IsCancellationRequested)
    {
        await Task.Delay(TimeSpan.FromSeconds(-Math.Log(1 - random.NextDouble()) * churnMean), stop.Token).ContinueWith(_ => { });
        if (!stop.IsCancellationRequested) engine.Churn();
    }
})).ToArray();

while (!stop.IsCancellationRequested)
{
    await Task.Delay(TimeSpan.FromSeconds(30), stop.Token).ContinueWith(_ => { });
    Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} agentes={agents.Length} comandos={engines.Sum(e => e.Commands)} reinícios={engines.Sum(e => e.Churned)}");
}

foreach (var worker in workers) await worker.StopAsync(CancellationToken.None);
await Task.WhenAll(churn);
return 0;

internal sealed record AgentSpec(Guid EnvironmentId, string Pfx);
