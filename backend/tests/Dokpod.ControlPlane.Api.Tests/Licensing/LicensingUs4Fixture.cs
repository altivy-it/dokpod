using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.Licensing;
using Dokpod.Licensing;
using Npgsql;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.Licensing;

/// <summary>Composição Business montada sobre PostgreSQL real, com chave sintética gerada na execução (spec 031 US4).</summary>
internal sealed class LicensingUs4Fixture : IDisposable
{
    private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    /// <param name="trustedKeys">Chaves extras do trust store, além da sintética (T059: chave pública da CLI de emissão).</param>
    public LicensingUs4Fixture(string connectionString, params TrustedKey[] trustedKeys)
    {
        ConnectionString = connectionString;
        TrustStore = new TrustStore([TrustedKey.FromPublicKey("dokpod-test-1", key), .. trustedKeys]);
    }

    public string ConnectionString { get; }

    public TrustStore TrustStore { get; }

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public sealed record Services(
        ControlPlaneDbContext Context,
        LicenseStore Licenses,
        CapacityLedger Ledger,
        LicensingEvaluationStore Evaluation,
        LicenseStatusService Status,
        LicenseImportService Import,
        RevocationImportService Revocations,
        LicensingReportService Reports) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    /// <summary>Instâncias novas a cada chamada: o equivalente a um reinício da API sobre o mesmo banco.</summary>
    public Services Create(TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        var context = CreateContext(ConnectionString);
        var writer = new LicensingAuditWriter(context);
        var licenses = new LicenseStore(context, writer);
        var ledger = new CapacityLedger(context, new LicensingHostStore(context, writer));
        var evaluation = new LicensingEvaluationStore(context, writer);
        var evaluator = new LicenseStateEvaluator(TrustStore);
        var status = new LicenseStatusService(licenses, licenses, evaluator, clock, ledger, evaluation);
        var hosts = new LicensingHostService(new LicensingHostStore(context, writer), ledger, licenses, licenses, evaluator, clock);
        return new Services(
            context,
            licenses,
            ledger,
            evaluation,
            status,
            new LicenseImportService(licenses, licenses, evaluator, TrustStore, clock),
            new RevocationImportService(licenses, licenses, status, TrustStore, clock),
            new LicensingReportService(status, hosts, licenses, new LicensingChainReader(context), writer, clock));
    }

    public byte[] Revocation(int sequence, string licenseId, int revisionsUpTo = 1, string organizationId = "org_us4") =>
        Sign("dokpod-revocation+jws", $$"""
            {"schemaVersion":1,"organizationId":"{{organizationId}}","sequence":{{sequence}},"issuedAt":"{{Iso(DateTimeOffset.UtcNow)}}",
            "revokedLicenses":[{"licenseId":"{{licenseId}}","revisionsUpTo":{{revisionsUpTo}}}]}
            """);

    public byte[] License(string licenseId, DateTimeOffset notBefore, DateTimeOffset expiresAt, int productionQuota = 8, string capabilities = "\"change-windows\"", int revision = 1)
    {
        var payload = $$"""
            {"schemaVersion":1,"meteringVersion":1,"licenseId":"{{licenseId}}","organizationId":"org_us4","revision":{{revision}},
            "product":"dokpod-business","plan":"business","issuedAt":"{{Iso(notBefore)}}","notBefore":"{{Iso(notBefore)}}",
            "expiresAt":"{{Iso(expiresAt)}}","capacity":{"mode":"limited","nodeEquivalents":10},"vcpuBand":16,"windowDays":30,
            "graceDays":30,"slots":[{"slotId":"prd","kind":"production","enabled":true,"nodeEquivalents":{{productionQuota}}}],"capabilities":[{{capabilities}}]}
            """;
        return Sign("dokpod-license+jws", payload);
    }

    private byte[] Sign(string type, string payload)
    {
        var header = $$"""{"alg":"ES256","kid":"dokpod-test-1","typ":"{{type}}"}""";
        var input = $"{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(header))}.{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload))}";
        var signature = key.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Encoding.ASCII.GetBytes($"{input}.{Base64Url.EncodeToString(signature)}");
    }

    public async Task<LicenseImportResult> ImportAsync(byte[] license, TimeProvider? clock = null)
    {
        await using var services = Create(clock);
        var version = (await services.Licenses.ReadAsync(Ct))?.Version ?? 0;
        return await services.Import.ImportAsync(license, "prd", version, "user-us4", Guid.NewGuid(), Ct);
    }

    /// <summary>Recoloca o instante confiável no relógio atual, para outros testes não herdarem um estado indeterminado.</summary>
    public async Task ResetClockAsync()
    {
        await using var connection = await OpenAsync(ConnectionString, Ct);
        await ExecuteAsync(connection, null, "UPDATE dokpod.licensing_state SET last_trusted_instant_utc = now()", Ct);
    }

    public async Task<(DateTimeOffset? ExpiryUntil, DateTimeOffset? ExcessStarted, DateTimeOffset? ExcessUntil)> DeadlinesAsync()
    {
        await using var connection = await OpenAsync(ConnectionString, Ct);
        await using var command = new NpgsqlCommand("SELECT expiry_grace_until_utc, excess_started_at_utc, excess_grace_until_utc FROM dokpod.licensing_state", connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        DateTimeOffset? At(int i) => reader.IsDBNull(i) ? null : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(i), DateTimeKind.Utc));
        return (At(0), At(1), At(2));
    }

    public static string Iso(DateTimeOffset instant) => LicenseStatusService.Iso(instant);

    public void Dispose()
    {
        TrustStore.Dispose();
        key.Dispose();
    }
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
