namespace Procurement.Api.Common;

/// <summary>
/// Illustrative signatory authority-limit banding, reused from docs/kb/business_kb.md Module M3's
/// value-banding text (Department+Procurement below RM500,000; ETC/GPPC from RM500,000;
/// GPPC/EXCO from RM1,000,000; EXCO from RM3,000,000; Board from RM5,000,000) for M9 contract
/// signing. No dedicated signatory/authority-limit registry exists elsewhere in the codebase.
/// </summary>
public static class SignatoryAuthority
{
    public const decimal DefaultLimit = 500_000m;

    private static readonly IReadOnlyDictionary<string, decimal> LimitsBySignatoryId = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
    {
        ["department"] = 500_000m,
        ["etc"] = 1_000_000m,
        ["gppc"] = 1_000_000m,
        ["exco"] = 3_000_000m,
        ["board"] = 5_000_000m,
    };

    public static decimal LimitFor(string signatoryId) =>
        LimitsBySignatoryId.TryGetValue(signatoryId, out var limit) ? limit : DefaultLimit;
}
