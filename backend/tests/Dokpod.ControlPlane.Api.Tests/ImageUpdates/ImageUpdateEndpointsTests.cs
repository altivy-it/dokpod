using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.ImageUpdates;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.ImageUpdates;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.ImageUpdates;

/// <summary>
/// Spec 025 T016 com a agenda no PostgreSQL real: "Verificar agora" autoriza <c>image:pull</c> (Keycloak indisponível
/// nega), confere a licença, limita a 1 por minuto e audita <c>image.update_check_requested</c> na categoria <c>images</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ImageUpdateEndpointsTests : IAsyncLifetime
{
    private readonly Guid environmentId = Guid.NewGuid();
    private readonly ImageUpdateSchedulerTests.ManualTime time = new(DateTimeOffset.UtcNow);
    private readonly Decider decider = new();
    private readonly Gate gate = new();
    private readonly Channel channel = new() { Connected = true, Supported = true };
    private readonly Audit audit = new();
    private ControlPlaneDbContext? context;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        context = NetworksIntegrationTests.CreateContextOrSkip();
        await new PostgresEnvironmentRegistrationStore(context).CreateAsync(
            EnvironmentRegistration.Create(environmentId, $"t025e-{environmentId:N}"[..20], "host-t025e", true, [EnvironmentResourceScopes.Read]), Ct);
    }

    [Fact]
    public async Task Accepted_MovesTheHostToDue_AndIsAudited_ThenRateLimitedForAMinute()
    {
        var first = await RequestAsync();
        var second = await RequestAsync();

        Assert.Equal(ImageUpdateCheckOutcome.Accepted, first.Outcome);
        Assert.Equal(time.Now, first.RequestedAt);
        Assert.Contains(environmentId, await new ImageUpdateStore(context!).ListDueAsync(time.Now, 1000, Ct));
        Assert.Equal((ImageUpdateCheckOutcome.RateLimited, "rate_limited"), (second.Outcome, second.Code));
        Assert.Equal(time.Now + ImageUpdateLimits.ManualCooldown, second.RetryAt);
        Assert.Equal([(AuditOutcome.Succeeded, (string?)null), (AuditOutcome.Denied, "rate_limited")], audit.Events.Select(e => (e.Outcome, e.FailureCode)));
        Assert.All(audit.Events, e => Assert.Equal(("images", environmentId), (AuditCategories.CategoryOf(e.Action), e.EnvironmentId)));

        time.Now += ImageUpdateLimits.ManualCooldown;
        Assert.Equal(ImageUpdateCheckOutcome.Accepted, (await RequestAsync()).Outcome);
    }

    [Theory]
    [InlineData(AuthorizationDecisionOutcome.Denied)]
    [InlineData(AuthorizationDecisionOutcome.Indeterminate)]
    public async Task WithoutImagePull_OrWithKeycloakUnavailable_IsDenied_WithoutTouchingTheSchedule(AuthorizationDecisionOutcome outcome)
    {
        decider.Outcome = outcome;

        var result = await RequestAsync();

        Assert.Equal((ImageUpdateCheckOutcome.AuthorizationDenied, "authorization_denied"), (result.Outcome, result.Code));
        Assert.Equal(EnvironmentResourceScopes.PullImage, decider.Scope);
        Assert.Null(await new ImageUpdateStore(context!).GetScheduleAsync(environmentId, Ct));
        Assert.Empty(audit.Events);
    }

    [Theory]
    [InlineData("license_expired")]
    [InlineData("license_invalid")]
    public async Task LicenseDenial_KeepsTheCode(string code)
    {
        gate.Denial = code;

        var result = await RequestAsync();

        Assert.Equal((ImageUpdateCheckOutcome.LicenseDenied, code), (result.Outcome, result.Code));
        Assert.Equal((AuditOutcome.Denied, code), (audit.Events.Single().Outcome, audit.Events.Single().FailureCode));
    }

    [Fact]
    public async Task DisconnectedOrUnsupportedAgent_IsAConflict()
    {
        channel.Supported = false;
        Assert.Equal(ImageUpdateCheckOutcome.AgentUnsupported, (await RequestAsync()).Outcome);

        channel.Connected = false;
        Assert.Equal(ImageUpdateCheckOutcome.EnvironmentDisconnected, (await RequestAsync()).Outcome);
    }

    [Fact]
    public async Task UnknownHost_IsNotFound_BeforeAnyAuthorizationCall()
    {
        var result = await Service().RequestAsync(Guid.NewGuid(), new AuthenticatedActor("user-1"), "token", Guid.NewGuid(), Ct);

        Assert.Equal(ImageUpdateCheckOutcome.EnvironmentNotFound, result.Outcome);
        Assert.Null(decider.Scope);
    }

    private Task<ImageUpdateCheckServiceResult> RequestAsync() =>
        Service().RequestAsync(environmentId, new AuthenticatedActor("user-1") { DisplayName = "Ana" }, "token", Guid.NewGuid(), Ct);

    private ImageUpdateCheckService Service() => new(decider, new PostgresEnvironmentRegistrationStore(context!), gate, new ImageUpdateStore(context!), channel, audit, time);

    public async ValueTask DisposeAsync()
    {
        if (context is not null) await context.DisposeAsync();
    }

    private sealed class Decider : IEnvironmentAuthorizationDecider
    {
        public AuthorizationDecisionOutcome Outcome { get; set; } = AuthorizationDecisionOutcome.Allowed;

        public string? Scope { get; private set; }

        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken)
        {
            Scope = scope;
            return Outcome == AuthorizationDecisionOutcome.Indeterminate
                ? throw new HttpRequestException("keycloak indisponível")
                : Task.FromResult(new EnvironmentAuthorizationDecision(Outcome, Outcome == AuthorizationDecisionOutcome.Allowed ? null : "denied"));
        }
    }

    private sealed class Gate : IBusinessCapabilityGate
    {
        public string? Denial { get; set; }

        public Task<BusinessCapabilityDecision> EnsureAsync(string capability, CancellationToken cancellationToken) =>
            Task.FromResult(Denial is null ? BusinessCapabilityDecision.Allow : BusinessCapabilityDecision.Deny(Denial, []));
    }

    private sealed class Channel : IImageUpdateAgentChannel
    {
        public bool Connected { get; set; }

        public bool Supported { get; set; }

        public bool IsConnected(Guid environmentId) => Connected;

        public bool Supports(Guid environmentId) => Supported;

        public Task<bool> SendAsync(Guid environmentId, ImageUpdateCheckRequest request, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class Audit : IAuditEventWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }
}
