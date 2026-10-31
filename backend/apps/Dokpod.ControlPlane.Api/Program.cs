using Dokpod.ControlPlane.Api;

if (args is ["--describe-edition"])
{
    Console.WriteLine(ProductEdition.Describe());
    return;
}

var builder = ApiHost.CreateBuilder(args);
// Spec 031 D-009: a edição é fixada no build; a CE não registra o licenciamento.
ProductEdition.AddServices(builder.Services);
var app = builder.Build();
ApiHost.MapEndpoints(app);
ProductEdition.MapEndpoints(app);
app.Run();

public partial class Program;
