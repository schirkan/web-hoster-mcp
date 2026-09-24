using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace WebHosterMcp.Core;

public static class HttpsCertificateLoader
{
    public static X509Certificate2 LoadOrCreate(HostOptions hostOptions, HttpsOptions httpsOptions, string contentRoot)
    {
        var certPassword = httpsOptions.CertPassword ?? string.Empty;

        // 1) Manual PFX
        if (!string.IsNullOrWhiteSpace(httpsOptions.CertPath))
        {
            var pfxPath = ResolvePath(httpsOptions.CertPath!, contentRoot);
            if (File.Exists(pfxPath))
            {
                try
                {
                    return new X509Certificate2(pfxPath, certPassword, X509KeyStorageFlags.Exportable);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("cert_load_failed", ex);
                }
            }
        }

        // 2) Self-signed fallback
        if (httpsOptions.SelfSigned.Enabled)
        {
            try
            {
                return LoadOrCreateSelfSigned(httpsOptions, certPassword, contentRoot);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("self_signed_failed", ex);
            }
        }

        // 3) No HTTPS certificate available
        throw new InvalidOperationException("https_startup_failed");
    }

    private static X509Certificate2 LoadOrCreateSelfSigned(HttpsOptions httpsOptions, string password, string contentRoot)
    {
        var certDir = ResolvePath(httpsOptions.SelfSigned.CertDir, contentRoot);
        Directory.CreateDirectory(certDir);

        var cn = string.IsNullOrWhiteSpace(httpsOptions.SelfSigned.Cn)
            ? Environment.MachineName
            : httpsOptions.SelfSigned.Cn!;

        var safeName = SanitizeFileName(cn.ToLowerInvariant());
        var certPath = Path.Combine(certDir, $"{safeName}.pfx");

        if (File.Exists(certPath) && !httpsOptions.SelfSigned.ForceRegenerate)
        {
            return new X509Certificate2(certPath, password, X509KeyStorageFlags.Exportable);
        }

        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            new X500DistinguishedName($"CN={cn}"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        req.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        req.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(cn);

        var lanIp = LanIpDetector.GetLanIpv4();
        if (!string.IsNullOrWhiteSpace(lanIp) && IPAddress.TryParse(lanIp, out var lanIpAddress))
        {
            san.AddIpAddress(lanIpAddress);
        }

        san.AddIpAddress(IPAddress.Loopback);
        req.CertificateExtensions.Add(san.Build());

        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var pfx = cert.Export(X509ContentType.Pfx, password);
        File.WriteAllBytes(certPath, pfx);

        return new X509Certificate2(pfx, password, X509KeyStorageFlags.Exportable);
    }

    private static string ResolvePath(string path, string contentRoot)
        => Path.IsPathFullyQualified(path) ? path : Path.GetFullPath(Path.Combine(contentRoot, path));

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(input.Length);

        foreach (var c in input)
        {
            sb.Append(invalid.Contains(c) ? '-' : c);
        }

        return sb.ToString();
    }
}
