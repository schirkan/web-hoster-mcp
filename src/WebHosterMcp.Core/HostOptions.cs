namespace WebHosterMcp.Core;

/// <summary>
/// Host configuration: HTTP listener.
/// Bound from the <c>appsettings.json</c> <c>Host</c> section.
/// </summary>
public class HostOptions
{
    /// <summary>HTTP bind address (default 0.0.0.0).</summary>
    public string Ip { get; set; } = "0.0.0.0";

    /// <summary>HTTP port (default 3000).</summary>
    public int Port { get; set; } = 3000;
}

/// <summary>Retention / auto-delete options.</summary>
public class RetentionOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Default TTL in seconds (default 604800 = 7 days).</summary>
    public int DefaultTtlSeconds { get; set; } = 604_800;

    /// <summary>Background-timer interval in seconds (default 3600 = 1h). Allowed range: 1-86400.</summary>
    public int CheckIntervalSeconds { get; set; } = 3600;
}

/// <summary>Sites configuration (host-root keys in appsettings.json).</summary>
public class SitesOptions
{
    /// <summary>Site storage root (default ./sites).</summary>
    public string SitesRoot { get; set; } = "./sites";

    /// <summary>Max file size in bytes for inline <c>content</c> (default 1 MB = 1048576).</summary>
    public int MaxFileSizeBytes { get; set; } = 1_048_576;

    /// <summary>Max size in bytes for <c>payload.json</c> for a2ui/schema-form (default 1 MB).</summary>
    public int MaxPayloadSizeBytes { get; set; } = 1_048_576;

    /// <summary>Max size in bytes for submission bodies for json-schema-form (default 1 MB).</summary>
    public int MaxSubmissionSizeBytes { get; set; } = 1_048_576;
}

/// <summary><c>src</c> download options (data URL / local path / HTTP URL).</summary>
public class SrcOptions
{
    /// <summary>HTTP timeout in seconds for <c>src</c> HTTP downloads. Default 30s.</summary>
    public int HttpTimeoutSeconds { get; set; } = 30;
}
