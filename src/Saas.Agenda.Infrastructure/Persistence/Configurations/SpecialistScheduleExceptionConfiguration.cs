using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saas.Agenda.Domain.Entities;

namespace Saas.Agenda.Infrastructure.Persistence.Configurations;

internal sealed class SpecialistScheduleExceptionConfiguration : IEntityTypeConfiguration<SpecialistScheduleException>
{
    public void Configure(EntityTypeBuilder<SpecialistScheduleException> builder)
    {
        builder.ToTable("specialist_schedule_exceptions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.SpecialistId).HasColumnName("specialist_id").IsRequired();
        builder.Property(e => e.DateGmt6).HasColumnName("date_gmt6").HasColumnType("date").IsRequired();
        builder.Property(e => e.IsUnavailable).HasColumnName("is_unavailable").IsRequired();
        builder.Property(e => e.CustomStartTime).HasColumnName("custom_start_time").HasColumnType("time");
        builder.Property(e => e.CustomEndTime).HasColumnName("custom_end_time").HasColumnType("time");
        builder.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(300).IsRequired();
        builder.Property(e => e.CreatedByAccountId).HasColumnName("created_by_account_id").IsRequired();
        builder.Property(e => e.CreatedByName).HasColumnName("created_by_name").HasMaxLength(160).IsRequired();
        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.SpecialistId, e.DateGmt6 })
            .IsUnique()
            .HasDatabaseName("ux_specialist_schedule_exceptions_tenant_spec_date");
    }
}
