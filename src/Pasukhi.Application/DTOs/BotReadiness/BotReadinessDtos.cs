using Pasukhi.Domain.Enums;

namespace Pasukhi.Application.DTOs.BotReadiness;

// --- Template ---

public record BotReadinessTemplateDto(
    List<BotReadinessSectionDto> Sections);

public record BotReadinessSectionDto(
    string Key,
    string Label,
    string Description,
    List<BotReadinessQuestionDto> Questions);

public record BotReadinessQuestionDto(
    string Key,
    string Label,
    string HelpText,
    string InputType,
    bool Required,
    int Weight);

// --- Report ---

public record BotReadinessReportDto(
    int ReadinessScore,
    int AnsweredWeight,
    int TotalWeight,
    List<BotReadinessAnswerDto> Answers,
    List<BotReadinessGapDto> Gaps,
    List<BotReadinessSectionCompletionDto> SectionCompletion,
    List<BotKnowledgeSuggestionDto> Suggestions);

public record BotReadinessAnswerDto(
    Guid Id,
    Guid BusinessId,
    string QuestionKey,
    string? AnswerText,
    bool IsSkipped,
    DateTime UpdatedAt);

public record BotReadinessGapDto(
    string QuestionKey,
    string Label,
    string SectionKey,
    string SectionLabel,
    int Weight);

public record BotReadinessSectionCompletionDto(
    string SectionKey,
    string Label,
    int AnsweredWeight,
    int TotalWeight,
    int Score);

public record BotKnowledgeSuggestionDto(
    Guid Id,
    Guid BusinessId,
    SuggestionType Type,
    SuggestionStatus Status,
    List<string> SourceQuestionKeys,
    object? Payload,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    DateTime? RejectedAt);

// --- Requests ---

public record SaveBotAnswersRequest(
    List<SaveBotAnswerItem> Answers);

public record SaveBotAnswerItem(
    string QuestionKey,
    string? AnswerText,
    bool IsSkipped);
