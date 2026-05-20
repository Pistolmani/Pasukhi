using FluentValidation;
using Pasukhi.Application.DTOs.Businesses;

namespace Pasukhi.Application.Validators;

public class UpdateBusinessRequestValidator : AbstractValidator<UpdateBusinessRequest>
{
    public UpdateBusinessRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
        RuleFor(x => x.LogoUrl).MaximumLength(2048).When(x => x.LogoUrl is not null);
    }
}
