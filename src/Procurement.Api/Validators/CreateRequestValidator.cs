using FluentValidation;
using Procurement.Api.Common;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Validators;

/// <summary>
/// docs/kb/business_kb.md Module M1: title, business justification, category, department,
/// delivery date and budget reference (budget reference itself belongs to Module M2 and is
/// captured separately) are all mandatory; category must be in the requester's role-allowed set.
/// </summary>
public sealed class CreateRequestValidator : AbstractValidator<CreateRequestRequest>
{
    public CreateRequestValidator()
    {
        RuleFor(x => x.RequesterId).NotEmpty();
        RuleFor(x => x.RequesterRole).NotEmpty();
        RuleFor(x => x.Title).NotEmpty();
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.Department).NotEmpty();
        RuleFor(x => x.DeliveryDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Category)
            .NotEmpty()
            .Must((request, category) => RoleCategories.IsCategoryAllowed(request.RequesterRole, category))
            .WithMessage(x => $"Category '{x.Category}' is not allowed for role '{x.RequesterRole}'.");
    }
}
