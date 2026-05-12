namespace OpenClaw.Tray.Tests;

/// <summary>
/// Serializes any test class that mutates <c>OPENCLAW_TRAY_DATA_DIR</c>.
/// xunit otherwise runs distinct test classes in parallel, which creates a
/// race because env vars are process-wide.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TrayDataDirEnvCollection
{
    public const string Name = "TrayDataDirEnv";
}
