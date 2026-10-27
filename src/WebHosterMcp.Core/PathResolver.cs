namespace WebHosterMcp.Core;

/// <summary>
/// Resolves configuration-provided paths against the host executable directory
/// (not the process current working directory) so behaviour stays stable when
/// an MCP host spawns the process with a foreign cwd (e.g. C:\Windows\System32).
///
/// Spec 02-sites-storage v1.4: relative paths from appsettings.json are
/// anchored to <see cref="AppContext.BaseDirectory"/>. Absolute paths are kept
/// verbatim and normalised through <see cref="Path.GetFullPath(string)"/>.
/// </summary>
public static class PathResolver
{
    /// <summary>
    /// Resolves <paramref name="configured"/> to an absolute path.
    /// </summary>
    /// <param name="configured">Path from configuration (relative or absolute; null/whitespace becomes <paramref name="defaultRelative"/>).</param>
    /// <param name="defaultRelative">Fallback relative path when <paramref name="configured"/> is null/whitespace. Defaults to <c>./sites</c>.</param>
    public static string Resolve(string? configured, string defaultRelative = "./sites")
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = defaultRelative;
        }
        if (Path.IsPathRooted(configured))
        {
            return Path.GetFullPath(configured);
        }
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
    }
}
