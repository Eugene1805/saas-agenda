using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saas.Agenda.Domain.Entities;

namespace Saas.Agenda.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(a => a.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(a => a.SpecialistId).HasColumnName("specialist_id").IsRequired();
        builder.Property(a => a.ScheduledStartUtc).HasColumnName("scheduled_start_utc").IsRequired();
        builder.Property(a => a.ScheduledEndUtc).HasColumnName("scheduled_end_utc").IsRequired();
        builder.Property(a => a.DurationMinutes).HasColumnName("duration_minutes").IsRequired();
        builder.Property(a => a.Reason).HasColumnName("reason").HasMaxLength(300).IsRequired();
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.CreatedByAccountId).HasColumnName("created_by_account_id").IsRequired();
        builder.Property(a => a.CreatedByName).HasColumnName("created_by_name").HasMaxLength(160).IsRequired();
        builder.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(a => a.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasMany(a => a.History)
            .WithOne()
            .HasForeignKey(h => h.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(a => a.History)
            .HasField("_history")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(a => new { a.TenantId, a.ScheduledStartUtc })
            .HasDatabaseName("ix_appointments_tenant_start");

        builder.HasIndex(a => new { a.TenantId, a.SpecialistId, a.ScheduledStartUtc })
            .HasDatabaseName("ix_appointments_tenant_specialist_start");

        builder.HasIndex(a => new { a.TenantId, a.PatientId })
            .HasDatabaseName("ix_appointments_tenant_patient");
    }
}
