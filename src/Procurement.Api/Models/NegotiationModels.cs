namespace Procurement.Api.Models;

/// <summary>docs/kb/technical_kb.md Module M8, Entity: NegotiationRound.</summary>
public sealed class NegotiationRound
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RfxEventId { get; set; }
    public Guid SupplierId { get; set; }
    public int RoundNumber { get; set; }
    public decimal RevisedPrice { get; set; }
    public string? Terms { get; set; }
    public string RoundType { get; set; } = "negotiation"; // negotiation | eAuction | bafo
    public string Status { get; set; } = "recorded"; // recorded | requested
    public DateTime? Deadline { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M8, Entity: SavingsRecord.</summary>
public sealed class SavingsRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RfxEventId { get; set; }
    public decimal BaselineValue { get; set; }
    public decimal AwardedValue { get; set; }
    public decimal SavingsAmount => BaselineValue - AwardedValue;
    public string SavingsType { get; set; } = "hard"; // hard | soft
    public bool FinanceValidated { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
