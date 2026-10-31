using Dokpod.ControlPlane.Api;
#if DOKPOD_BUSINESS
using Dokpod.ControlPlane.Api.Licensing;
#endif

var builder = ApiHost.CreateBuilder(args);
#if DOKPOD_BUSINESS
// Spec 031 D-009: a edição é fixada no build; a imagem CE não registra o licenciamento.
builder.Services.AddDokpodBusinessLicensing();
#endif
var app = builder.Build();
ApiHost.MapEndpoints(app);
#if DOKPOD_BUSINESS
app.MapLicensingEndpoints();
#endif
app.Run();

public partial class Program;
