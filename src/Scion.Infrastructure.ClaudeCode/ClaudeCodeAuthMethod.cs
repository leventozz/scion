using System.Text.Json;
using Scion.Application.Adapting;

namespace Scion.Infrastructure.ClaudeCode;

/// <summary>
/// Reads the <c>authMethod</c> of <c>claude auth status</c> (documented values: <c>none</c>, <c>claude.ai</c>,
/// <c>oauth_token</c>, <c>api_key</c>, <c>api_key_helper</c>, <c>third_party</c>) and nothing else of what it prints.
/// </summary>
internal static class ClaudeCodeAuthMethod
{
    public static AiUsageBasis UsageBasisIn(string authStatusJson)
    {
        try
        {
            using var document = JsonDocument.Parse(authStatusJson);
            var method = document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("authMethod", out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;

            return method switch
            {
                "claude.ai" or "oauth_token" => AiUsageBasis.Subscription,
                "api_key" or "api_key_helper" => AiUsageBasis.ApiKey,
                _ => AiUsageBasis.Unknown,
            };
        }
        catch (JsonException)
        {
            return AiUsageBasis.Unknown;
        }
    }
}
