namespace Procurement.Api.Common;

/// <summary>
/// Per-role allowed procurement category list, from docs/kb/business_kb.md Module M1
/// (the eProcurement prototype's role selector). Distinct from the fuller CTO/CFO/SMC/Board
/// approval role model tracked separately for Module M3 (DECISION-M3-1) — this one only
/// governs which categories a requestor role may pick at intake.
/// </summary>
public static class RoleCategories
{
    public const string ItBusinessRequestor = "IT Business Requestor";
    public const string NonItBusinessRequestor = "Non-IT Business Requestor";
    public const string ProcurementAdministrator = "Procurement Administrator";

    private static readonly IReadOnlyDictionary<string, string[]> AllowedByRole = new Dictionary<string, string[]>
    {
        [ItBusinessRequestor] = ["IT and Telecommunication"],
        [NonItBusinessRequestor] = ["Facilities Management", "Sales and Marketing", "General Spend", "Professional Services"],
        [ProcurementAdministrator] =
        [
            "Banking Operations", "Facilities Management", "General Spend",
            "IT and Telecommunication", "Sales and Marketing", "Professional Services",
        ],
    };

    public static IReadOnlyList<string> AllowedCategoriesFor(string role) =>
        AllowedByRole.TryGetValue(role, out var categories) ? categories : [];

    public static bool IsCategoryAllowed(string role, string category) =>
        AllowedCategoriesFor(role).Contains(category);
}
