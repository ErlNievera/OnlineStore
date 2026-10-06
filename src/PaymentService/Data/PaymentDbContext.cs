using Microsoft.EntityFrameworkCore;
using PaymentService.Models;

namespace PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.OrderId)
                .IsRequired();

            entity.Property(p => p.Amount)
                .IsRequired();

            entity.Property(p => p.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(p => p.PaymentMethod)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(p => p.CreatedAt)
                .IsRequired();

            entity.Property(p => p.UpdatedAt)
                .IsRequired();
        });
    }
}
