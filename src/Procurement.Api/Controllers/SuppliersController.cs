using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1/suppliers")]
public sealed class SuppliersController(ProcurementDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SupplierResponse>> Create([FromBody] CreateSupplierRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.LegalEntityName) ||
            string.IsNullOrWhiteSpace(body.RegistrationNumber) ||
            string.IsNullOrWhiteSpace(body.TaxId) ||
            string.IsNullOrWhiteSpace(body.Address))
        {
            throw new UnprocessableException("LegalEntityName, RegistrationNumber, TaxId, and Address are required.");
        }

        var supplier = new Supplier
        {
            LegalEntityName = body.LegalEntityName.Trim(),
            RegistrationNumber = body.RegistrationNumber.Trim(),
            TaxId = body.TaxId.Trim(),
            Address = body.Address.Trim(),
            ContactEmail = body.ContactEmail?.Trim(),
            ContactPhone = body.ContactPhone?.Trim(),
            BeneficiaryName = body.BeneficiaryName?.Trim(),
            BankName = body.BankName?.Trim(),
            AccountNumber = body.AccountNumber?.Trim(),
            PaymentTerms = body.PaymentTerms ?? "Net 30",
            CategoryCodes = body.CategoryCodes ?? "IT,General",
            ActiveFlag = true,
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);

        return Ok(new SupplierResponse(
            supplier.Id, supplier.LegalEntityName, supplier.RegistrationNumber, supplier.TaxId, supplier.Address, "pending", supplier.ActiveFlag));
    }

    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult<SupplierStatusResponse>> GetStatus(Guid id, CancellationToken ct)
    {
        var supplier = await db.Suppliers.FindAsync([id], ct)
            ?? throw new NotFoundException($"Supplier {id} not found");

        return Ok(new SupplierStatusResponse(
            supplier.Id,
            supplier.ActiveFlag,
            "Valid",
            supplier.EsgScore,
            "Completed",
            "Non-Material"));
    }

    [HttpPost("duplicate-check")]
    public async Task<ActionResult<DuplicateCheckResponse>> DuplicateCheck(
        [FromBody] DuplicateCheckRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.LegalEntityName))
        {
            throw new UnprocessableException("LegalEntityName is required for duplicate check.");
        }

        var query = db.Suppliers.AsNoTracking().AsQueryable();
        var searchName = body.LegalEntityName.Trim().ToLowerInvariant();
        var searchReg = body.RegistrationNumber?.Trim().ToLowerInvariant();
        var searchTax = body.TaxId?.Trim().ToLowerInvariant();
        var searchBank = body.BankAccountNumber?.Trim().ToLowerInvariant();
        var searchEmail = body.ContactEmail?.Trim().ToLowerInvariant();

        var matches = await query
            .Where(s =>
                s.LegalEntityName.ToLower().Contains(searchName) ||
                (!string.IsNullOrEmpty(searchReg) && s.RegistrationNumber.ToLower() == searchReg) ||
                (!string.IsNullOrEmpty(searchTax) && s.TaxId.ToLower() == searchTax) ||
                (!string.IsNullOrEmpty(searchBank) && s.AccountNumber != null && s.AccountNumber.ToLower() == searchBank) ||
                (!string.IsNullOrEmpty(searchEmail) && s.ContactEmail != null && s.ContactEmail.ToLower() == searchEmail))
            .ToListAsync(ct);

        var candidates = matches.Select(s =>
        {
            var reasons = new List<string>();
            if (s.LegalEntityName.ToLower().Contains(searchName)) reasons.Add("Name match");
            if (!string.IsNullOrEmpty(searchReg) && s.RegistrationNumber.ToLower() == searchReg) reasons.Add("Registration number match");
            if (!string.IsNullOrEmpty(searchTax) && s.TaxId.ToLower() == searchTax) reasons.Add("Tax ID match");
            if (!string.IsNullOrEmpty(searchBank) && s.AccountNumber != null && s.AccountNumber.ToLower() == searchBank) reasons.Add("Bank account match");
            if (!string.IsNullOrEmpty(searchEmail) && s.ContactEmail != null && s.ContactEmail.ToLower() == searchEmail) reasons.Add("Contact email match");

            return new SupplierCandidateDto(
                s.Id, s.LegalEntityName, s.RegistrationNumber, s.TaxId, s.ActiveFlag, string.Join(", ", reasons));
        }).ToList();

        return Ok(new DuplicateCheckResponse(candidates.Count > 0, candidates));
    }

    [HttpPatch("{id:guid}/bank-details")]
    public async Task<ActionResult<BankDetailsResponse>> UpdateBankDetails(
        Guid id, [FromBody] UpdateBankDetailsRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.BeneficiaryName) ||
            string.IsNullOrWhiteSpace(body.BankName) ||
            string.IsNullOrWhiteSpace(body.AccountNumber))
        {
            throw new UnprocessableException("BeneficiaryName, BankName, and AccountNumber are required.");
        }

        var supplier = await db.Suppliers.FindAsync([id], ct)
            ?? throw new NotFoundException($"Supplier {id} not found");

        supplier.BeneficiaryName = body.BeneficiaryName.Trim();
        supplier.BankName = body.BankName.Trim();
        supplier.AccountNumber = body.AccountNumber.Trim();
        supplier.BankVerified = false;
        supplier.BankStatus = "pending_verification";

        await db.SaveChangesAsync(ct);

        return Ok(new BankDetailsResponse(
            supplier.Id,
            "pending_verification",
            "Bank details updated and set to pending verification. Authorised Finance review is required."));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<SupplierResponse>> UpdateProfile(
        Guid id, [FromBody] UpdateSupplierProfileRequest body, CancellationToken ct)
    {
        var supplier = await db.Suppliers.FindAsync([id], ct)
            ?? throw new NotFoundException($"Supplier {id} not found");

        if (body.Address != null) supplier.Address = body.Address.Trim();
        if (body.ContactEmail != null) supplier.ContactEmail = body.ContactEmail.Trim();
        if (body.ContactPhone != null) supplier.ContactPhone = body.ContactPhone.Trim();
        if (body.PaymentTerms != null) supplier.PaymentTerms = body.PaymentTerms.Trim();
        if (body.CategoryCodes != null) supplier.CategoryCodes = body.CategoryCodes.Trim();

        await db.SaveChangesAsync(ct);

        return Ok(new SupplierResponse(
            supplier.Id, supplier.LegalEntityName, supplier.RegistrationNumber, supplier.TaxId, supplier.Address, "active", supplier.ActiveFlag));
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<SupplierResponse>> Deactivate(Guid id, CancellationToken ct)
    {
        var supplier = await db.Suppliers.FindAsync([id], ct)
            ?? throw new NotFoundException($"Supplier {id} not found");

        supplier.ActiveFlag = false;
        await db.SaveChangesAsync(ct);

        return Ok(new SupplierResponse(
            supplier.Id, supplier.LegalEntityName, supplier.RegistrationNumber, supplier.TaxId, supplier.Address, "deactivated", false));
    }

    [HttpPost("{id:guid}/requalify")]
    public async Task<ActionResult<SupplierResponse>> Requalify(Guid id, CancellationToken ct)
    {
        var supplier = await db.Suppliers.FindAsync([id], ct)
            ?? throw new NotFoundException($"Supplier {id} not found");

        supplier.RequalificationDueUtc = DateTime.UtcNow.AddMonths(12);
        await db.SaveChangesAsync(ct);

        return Ok(new SupplierResponse(
            supplier.Id, supplier.LegalEntityName, supplier.RegistrationNumber, supplier.TaxId, supplier.Address, "requalified", supplier.ActiveFlag));
    }

    [HttpGet("expiring-documents")]
    public async Task<ActionResult<IReadOnlyList<ExpiringDocumentDto>>> GetExpiringDocuments(
        [FromQuery] int windowDays = 30, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(windowDays);
        var docs = await db.SupplierDocuments
            .Where(d => d.ExpiryDateUtc <= cutoff)
            .ToListAsync(ct);

        var supplierIds = docs.Select(d => d.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);

        var result = docs.Select(d =>
        {
            var sName = suppliers.TryGetValue(d.SupplierId, out var s) ? s.LegalEntityName : "Unknown Supplier";
            var days = (int)Math.Ceiling((d.ExpiryDateUtc - DateTime.UtcNow).TotalDays);
            return new ExpiringDocumentDto(d.SupplierId, sName, d.DocumentType, d.FileName, d.ExpiryDateUtc, days);
        }).ToList();

        return Ok(result);
    }
}
