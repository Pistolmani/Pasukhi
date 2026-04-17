using FluentValidation;
using Pasukhi.Application.DTOs.Businesses;

namespace Pasukhi.Application.Validators;

public class CreateBusinessRequestValidator : AbstractValidator<CreateBusinessRequest>
{
    public CreateBusinessRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(200)
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Slug must be lowercase letters, numbers, and hyphens only.");
    }
}
