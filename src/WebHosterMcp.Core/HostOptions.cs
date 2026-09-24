namespace WebHosterMcp.Core;

/// <summary>
/// Host configuration: HTTP/HTTPS listener.
/// Bound from the `appsettings.json` `Host` section.
/// </summary>
public class HostOptions
{
    /// <summary>HTTP bind address (default 0.0.0.0).</summary>
    public string Ip { get; set; } = "0.0.0.0";

    /// <summary>HTTP port (default 3000).</summary>
    public int Port { get; set; } = 3000;

    /// <summary>HTTPS listener enabled (default true).</summary>
    public bool UseHttps { get; set; } = true;

    /// <summary>HTTPS port (default 3443).</summary>
    public int HttpsPort { get; set; } = 3443;
}

/// <summary>HTTPS certificate options.</summary>
public class HttpsOptions
{
    /// <summary>Optional PFX path. null = no PFX, self-signed fallback.</summary>
    public string? CertPath { get; set; }

    /// <summary>PFX password.</summary>
    public string? CertPassword { get; set; }

    /// <summary>Self-signed certificate options.</summary>
    public HttpsSelfSignedOptions SelfSigned { get; set; } = new();
}

/// <summary>Self-signed certificate generation (RSA 2048, SAN entries, persisted PFX).</summary>
public class HttpsSelfSignedOptions
{
    public bool Enabled { get; set; } = true;
    public string CertDir { get; set; } = "./certs";
    public string? Cn { get; set; }
    public bool ForceRegenerate { get; set; }
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

    /// <summary>Max file size in bytes for inline `content` (default 1 MB = 1048576).</summary>
    public int MaxFileSizeBytes { get; set; } = 1_048_576;

    /// <summary>Max size in bytes for `payload.json` for a2ui/schema-form (default 1 MB).</summary>
    public int MaxPayloadSizeBytes { get; set; } = 1_048_576;

    /// <summary>Max size in bytes for submission bodies for json-schema-form (default 1 MB).</summary>
    public int MaxSubmissionSizeBytes { get; set; } = 1_048_576;
}

/// <summary>`src` download options (data URL / local path / HTTP/HTTPS).</summary>
public class SrcOptions
{
    /// <summary>HTTP timeout in seconds for `src` HTTP/HTTPS downloads. Default 30s.</summary>
    public int HttpTimeoutSeconds { get; set; } = 30;
}
