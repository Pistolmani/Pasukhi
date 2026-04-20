namespace Pasukhi.Application.Interfaces;

public record AiSafetyResult(bool Passed, string? RejectionReason);

public interface IAiSafetyChecker
{
    Task<AiSafetyResult> ValidateAsync(
        AiContext context,
        AiReplyResult result,
        CancellationToken ct = default);
}
