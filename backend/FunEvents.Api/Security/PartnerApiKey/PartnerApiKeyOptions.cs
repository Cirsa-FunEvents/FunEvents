namespace FunEvents.Api.Security.PartnerApiKey;

public sealed class PartnerApiKeyOptions
{
    public Dictionary<string, string> Channels { get; set; } = new(StringComparer.Ordinal);
}
