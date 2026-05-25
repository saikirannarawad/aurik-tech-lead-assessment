using System.Text.Json;
using Aurik.Monitoring.Api.Configuration;
using Aurik.Monitoring.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Aurik.Monitoring.Api.Middleware;

/// <summary>
/// Requires the X-Vendor-Api-Key header on /api/ingestion/* requests. Matches the supplied key
/// against the configured key for the URL-segment vendor (e.g. /api/ingestion/pulseforge).
/// Read endpoints are not protected — change as needed.
/// </summary>
public sealed class ApiKeyAuthMiddleware
{
    public const string HeaderName = "X-Vendor-Api-Key";
    private const string IngestionPrefix = "/api/ingestion/";

    private readonly RequestDelegate _next;
    private readonly AuthOptions _options;

    public ApiKeyAuthMiddleware(RequestDelegate next, IOptions<AuthOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith(IngestionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var vendorSegment = path[IngestionPrefix.Length..].Split('/')[0];
        if (!Enum.TryParse<VendorType>(vendorSegment, ignoreCase: true, out var vendor))
        {
            await WriteUnauthorizedAsync(context, "Unknown vendor in URL.").ConfigureAwait(false);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var supplied) || string.IsNullOrEmpty(supplied))
        {
            await WriteUnauthorizedAsync(context, $"Missing {HeaderName} header.").ConfigureAwait(false);
            return;
        }

        if (!_options.VendorApiKeys.TryGetValue(vendor.ToString(), out var expected)
            || !FixedTimeEquals(supplied!, expected))
        {
            await WriteUnauthorizedAsync(context, "Invalid API key for vendor.").ConfigureAwait(false);
            return;
        }

        context.Items["Vendor"] = vendor;
        await _next(context).ConfigureAwait(false);
    }

    private static async Task WriteUnauthorizedAsync(HttpContext ctx, string reason)
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { error = "unauthorized", reason }))
            .ConfigureAwait(false);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
