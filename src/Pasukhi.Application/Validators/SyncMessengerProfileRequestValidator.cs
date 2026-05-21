using FluentValidation;
using Pasukhi.Application.DTOs.Channels;

namespace Pasukhi.Application.Validators;

public class SyncMessengerProfileRequestValidator : AbstractValidator<SyncMessengerProfileRequest>
{
    public SyncMessengerProfileRequestValidator()
    {
        RuleFor(x => x.GreetingText)
            .MaximumLength(160)
            .When(x => x.GreetingText is not null);

        RuleFor(x => x.MaxIceBreakers)
            .InclusiveBetween(1, 4);
    }
}
