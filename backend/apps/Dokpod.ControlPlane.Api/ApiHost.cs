using Dokpod.ControlPlane.Application.Events;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Overview;
using Dokpod.ControlPlane.Application.Realtime;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Net.Security;
using System.Net;
using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Api.Agents;
using Dokpod.ControlPlane.Api.Commands;
using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Dokpod.ControlPlane.Api.Realtime;
using Dokpod.ControlPlane.Api.Authorization;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Inventory;
using System.Security.Claims;
using Dokpod.ControlPlane.Api.Endpoints;
using Dokpod.ControlPlane.Api.Health;

namespace Dokpod.ControlPlane.Api;

public sealed record ApiHostOptions(
    int GrpcPort,
    int HealthPort,
    X509Certificate2 ServerCertificate,
    Func<X509Certificate2, X509Chain?, SslPolicyErrors, bool>? ClientCertificateValidation = null,
    bool LoopbackOnly = false,
    Action<IServiceCollection>? ConfigureServices = null);

public static class ApiHost
{
    public static WebApplicationBuilder CreateBuilder(
        string[] args,
        ApiHostOptions? options = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        // URLs com query não vão aos logs: `contains` dos logs sob demanda (spec 011) e demais filtros
        // apareceriam em "Request starting ..." e no log do HttpClient em nível Information.
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        builder.AddDokpodTelemetry();
        options ??= LoadOptionsFromEnvironment();
        ArgumentNullException.ThrowIfNull(
            options.ServerCertificate,
            nameof(ApiHostOptions.ServerCertificate));

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            var listenHealth = options.LoopbackOnly
                ? (Action<int, Action<ListenOptions>>)((port, configure) => serverOptions.Listen(IPAddress.Loopback, port, configure))
                : serverOptions.ListenAnyIP;
            var listenGrpc = options.LoopbackOnly
                ? (Action<int, Action<ListenOptions>>)((port, configure) => serverOptions.Listen(IPAddress.Loopback, port, configure))
                : serverOptions.ListenAnyIP;

            listenHealth(options.HealthPort, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1;
            });

