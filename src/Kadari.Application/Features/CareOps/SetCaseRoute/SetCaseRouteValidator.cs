using FluentValidation;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;

namespace Kadari.Application.Features.CareOps.SetCaseRoute;

internal sealed class SetCaseRouteValidator : AbstractValidator<SetCaseRouteCommand>
{
    private static readonly RouteType[] Routes = Enum.GetValues<RouteType>().Where(r => r != RouteType.ResearchRequired).ToArray();

    public SetCaseRouteValidator()
    {
        RuleFor(x => x.WorkItemId).NotEmpty();
        RuleFor(x => x.RouteType)
            .Must(r => CareEnum.TryParse<RouteType>(r, out var v) && Routes.Contains(v))
            .WithMessage($"RouteType must be one of: {CareEnum.Names(Routes)}.");
        RuleFor(x => x.ChannelType)
            .Must(c => CareEnum.IsName<ContactChannelType>(c))
            .WithMessage($"ChannelType must be one of: {CareEnum.Names<ContactChannelType>()}.");
        RuleFor(x => x.ChannelValue)
            .NotEmpty().When(x => !string.Equals(x.ChannelType, nameof(ContactChannelType.InStore), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Say how to reach them.");
        RuleFor(x => x.ChannelValue)
            .Must(v => Uri.TryCreate(v, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps)
            .When(x => string.Equals(x.ChannelType, nameof(ContactChannelType.WebPage), StringComparison.OrdinalIgnoreCase))
            .WithMessage("A web page must be an https address.");
        RuleFor(x => x.TargetName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Instructions).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Resolution)
            .Must(r => CareEnum.IsName<OpsResolution>(r))
            .WithMessage($"Resolution must be one of: {CareEnum.Names<OpsResolution>()}.");
        RuleFor(x => x.Notes).NotEmpty().MaximumLength(OpsWorkItem.MaxNotesLength);
    }
}
