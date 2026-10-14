namespace Dokpod.Domain.Environments;

public enum EnvironmentStage
{
    Production,
    Staging,
    Development,
}

public static class EnvironmentStages
{
    public static string ToValue(EnvironmentStage stage) => stage switch
    {
        EnvironmentStage.Production => "production",
        EnvironmentStage.Staging => "staging",
        EnvironmentStage.Development => "development",
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    };

    public static bool TryParse(string? value, out EnvironmentStage stage)
    {
        switch (value)
        {
            case "production":
                stage = EnvironmentStage.Production;
                return true;
            case "staging":
                stage = EnvironmentStage.Staging;
                return true;
            case "development":
                stage = EnvironmentStage.Development;
                return true;
            default:
                stage = default;
                return false;
        }
    }

    public static EnvironmentStage? ParseNullable(string? value) =>
        value is null ? null : TryParse(value, out var stage) ? stage : throw new ArgumentException("Unsupported stage.", nameof(value));
}
