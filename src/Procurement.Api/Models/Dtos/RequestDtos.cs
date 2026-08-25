namespace Procurement.Api.Models.Dtos;

public sealed class CreateRequestRequest
{
    public string RequesterId { get; set; } = string.Empty;
    public string RequesterRole { get; set; } = string.Empty;
    public string BusinessUnit { get; set; } = string.Empty;
    public string CostCentre { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal EstimatedValue { get; set; }
    public string Currency { get; set; } = "MYR";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Country { get; set; } = "Malaysia";
    public string Entity { get; set; } = string.Empty;
    public DateOnly DeliveryDate { get; set; }
    public string Criticality { get; set; } = "Standard";
    public string EngagementPathway { get; set; } = string.Empty;
    public string ProcurementNature { get; set; } = "New Procurement";
    public string? PreviousContractId { get; set; }
    public bool IsNonCatalogue { get; set; }
}

public sealed class UpdateRequestRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Department { get; set; }
    public decimal? EstimatedValue { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public string? Criticality { get; set; }
}

public sealed class SubmitRequestRequest
{
    public bool NoConflict { get; set; }
    public bool ConnectedPartyDeclared { get; set; }
    public bool NoSplittingDeclaration { get; set; }
    public bool CompleteDeclaration { get; set; }
}

public sealed record RequestResponse(
    Guid Id,
    string RequestId,
    string RequesterId,
    string Category,
    decimal EstimatedValue,
    string Currency,
    string Title,
    string Status,
    string RoutingDestination,
    bool PotentialDuplicateOrSplit)
{
    public static RequestResponse From(Request request) => new(
        request.Id, request.RequestId, request.RequesterId, request.Category, request.EstimatedValue,
        request.Currency, request.Title, request.Status.ToString(), request.RoutingDestination.ToString(),
        request.PotentialDuplicateOrSplit);
}

public sealed record AttachmentResponse(Guid Id, string FileName, long SizeBytes, string DocumentType)
{
    public static AttachmentResponse From(RequestAttachment attachment) =>
        new(attachment.Id, attachment.FileName, attachment.SizeBytes, attachment.DocumentType);
}

public sealed record CatalogueItemResponse(string Name, string Category, string Supplier);
