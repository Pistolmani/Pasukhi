using FluentValidation;
using Pasukhi.Application.DTOs.Channels;

namespace Pasukhi.Application.Validators;

public class CreateChannelConnectionRequestValidator : AbstractValidator<CreateChannelConnectionRequest>
{
    public CreateChannelConnectionRequestValidator()
    {
        RuleFor(x => x.ChannelType).IsInEnum();
        RuleFor(x => x.ExternalAccountId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ExternalAccountName).MaximumLength(200);
        RuleFor(x => x.AccessToken).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.VerifyToken).MaximumLength(200);
    }
}
