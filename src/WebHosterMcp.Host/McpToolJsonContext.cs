using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WebHosterMcp.Host;

/// <summary>
/// Source-generated JSON context for the MCP tool request/response DTOs declared in
/// <see cref="SiteTools"/>. The MCP C# SDK builds tool parameter marshallers via
/// <see cref="System.Text.Json.JsonSerializerOptions"/>, which defaults to a limited
/// source-generation resolver chain that does not know any of our tool DTOs. This
/// context registers them so that <c>deploy</c>, <c>list_sites</c>,
/// <c>get_site_info</c>, <c>delete_site</c> and <c>get_submissions</c> can be
/// invoked without crashing the server at startup.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DeployToolFileEntry))]
[JsonSerializable(typeof(IReadOnlyList<DeployToolFileEntry>))]
[JsonSerializable(typeof(DeployToolResultFile))]
[JsonSerializable(typeof(IReadOnlyList<DeployToolResultFile>))]
[JsonSerializable(typeof(DeployToolResponse))]
[JsonSerializable(typeof(ListSitesItem))]
[JsonSerializable(typeof(IReadOnlyList<ListSitesItem>))]
[JsonSerializable(typeof(GetSiteInfoResponse))]
[JsonSerializable(typeof(DeleteSiteResponse))]
[JsonSerializable(typeof(SubmissionInfoDto))]
[JsonSerializable(typeof(IReadOnlyList<SubmissionInfoDto>))]
internal sealed partial class McpToolJsonContext : JsonSerializerContext
{
}
