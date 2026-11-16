using Dokpod.ControlPlane.Api;

if (args is ["--describe-edition"])
{
    Console.WriteLine(ProductEdition.Describe());
    return 0;
}

// Spec 033 T010 (D-003, D-004): modo administrativo; não sobe Kestrel, gRPC, SignalR nem hosted services.
if (args is [Dokpod.ControlPlane.Api.Admin.AdminCommandHost.Verb, ..])
{
    return await Dokpod.ControlPlane.Api.Admin.AdminCommandHost.RunAsync(args[1..], Dokpod.ControlPlane.Api.Admin.AdminContext.ForImage());
}

var builder = ApiHost.CreateBuilder(args);
// Spec 031 D-009: a edição é fixada no build; a CE não registra o licenciamento.
ProductEdition.AddServices(builder.Services);
var app = builder.Build();
ApiHost.MapEndpoints(app);
ProductEdition.MapEndpoints(app);
app.Run();
return 0;

public partial class Program;
