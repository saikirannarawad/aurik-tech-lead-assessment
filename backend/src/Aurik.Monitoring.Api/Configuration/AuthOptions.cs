namespace Aurik.Monitoring.Api.Configuration;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>API keys per vendor name (matches VendorType.ToString()).</summary>
    public Dictionary<string, string> VendorApiKeys { get; set; } = new();
}
