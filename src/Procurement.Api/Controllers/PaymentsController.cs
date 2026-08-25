using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
public sealed class PaymentsController(ProcurementDbContext db) : ControllerBase
{
    private const decimal ApprovedLimit = 5_000_000m;

    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> Create([FromBody] CreatePaymentRequest body, CancellationToken ct)
    {
        var validRoutes = new[] { "PO", "direct", "TT" };
        if (!validRoutes.Contains(body.Route) || body.PayeeId == Guid.Empty)
        {
            throw new UnprocessableException("route (PO|direct|TT) and payeeId are required.");
        }

        var payment = new Payment
        {
            Reference = $"PV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}",
            EpvVoucherId = body.EpvVoucherId,
            Route = body.Route,
            PayeeId = body.PayeeId,
            BankDetails = body.BankDetails,
            Amount = body.Amount,
            Currency = body.Currency,
            PaymentDate = body.PaymentDate,
            PreparerId = body.PreparerId,
            Category = body.Category,
            Status = "draft",
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);

        return Ok(new PaymentResponse(payment.Id, payment.Reference, payment.Status));
    }

    [HttpPost("{id:guid}/tax-treatment")]
    public async Task<ActionResult<TaxTreatmentResponse>> TaxTreatment(
        Guid id, [FromBody] TaxTreatmentRequest body, CancellationToken ct)
    {
        var payment = await db.Payments.FindAsync([id], ct)
            ?? throw new NotFoundException($"Payment {id} not found");

        payment.TaxCode = body.TaxCode;
        payment.WhtAmount = body.WhtAmount ?? 0m;
        payment.SstAmount = body.SstAmount ?? 0m;
        payment.TaxAmount = payment.WhtAmount + payment.SstAmount;
        payment.PayableAmount = payment.Amount - payment.WhtAmount.Value + payment.SstAmount.Value;

        await db.SaveChangesAsync(ct);

        return Ok(new TaxTreatmentResponse(payment.Id, payment.PayableAmount.Value, payment.TaxAmount.Value));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ApprovePaymentResponse>> Approve(
        Guid id, [FromBody] ApprovePaymentRequest body, CancellationToken ct)
    {
        var payment = await db.Payments.FindAsync([id], ct)
            ?? throw new NotFoundException($"Payment {id} not found");

        if (!string.IsNullOrWhiteSpace(payment.PreparerId) &&
            string.Equals(payment.PreparerId, body.ApproverId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnprocessableException(
                $"Approver '{body.ApproverId}' has a segregation-of-duties conflict: they also prepared this payment.");
        }

        payment.ApproverId = body.ApproverId;
        payment.Status = "approved";

        await db.SaveChangesAsync(ct);

        return Ok(new ApprovePaymentResponse(payment.Id, payment.Status));
    }

    [HttpPost("{id:guid}/controls-check")]
    public async Task<ActionResult<PaymentControlsCheckResponse>> ControlsCheck(Guid id, CancellationToken ct)
    {
        var payment = await db.Payments.FindAsync([id], ct)
            ?? throw new NotFoundException($"Payment {id} not found");

        var beneficiaryVerified = !string.IsNullOrWhiteSpace(payment.BankDetails);

        var duplicateFlag = await db.Payments.AnyAsync(p =>
            p.Id != payment.Id &&
            p.PayeeId == payment.PayeeId &&
            p.Amount == payment.Amount &&
            p.PaymentDate == payment.PaymentDate &&
            p.Status != "failed", ct);

        var withinLimit = payment.Amount <= ApprovedLimit;

        if (!beneficiaryVerified || duplicateFlag || !withinLimit)
        {
            throw new ConflictException(
                "Payment controls failed: " +
                (!beneficiaryVerified ? "beneficiary/bank account not verified. " : string.Empty) +
                (duplicateFlag ? "duplicate payment detected. " : string.Empty) +
                (!withinLimit ? "amount exceeds the approved limit." : string.Empty));
        }

        return Ok(new PaymentControlsCheckResponse(beneficiaryVerified, duplicateFlag, withinLimit));
    }

    [HttpPost("{id:guid}/execute")]
    public async Task<ActionResult<ExecutePaymentResponse>> Execute(Guid id, CancellationToken ct)
    {
        var payment = await db.Payments.FindAsync([id], ct)
            ?? throw new NotFoundException($"Payment {id} not found");

        if (!string.Equals(payment.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException($"Payment {id} is not yet approved (status: {payment.Status}); cannot execute.");
        }

        payment.Status = "executed";
        payment.BankReference = $"BANKREF-{Guid.NewGuid().ToString()[..10].ToUpperInvariant()}";

        await db.SaveChangesAsync(ct);

        return Ok(new ExecutePaymentResponse(payment.Id, payment.Status, payment.BankReference));
    }

    [HttpGet("{id:guid}/self-billed-applicability")]
    public async Task<ActionResult<SelfBilledApplicabilityResponse>> SelfBilledApplicability(Guid id, CancellationToken ct)
    {
        var payment = await db.Payments.FindAsync([id], ct)
            ?? throw new NotFoundException($"Payment {id} not found");

        return Ok(new SelfBilledApplicabilityResponse(IsSelfBilledApplicable(payment)));
    }

    [HttpPost("{id:guid}/self-billed-submission")]
    public async Task<ActionResult<SelfBilledSubmissionResponse>> SelfBilledSubmission(Guid id, CancellationToken ct)
    {
        var payment = await db.Payments.FindAsync([id], ct)
            ?? throw new NotFoundException($"Payment {id} not found");

        if (!IsSelfBilledApplicable(payment))
        {
            throw new UnprocessableException($"Payment {id} is not applicable for self-billed e-Invoice submission.");
        }

        var record = await db.SelfBilledRecords.FirstOrDefaultAsync(r => r.PaymentId == payment.Id, ct);
        if (record is null)
        {
            record = new SelfBilledRecord { PaymentId = payment.Id };
            db.SelfBilledRecords.Add(record);
        }

        record.Status = "pending";
        record.MyInvoisPayloadVersion = "1.0";
        record.SubmissionDate = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new SelfBilledSubmissionResponse(record.Id, record.MyInvoisPayloadVersion, record.SubmissionDate.Value));
    }

    private static bool IsSelfBilledApplicable(Payment payment) =>
        !string.Equals(payment.Category, "Non-LHDN", StringComparison.OrdinalIgnoreCase);
}
