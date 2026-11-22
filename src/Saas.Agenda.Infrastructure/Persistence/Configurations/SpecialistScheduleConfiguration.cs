using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saas.Agenda.Domain.Entities;

namespace Saas.Agenda.Infrastructure.Persistence.Configurations;

internal sealed class SpecialistScheduleConfiguration : IEntityTypeConfiguration<SpecialistSchedule>
{
    public void Configure(EntityTypeBuilder<SpecialistSchedule> builder)
    {
        builder.ToTable("specialist_schedules");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(s => s.SpecialistId).HasColumnName("specialist_id").IsRequired();
        builder.Property(s => s.UseClinicFallback).HasColumnName("use_clinic_fallback").IsRequired();
        builder.Property(s => s.UpdatedByAccountId).HasColumnName("updated_by_account_id").IsRequired();
        builder.Property(s => s.UpdatedByName).HasColumnName("updated_by_name").HasMaxLength(160).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasIndex(s => new { s.TenantId, s.SpecialistId })
            .IsUnique()
            .HasDatabaseName("ux_specialist_schedules_tenant_spec");

        builder.HasMany(s => s.Slots)
            .WithOne()
            .HasForeignKey(slot => slot.SpecialistScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Slots)
            .HasField("_slots")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SpecialistScheduleSlotConfiguration : IEntityTypeConfiguration<SpecialistScheduleSlot>
{
    public void Configure(EntityTypeBuilder<SpecialistScheduleSlot> builder)
    {
        builder.ToTable("specialist_schedule_slots");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.SpecialistScheduleId).HasColumnName("specialist_schedule_id").IsRequired();
        builder.Property(s => s.DayOfWeek).HasColumnName("day_of_week").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(s => s.StartTime).HasColumnName("start_time").HasColumnType("time").IsRequired();
        builder.Property(s => s.EndTime).HasColumnName("end_time").HasColumnType("time").IsRequired();

        builder.HasIndex(s => new { s.SpecialistScheduleId, s.DayOfWeek, s.StartTime })
            .HasDatabaseName("ix_specialist_schedule_slots_parent_day");
    }
}
