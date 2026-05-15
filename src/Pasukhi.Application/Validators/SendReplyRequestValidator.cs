using FluentValidation;
using Pasukhi.Application.DTOs.Conversations;

namespace Pasukhi.Application.Validators;

public class SendReplyRequestValidator : AbstractValidator<SendReplyRequest>
{
    public SendReplyRequestValidator()
    {
        RuleFor(x => x.TextContent)
            .NotEmpty()
            .MaximumLength(2000);
    }
}
