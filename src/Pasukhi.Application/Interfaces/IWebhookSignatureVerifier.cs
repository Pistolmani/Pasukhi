namespace Pasukhi.Application.Interfaces;

public interface IWebhookSignatureVerifier
{
    bool Verify(string payload, string signatureHeader, string appSecret);
}
