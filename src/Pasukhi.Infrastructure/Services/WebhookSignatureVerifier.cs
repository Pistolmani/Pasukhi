using System.Security.Cryptography;
using System.Text;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Services;

public class WebhookSignatureVerifier : IWebhookSignatureVerifier
{
    public bool Verify(string payload, string signatureHeader, string appSecret)
    {
        if (string.IsNullOrEmpty(appSecret))
            return false;

        if (string.IsNullOrEmpty(signatureHeader) ||
            !signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;

        var receivedHash = signatureHeader["sha256=".Length..];

        var keyBytes = Encoding.UTF8.GetBytes(appSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var computedHash = hmac.ComputeHash(payloadBytes);
        var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(receivedHash.ToLowerInvariant())
        );
    }
}
