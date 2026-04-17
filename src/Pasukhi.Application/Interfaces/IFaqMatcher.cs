using Pasukhi.Domain.Entities;

namespace Pasukhi.Application.Interfaces;

public record FaqMatchResult(FaqItem FaqItem, double Confidence);

public interface IFaqMatcher
{
    Task<FaqMatchResult?> FindBestMatchAsync(
        Guid businessId,
        string messageText,
        CancellationToken cancellationToken = default);
}
