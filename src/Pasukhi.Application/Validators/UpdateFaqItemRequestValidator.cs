using FluentValidation;
using Pasukhi.Application.DTOs.Faqs;

namespace Pasukhi.Application.Validators;

public class UpdateFaqItemRequestValidator : AbstractValidator<UpdateFaqItemRequest>
{
    public UpdateFaqItemRequestValidator()
    {
        RuleFor(x => x.Question).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Answer).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Keywords).MaximumLength(1000);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
