using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saas.Agenda.Domain.Entities;

namespace Saas.Agenda.Infrastructure.Persistence.Configurations;

internal sealed class ClinicScheduleConfiguration : IEntityTypeConfiguration<ClinicSchedule>
{
    public void Configure(EntityTypeBuilder<ClinicSchedule> builder)
    {
        builder.ToTable("clinic_schedules");
        builder.HasKey(s => s.TenantId);

        builder.Property(s => s.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(s => s.UpdatedByAccountId).HasColumnName("updated_by_account_id").IsRequired();
        builder.Property(s => s.UpdatedByName).HasColumnName("updated_by_name").HasMaxLength(160).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasMany(s => s.Slots)
            .WithOne()
            .HasForeignKey(slot => slot.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Slots)
            .HasField("_slots")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ClinicScheduleSlotConfiguration : IEntityTypeConfiguration<ClinicScheduleSlot>
{
    public void Configure(EntityTypeBuilder<ClinicScheduleSlot> builder)
    {
        builder.ToTable("clinic_schedule_slots");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(s => s.DayOfWeek).HasColumnName("day_of_week").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(s => s.StartTime).HasColumnName("start_time").HasColumnType("time").IsRequired();
        builder.Property(s => s.EndTime).HasColumnName("end_time").HasColumnType("time").IsRequired();

        builder.HasIndex(s => new { s.TenantId, s.DayOfWeek, s.StartTime })
            .HasDatabaseName("ix_clinic_schedule_slots_tenant_day");
    }
}
