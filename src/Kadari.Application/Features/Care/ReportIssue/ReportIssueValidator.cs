using FluentValidation;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;

namespace Kadari.Application.Features.Care.ReportIssue;

internal sealed class ReportIssueValidator : AbstractValidator<ReportIssueCommand>
{
    private static readonly IssueClass[] Reportable = [IssueClass.Fault, IssueClass.AccidentalDamage, IssueClass.DeadOnArrival];

    public ReportIssueValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.IssueClass)
            .Must(s => CareEnum.TryParse<IssueClass>(s, out var v) && Reportable.Contains(v))
            .WithMessage($"IssueClass must be one of: {CareEnum.Names(Reportable)}.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(AssetCase.MaxSummaryLength);
    }
}
