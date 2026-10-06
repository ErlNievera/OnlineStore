using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.DTOs;
using PaymentService.Models;

namespace PaymentService.Controllers;

[ApiController]
[Route("payments/v1/payments")]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentDbContext _db;

    public PaymentsController(PaymentDbContext db)
    {
        _db = db;
    }

    // GET: /payments/v1/payments
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PaymentResponse>>> GetPayments()
    {
        var payments = await _db.Payments
            .AsNoTracking()
            .Select(p => new PaymentResponse
            {
                Id = p.Id,
                OrderId = p.OrderId,
                Amount = p.Amount,
                Status = p.Status,
                PaymentMethod = p.PaymentMethod,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync();

        return Ok(payments);
    }

    // GET: /payments/v1/payments/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentResponse>> GetPayment(Guid id)
    {
        var payment = await _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Payment not found",
                Detail = $"No payment was found with ID '{id}'."
            });
        }

        return Ok(ToResponse(payment));
    }

    // POST: /payments/v1/payments
    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> CreatePayment(
        CreatePaymentRequest request)
    {
        var now = DateTime.UtcNow;

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            Amount = request.Amount,
            Status = "Pending",
            PaymentMethod = request.PaymentMethod,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Payments.Add(payment);

        await _db.SaveChangesAsync();

        var response = ToResponse(payment);

        return CreatedAtAction(
            nameof(GetPayment),
            new { id = payment.Id },
            response);
    }

    // PUT: /payments/v1/payments/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PaymentResponse>> UpdatePayment(
        Guid id,
        UpdatePaymentRequest request)
    {
        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Payment not found",
                Detail = $"No payment was found with ID '{id}'."
            });
        }

        payment.Amount = request.Amount;
        payment.Status = request.Status;
        payment.PaymentMethod = request.PaymentMethod;
        payment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ToResponse(payment));
    }

    // DELETE: /payments/v1/payments/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePayment(Guid id)
    {
        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Payment not found",
                Detail = $"No payment was found with ID '{id}'."
            });
        }

        _db.Payments.Remove(payment);

        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static PaymentResponse ToResponse(Payment payment)
    {
        return new PaymentResponse
        {
            Id = payment.Id,
            OrderId = payment.OrderId,
            Amount = payment.Amount,
            Status = payment.Status,
            PaymentMethod = payment.PaymentMethod,
            CreatedAt = payment.CreatedAt,
            UpdatedAt = payment.UpdatedAt
        };
    }
}
