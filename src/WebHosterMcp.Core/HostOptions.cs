namespace WebHosterMcp.Core;

/// <summary>
/// Host-Konfiguration: HTTP/HTTPS-Listener.
/// Gebunden aus `appsettings.json` Sektion `Host`.
/// </summary>
public class HostOptions
{
    /// <summary>HTTP-Bind-Adresse (Default 0.0.0.0).</summary>
    public string Ip { get; set; } = "0.0.0.0";

    /// <summary>HTTP-Port (Default 3000).</summary>
    public int Port { get; set; } = 3000;

    /// <summary>HTTPS-Listener aktiv (Default true).</summary>
    public bool UseHttps { get; set; } = true;

    /// <summary>HTTPS-Port (Default 3443).</summary>
    public int HttpsPort { get; set; } = 3443;
}

/// <summary>HTTPS-Cert Optionen (MVP2 §2).</summary>
public class HttpsOptions
{
    /// <summary>Optionaler PFX-Pfad. null = kein PFX, Self-Signed Fallback.</summary>
    public string? CertPath { get; set; }

    /// <summary>PFX-Password.</summary>
    public string? CertPassword { get; set; }

    /// <summary>Self-Signed Cert Optionen.</summary>
    public HttpsSelfSignedOptions SelfSigned { get; set; } = new();
}

/// <summary>Self-Signed Cert Generation (MVP2 §2.2).</summary>
public class HttpsSelfSignedOptions
{
    public bool Enabled { get; set; } = true;
    public string CertDir { get; set; } = "./certs";
    public string? Cn { get; set; }
    public bool ForceRegenerate { get; set; }
}

/// <summary>Retention/Auto-Delete Optionen (MVP2 §3).</summary>
public class RetentionOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Default-TTL in Sekunden (Default 604800 = 7 Tage).</summary>
    public int DefaultTtlSeconds { get; set; } = 604_800;

    /// <summary>Background-Timer-Intervall in Sekunden (Default 3600 = 1h). Erlaubter Range: 1-86400.</summary>
    public int CheckIntervalSeconds { get; set; } = 3600;
}

/// <summary>Sites-Konfiguration (Host-Root-Keys in appsettings.json).</summary>
public class SitesOptions
{
    /// <summary>Site-Storage-Root (Default ./sites).</summary>
    public string SitesRoot { get; set; } = "./sites";

    /// <summary>Max File-Größe in Bytes auf Platte (Default 1 MB = 1048576).</summary>
    public int MaxFileSizeBytes { get; set; } = 1_048_576;
}
