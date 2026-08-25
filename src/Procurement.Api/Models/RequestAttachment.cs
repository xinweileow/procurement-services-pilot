namespace Procurement.Api.Models;

/// <summary>docs/kb/technical_kb.md Module M1, Entity: RequestAttachment.</summary>
public sealed class RequestAttachment
{
    public const long MaxSizeBytes = 20 * 1024 * 1024; // 20 MB, per docs/kb/business_kb.md Module M1

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public string DocumentType { get; set; } = string.Empty; // businessCase | scope | otherRequest | existingContract | ...
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
