using Microsoft.EntityFrameworkCore;
using Saas.Agenda.Domain.Entities;

namespace Saas.Agenda.Infrastructure.Persistence;

public sealed class AgendaDbContext(DbContextOptions<AgendaDbContext> options)
    : DbContext(options)
{
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentHistoryEntry> AppointmentHistory => Set<AppointmentHistoryEntry>();
    public DbSet<ClinicSchedule> ClinicSchedules => Set<ClinicSchedule>();
    public DbSet<ClinicScheduleSlot> ClinicScheduleSlots => Set<ClinicScheduleSlot>();
    public DbSet<SpecialistSchedule> SpecialistSchedules => Set<SpecialistSchedule>();
    public DbSet<SpecialistScheduleSlot> SpecialistScheduleSlots => Set<SpecialistScheduleSlot>();
    public DbSet<SpecialistScheduleException> SpecialistScheduleExceptions => Set<SpecialistScheduleException>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("agenda");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgendaDbContext).Assembly);
    }
}