            listenGrpc(options.GrpcPort, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
                listenOptions.UseHttps(httpsOptions =>
                {
                    httpsOptions.ServerCertificate = options.ServerCertificate;
                    httpsOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                    httpsOptions.CheckCertificateRevocation = options.ClientCertificateValidation is null;
                    httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                    if (options.ClientCertificateValidation is not null)
                    {
                        httpsOptions.ClientCertificateValidation = options.ClientCertificateValidation;
                    }
                });
            });
        });

        builder.Configuration["Authentication:Keycloak:Authority"] ??= "https://keycloak.invalid/realms/dokpod";
        builder.Configuration["Authentication:Keycloak:BackchannelAuthority"] ??=
            builder.Configuration["Authentication:Keycloak:Authority"];
        builder.Configuration["Authentication:Keycloak:Audience"] ??= "dokpod-api";
        builder.Configuration["Authentication:Keycloak:DecisionTimeoutSeconds"] ??= "3";

        builder.Services.AddGrpc(options =>
        {
            options.MaxReceiveMessageSize = 1_048_576;
            options.MaxSendMessageSize = 1_048_576;
        });
        var authority = builder.Configuration["Authentication:Keycloak:Authority"];
        var backchannelAuthority = builder.Configuration["Authentication:Keycloak:BackchannelAuthority"];
        var audience = builder.Configuration["Authentication:Keycloak:Audience"];
        builder.Services.AddOptions<KeycloakAuthorizationOptions>()
            .BindConfiguration(KeycloakAuthorizationOptions.SectionName)
            .ValidateOnStart();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.MetadataAddress = $"{backchannelAuthority!.TrimEnd('/')}/.well-known/openid-configuration";
                options.Audience = audience;
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                options.MapInboundClaims = false;
                options.TokenValidationParameters.ValidIssuer = authority!.TrimEnd('/');
                options.BackchannelHttpHandler = new KeycloakBackchannelHandler(
                    new Uri(authority, UriKind.Absolute),
                    new Uri(backchannelAuthority, UriKind.Absolute));
            });
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());
        var terminalOptions = builder.Configuration.GetSection(Dokpod.ControlPlane.Application.Terminal.TerminalOptions.SectionName).Get<Dokpod.ControlPlane.Application.Terminal.TerminalOptions>()
            ?? new Dokpod.ControlPlane.Application.Terminal.TerminalOptions();
        if (!terminalOptions.IsValid()) throw new InvalidOperationException("Terminal options are invalid.");
        builder.Services.AddSingleton(terminalOptions);
        builder.Services.AddSingleton<Dokpod.ControlPlane.Application.Terminal.TerminalSessionRegistry>();
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Terminal.TerminalSessionService>();
        // Spec 014 D-010: queda do navegador detectada em até 30 s (terminal e logs ao vivo).
        builder.Services.AddSignalR(options =>
        {
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddSingleton<IValidateOptions<KeycloakAuthorizationOptions>, KeycloakAuthorizationOptionsValidator>();
        builder.Services.AddHttpClient<IEnvironmentAuthorizationDecider, KeycloakAuthorizationDecisionService>(
            (provider, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(
                provider.GetRequiredService<IOptions<KeycloakAuthorizationOptions>>().Value.DecisionTimeoutSeconds);
        });
        builder.Services.AddHttpClient<IEnvironmentGrantReader, KeycloakEnvironmentGrantReader>((provider, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(
                provider.GetRequiredService<IOptions<KeycloakAuthorizationOptions>>().Value.DecisionTimeoutSeconds);
        });
        builder.Services.AddHttpClient(nameof(KeycloakReadinessHealthCheck), (provider, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(
                provider.GetRequiredService<IOptions<KeycloakAuthorizationOptions>>().Value.DecisionTimeoutSeconds);
        });
        var healthChecks = builder.Services.AddHealthChecks()
            .AddCheck("controlplane-api", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["ready", "api"])
            .AddCheck<KeycloakReadinessHealthCheck>("keycloak", tags: ["ready", "keycloak"]);
        builder.Services.AddSingleton<IAgentIdentityRegistry, UnavailableAgentIdentityRegistry>();
        builder.Services.AddSingleton<IAgentSessionStore, InMemoryAgentSessionStore>();
        builder.Services.AddSingleton<IAgentCommandDeliveryQueue, InMemoryAgentCommandDeliveryQueue>();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddScoped<AgentSessionNegotiator>();
        builder.Services.AddScoped<IAuditEventWriter, UnavailableAuditEventWriter>();
        builder.Services.AddScoped<IAgentCommandStore, UnavailableAgentCommandStore>();
        builder.Services.AddScoped<IEnvironmentRegistrationStore, UnavailableEnvironmentRegistrationStore>();
        builder.Services.AddScoped<IInventoryProjectionStore, UnavailableInventoryProjectionStore>();
        builder.Services.AddScoped<EnvironmentAccessService>();
        builder.Services.AddScoped<EnvironmentRegistrationService>();
        builder.Services.AddScoped<AgentIdentityRevocationService>();
        builder.Services.AddScoped<AgentCommandQueueService>();
        builder.Services.AddScoped<ContainerCommandService>();
        builder.Services.AddScoped<ContainerCommandQueryService>();
        builder.Services.AddScoped<AgentCommandStatusService>();
        var commandRetention = builder.Configuration.GetSection(CommandRetentionOptions.SectionName).Get<CommandRetentionOptions>() ?? new CommandRetentionOptions();
        if (!commandRetention.IsValid()) throw new InvalidOperationException("Commands:Retention is invalid.");
        builder.Services.AddSingleton(commandRetention);
        builder.Services.AddHostedService<AgentCommandExpirationWorker>();
        var imagePullPolicy = builder.Configuration.GetSection("Images").Get<Dokpod.ControlPlane.Application.Images.ImagePullPolicy>() ?? new Dokpod.ControlPlane.Application.Images.ImagePullPolicy();
        builder.Services.AddSingleton(imagePullPolicy);
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Images.IImageInventoryStore, UnavailableImageInventoryStore>();
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Images.ImagePullService>();
        // Spec 013 D-006: key ring próprio da API (separado do BFF) para valores de comandos.
        var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Dokpod.ControlPlane.Api");
        var keysPath = builder.Configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            if (!Path.IsPathFullyQualified(keysPath)) throw new InvalidOperationException("DataProtection:KeysPath must be an absolute path.");
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        builder.Services.AddSingleton<Dokpod.ControlPlane.Application.Commands.ICommandParameterProtector, DataProtectionCommandParameterProtector>();
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Containers.ContainerCreationService>();
        builder.Services.AddScoped<InventoryProjectionService>();
        builder.Services.AddScoped<InventoryQueryService>();
        var engineSupport = builder.Configuration.GetSection(EngineSupportOptions.SectionName).Get<EngineSupportOptions>() ?? new EngineSupportOptions();
        if (!engineSupport.IsValid()) throw new InvalidOperationException("EngineSupport minimum versions are invalid.");
        builder.Services.AddSingleton(engineSupport);
        builder.Services.AddSingleton<AgentLivenessTracker>();
        builder.Services.AddSingleton<ControlPlaneMetrics>();
        builder.Services.AddSingleton<IControlPlaneNotifier, SignalRControlPlaneNotifier>();
        builder.Services.AddScoped<UnavailableAgentHostStore>();
        builder.Services.AddScoped<IAgentHostStore>(provider => provider.GetRequiredService<UnavailableAgentHostStore>());
        builder.Services.AddScoped<IInventoryStatisticsStore>(provider => provider.GetRequiredService<UnavailableAgentHostStore>());
        builder.Services.AddScoped<IEnvironmentStageStore>(provider => provider.GetRequiredService<UnavailableAgentHostStore>());
        builder.Services.AddScoped<IOperationalEventStore, UnavailableOperationalEventStore>();
        builder.Services.AddScoped<AuthorizedEnvironmentsService>();
        builder.Services.AddScoped<HostQueryService>();
        builder.Services.AddScoped<EnvironmentStageService>();
        builder.Services.AddScoped<OperationalEventService>();
        // Spec 016: governança de alertas. Sem banco, o avaliador não é registrado e a gravação de eventos segue igual.
        builder.Services.AddSingleton<Dokpod.ControlPlane.Application.Alerts.AlertConditionState>();
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Alerts.AlertEvaluator>();
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Alerts.AlertSweepService>();
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Alerts.AlertGovernanceService>();
        // Spec 017: consulta da auditoria e nome do ator nos eventos novos.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<Dokpod.ControlPlane.Api.Auditing.AuditActorNameCache>();
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Auditing.IAuditActorNameProvider, Dokpod.ControlPlane.Api.Auditing.HttpAuditActorNameProvider>();
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Auditing.AuditQueryService>();
        // Spec 018: remoção de volume (o store só existe com banco).
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Volumes.VolumeRemovalService>();
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Networks.NetworkRemovalService>();
        // Spec 020: catálogo de registros e política editável (os stores só existem com banco).
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Registries.RegistryCatalog>();
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Registries.RegistryPolicyService>();
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Pods.PodActionService>();
        builder.Services.AddHostedService<Dokpod.ControlPlane.Api.Alerts.AlertSweeper>();
        builder.Services.AddScoped<OverviewQueryService>();
        builder.Services.AddScoped<IContainerSearchStore, UnavailableContainerSearchStore>();
        builder.Services.AddScoped<ContainerSearchService>();
        builder.Services.AddSingleton<Dokpod.ControlPlane.Application.Queries.AgentQueryDispatcher>();
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Queries.ContainerInsightsService>();
        builder.Services.AddScoped<AgentHostActivity>();
        builder.Services.AddHostedService<AgentConnectionSweeper>();
        builder.Services.AddHostedService<EventRetentionWorker>();
        var metricsOptions = builder.Configuration.GetSection(Dokpod.ControlPlane.Application.Metrics.MetricsOptions.SectionName)
            .Get<Dokpod.ControlPlane.Application.Metrics.MetricsOptions>() ?? new Dokpod.ControlPlane.Application.Metrics.MetricsOptions();
        if (!metricsOptions.IsValid()) throw new InvalidOperationException("Metrics options are invalid.");
        builder.Services.AddSingleton(metricsOptions);
        builder.Services.AddSingleton<Dokpod.ControlPlane.Application.Metrics.LatestMetricsCache>();
        builder.Services.AddSingleton<Dokpod.ControlPlane.Application.Metrics.ContainerMetricsBuffer>();
        builder.Services.AddSingleton<Dokpod.ControlPlane.Application.Metrics.HostMinuteAggregator>();
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Metrics.IHostMetricsStore, UnavailableHostMetricsStore>();
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Metrics.MetricsIngestionService>();
        builder.Services.AddScoped<Dokpod.ControlPlane.Application.Metrics.MetricsQueryService>();
        builder.Services.AddHostedService<Dokpod.ControlPlane.Api.Metrics.MetricsMaintenanceWorker>();
        var databaseConnectionString = builder.Configuration.GetConnectionString("ControlPlane");
        if (!string.IsNullOrWhiteSpace(databaseConnectionString))
        {
            builder.Services.AddControlPlaneInfrastructure(databaseConnectionString);
            builder.Services.AddHostedService<RegistryPolicyRefresher>();
            builder.Services.AddScoped<Dokpod.ControlPlane.Application.Events.IOperationalEventObserver>(provider =>
                provider.GetRequiredService<Dokpod.ControlPlane.Application.Alerts.AlertEvaluator>());
            healthChecks.AddCheck<PostgresReadinessHealthCheck>("postgresql", tags: ["ready", "database"]);
        }
        else
        {
            healthChecks.AddCheck(
                "postgresql",
                () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("PostgreSQL não configurado."),
                tags: ["ready", "database"]);
        }

        options.ConfigureServices?.Invoke(builder.Services);
        return builder;
    }

    public static void MapEndpoints(WebApplication app)
    {
        app.MapGrpcService<AgentControlService>().AllowAnonymous();
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
        }).AllowAnonymous();
        app.MapHealthChecks("/health/ready/database", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("database"),
        }).AllowAnonymous();
        app.MapHealthChecks("/health/ready/keycloak", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("keycloak"),
        }).AllowAnonymous();
        app.UseAuthentication();
        app.UseAuthorization();
        // Spec 017: lembra o nome do usuário para eventos de auditoria gravados fora da requisição (fim do terminal).
        app.Use((context, next) =>
        {
            context.RequestServices.GetService<Dokpod.ControlPlane.Api.Auditing.AuditActorNameCache>()?.Remember(context.User);
            return next(context);
        });
        app.MapGet("/api/v1/session", (ClaimsPrincipal principal) => Results.Ok(new
        {
            subject = principal.FindFirstValue("sub")
        })).RequireAuthorization();
        EnvironmentEndpoints.Map(app);
        InventoryEndpoints.Map(app);
        ConsoleEndpoints.Map(app);
        ImageEndpoints.Map(app);
        ContainerCreationEndpoints.Map(app);
        TerminalEndpoints.Map(app);
        ContainerDetailEndpoints.Map(app);
        MetricsEndpoints.Map(app);
        ContainerCommandEndpoints.Map(app);
        AlertEndpoints.Map(app);
        AuditEndpoints.Map(app);
        VolumeEndpoints.Map(app);
        NetworkEndpoints.Map(app);
        RegistryEndpoints.Map(app);
        PodEndpoints.Map(app);
        SettingsEndpoints.Map(app);
        app.MapHub<ControlPlaneHub>("/hubs/control-plane").RequireAuthorization();
    }

    private static ApiHostOptions LoadOptionsFromEnvironment()
    {
        var certificatePath = Environment.GetEnvironmentVariable("DOKPOD_API_CERTIFICATE_PATH")
            ?? throw new InvalidOperationException("DOKPOD_API_CERTIFICATE_PATH is required.");
        var certificatePassword = Environment.GetEnvironmentVariable("DOKPOD_API_CERTIFICATE_PASSWORD");
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath, certificatePassword);
        var clientCaPath = Environment.GetEnvironmentVariable("DOKPOD_API_CLIENT_CA_CERTIFICATE_PATH");
        var clientCaCertificate = string.IsNullOrWhiteSpace(clientCaPath)
            ? null
            : X509CertificateLoader.LoadCertificateFromFile(clientCaPath);
        var port = int.TryParse(Environment.GetEnvironmentVariable("DOKPOD_API_GRPC_PORT"), out var configuredPort)
            ? configuredPort
            : 7443;
        var healthPort = int.TryParse(Environment.GetEnvironmentVariable("DOKPOD_API_HEALTH_PORT"), out var configuredHealthPort)
            ? configuredHealthPort
            : 8080;

        Func<X509Certificate2, X509Chain?, SslPolicyErrors, bool>? clientValidation =
            clientCaCertificate is null
                ? null
                : (certificate, chain, errors) => ValidateClientCertificate(certificate, clientCaCertificate);

        return new ApiHostOptions(
            port,
            healthPort,
            certificate,
            clientValidation);
    }

    private static bool ValidateClientCertificate(
        X509Certificate2? clientCertificate,
        X509Certificate2 trustedRoot)
    {
        if (clientCertificate is null)
        {
            return false;
        }

        const string clientAuthenticationOid = "1.3.6.1.5.5.7.3.2";
        var eku = clientCertificate.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .SingleOrDefault();
        if (eku is null || !eku.EnhancedKeyUsages.Cast<Oid>().Any(oid => oid.Value == clientAuthenticationOid))
        {
            return false;
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(trustedRoot);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(clientCertificate);
    }
}