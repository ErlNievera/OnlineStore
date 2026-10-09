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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PaymentResponse>>> GetPayments()
    {
        List<PaymentResponse> payments = await _db.Payments
            .AsNoTracking()
            .Select(payment => new PaymentResponse
            {
                Id = payment.Id,
                OrderId = payment.OrderId,
                Amount = payment.Amount,
                Status = payment.Status,
                PaymentMethod = payment.PaymentMethod,
                CreatedAt = payment.CreatedAt,
                UpdatedAt = payment.UpdatedAt
            })
            .ToListAsync();

        return Ok(payments);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentResponse>> GetPayment(Guid id)
    {
        Payment? payment = await _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id);

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

    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> CreatePayment(
        CreatePaymentRequest request)
    {
        if (request.OrderId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid order",
                Detail = "A valid OrderId is required."
            });
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid amount",
                Detail = "Payment amount must be greater than zero."
            });
        }

        // Demo simulation only. This does not charge a real payment method.
        DateTime now = DateTime.UtcNow;

        Payment payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            Amount = request.Amount,
            Status = request.SimulateFailure ? "Failed" : "Succeeded",
            PaymentMethod = request.PaymentMethod,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        PaymentResponse response = ToResponse(payment);

        return CreatedAtAction(
            nameof(GetPayment),
            new { id = payment.Id },
            response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PaymentResponse>> UpdatePayment(
        Guid id,
        UpdatePaymentRequest request)
    {
        Payment? payment = await _db.Payments
            .FirstOrDefaultAsync(item => item.Id == id);

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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePayment(Guid id)
    {
        Payment? payment = await _db.Payments
            .FirstOrDefaultAsync(item => item.Id == id);

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
