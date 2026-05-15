using FluentValidation;
using Pasukhi.Application.DTOs.Auth;

namespace Pasukhi.Application.Validators;

public class SetupBusinessRequestValidator : AbstractValidator<SetupBusinessRequest>
{
    public SetupBusinessRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description is not null);
    }
}
