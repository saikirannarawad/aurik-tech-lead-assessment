using System.Security.Cryptography;
using System.Text;
using Aurik.Monitoring.Domain.Enums;

namespace Aurik.Monitoring.Application.Ingestion;

public static class IdempotencyKey
{
    /// <summary>
    /// Derive a stable idempotency key for a vendor payload when the caller did not provide one.
    /// Uses SHA-256(vendor + body). Same content = same key, so duplicate bodies collapse cleanly.
    /// </summary>
    public static string ForPayload(VendorType vendor, string rawJson)
    {
        var input = $"{vendor}:{rawJson}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
