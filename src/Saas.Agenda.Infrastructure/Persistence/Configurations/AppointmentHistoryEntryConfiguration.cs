using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saas.Agenda.Domain.Entities;

namespace Saas.Agenda.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentHistoryEntryConfiguration : IEntityTypeConfiguration<AppointmentHistoryEntry>
{
    public void Configure(EntityTypeBuilder<AppointmentHistoryEntry> builder)
    {
        builder.ToTable("appointment_history");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(h => h.AppointmentId).HasColumnName("appointment_id").IsRequired();
        builder.Property(h => h.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(h => h.EventType).HasColumnName("event_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(h => h.FromStatus).HasColumnName("from_status").HasConversion<string>().HasMaxLength(32);
        builder.Property(h => h.ToStatus).HasColumnName("to_status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(h => h.PreviousStartUtc).HasColumnName("previous_start_utc");
        builder.Property(h => h.NewStartUtc).HasColumnName("new_start_utc");
        builder.Property(h => h.PreviousDurationMinutes).HasColumnName("previous_duration_minutes");
        builder.Property(h => h.NewDurationMinutes).HasColumnName("new_duration_minutes");
        builder.Property(h => h.ActorAccountId).HasColumnName("actor_account_id").IsRequired();
        builder.Property(h => h.ActorName).HasColumnName("actor_name").HasMaxLength(160).IsRequired();
        builder.Property(h => h.Note).HasColumnName("note").HasMaxLength(500);
        builder.Property(h => h.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();

        builder.HasIndex(h => new { h.AppointmentId, h.OccurredAtUtc })
            .HasDatabaseName("ix_appointment_history_apt_occurred");
    }
}
