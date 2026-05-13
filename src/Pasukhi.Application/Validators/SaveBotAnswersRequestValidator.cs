using FluentValidation;
using Pasukhi.Application.DTOs.BotReadiness;

namespace Pasukhi.Application.Validators;

public class SaveBotAnswersRequestValidator : AbstractValidator<SaveBotAnswersRequest>
{
    public SaveBotAnswersRequestValidator()
    {
        RuleFor(x => x.Answers).NotEmpty();
        RuleForEach(x => x.Answers).ChildRules(item =>
        {
            item.RuleFor(a => a.QuestionKey).NotEmpty().MaximumLength(120);
            item.RuleFor(a => a.AnswerText).MaximumLength(5000);
        });
    }
}
