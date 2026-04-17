namespace Pasukhi.Domain.Enums;

public enum EscalationReason
{
    NoMatch = 0,
    LowAiConfidence = 1,
    SafetyCheckFailed = 2,
    CustomerRequested = 3,
    OperatorTriggered = 4
}
