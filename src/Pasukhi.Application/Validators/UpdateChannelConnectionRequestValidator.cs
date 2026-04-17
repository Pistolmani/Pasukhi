using FluentValidation;
using Pasukhi.Application.DTOs.Channels;

namespace Pasukhi.Application.Validators;

public class UpdateChannelConnectionRequestValidator : AbstractValidator<UpdateChannelConnectionRequest>
{
    public UpdateChannelConnectionRequestValidator()
    {
        RuleFor(x => x.ExternalAccountId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ExternalAccountName).MaximumLength(200);
        RuleFor(x => x.AccessToken).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.VerifyToken).NotEmpty().MaximumLength(200);
    }
}
