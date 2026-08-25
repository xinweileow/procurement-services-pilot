namespace Procurement.Api.Common;

/// <summary>Standard pagination envelope (docs/kb/technical_kb.md Conventions).</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
