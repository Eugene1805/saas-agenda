using Saas.Agenda.Domain.Common;
using Saas.Agenda.Domain.Entities;
using Saas.Agenda.Domain.Policies;
using Saas.Agenda.Domain.ValueObjects;

namespace Saas.Agenda.Application.UseCases.Appointments;

public sealed record AppointmentDto(
    Guid Id,
    Guid TenantId,
    Guid PatientId,
    Guid SpecialistId,
    string ScheduledStartGmt6,
    string ScheduledEndGmt6,
    int DurationMinutes,
    string Reason,
    string Status,
    IReadOnlyList<string> AllowedNextStatuses,
    Guid CreatedByAccountId,
    string CreatedByName,
    string CreatedAtGmt6,
    string UpdatedAtGmt6);

public sealed record AppointmentHistoryItemDto(
    Guid Id,
    string EventType,
    string? FromStatus,
    string ToStatus,
    string? PreviousStartGmt6,
    string? NewStartGmt6,
    int? PreviousDurationMinutes,
    int? NewDurationMinutes,
    Guid ActorAccountId,
    string ActorName,
    string? Note,
    string OccurredAtGmt6);

public sealed record AppointmentDetailDto(
    Guid Id,
    Guid TenantId,
    Guid PatientId,
    Guid SpecialistId,
    string ScheduledStartGmt6,
    string ScheduledEndGmt6,
    int DurationMinutes,
    string Reason,
    string Status,
    IReadOnlyList<string> AllowedNextStatuses,
    Guid CreatedByAccountId,
    string CreatedByName,
    string CreatedAtGmt6,
    string UpdatedAtGmt6,
    IReadOnlyList<AppointmentHistoryItemDto> History);

public sealed record AppointmentListResponse(
    int Total,
    IReadOnlyList<AppointmentDto> Items);

public sealed record ListAppointmentsQuery(
    AgendaActorContext Actor,
    Guid? SpecialistId = null,
    Guid? PatientId = null,
    string? Status = null,
    string? FromDateGmt6 = null,
    string? ToDateGmt6 = null);

public sealed record CreateAppointmentCommand(
    AgendaActorContext Actor,
    Guid PatientId,
    Guid SpecialistId,
    string ScheduledStartGmt6,
    int DurationMinutes,
    string Reason);

public sealed record RescheduleAppointmentCommand(
    AgendaActorContext Actor,
    Guid AppointmentId,
    string ScheduledStartGmt6,
    int DurationMinutes,
    string? Note = null);

public sealed record TransitionAppointmentStatusCommand(
    AgendaActorContext Actor,
    Guid AppointmentId,
    string NextStatus,
    string? Note = null);

internal static class AppointmentMapper
{
    public static AppointmentDto ToDto(Appointment appointment) =>
        new(
            appointment.Id,
            appointment.TenantId,
            appointment.PatientId,
            appointment.SpecialistId,
            Gmt6Clock.FormatIsoGmt6(appointment.ScheduledStartUtc),
            Gmt6Clock.FormatIsoGmt6(appointment.ScheduledEndUtc),
            appointment.DurationMinutes,
            appointment.Reason,
            appointment.Status.ToString(),
            AppointmentPolicy.GetAllowedTransitions(appointment.Status).Select(s => s.ToString()).ToArray(),
            appointment.CreatedByAccountId,
            appointment.CreatedByName,
            Gmt6Clock.FormatIsoGmt6(appointment.CreatedAtUtc),
            Gmt6Clock.FormatIsoGmt6(appointment.UpdatedAtUtc));

    public static AppointmentDetailDto ToDetailDto(Appointment appointment) =>
        new(
            appointment.Id,
            appointment.TenantId,
            appointment.PatientId,
            appointment.SpecialistId,
            Gmt6Clock.FormatIsoGmt6(appointment.ScheduledStartUtc),
            Gmt6Clock.FormatIsoGmt6(appointment.ScheduledEndUtc),
            appointment.DurationMinutes,
            appointment.Reason,
            appointment.Status.ToString(),
            AppointmentPolicy.GetAllowedTransitions(appointment.Status).Select(s => s.ToString()).ToArray(),
            appointment.CreatedByAccountId,
            appointment.CreatedByName,
            Gmt6Clock.FormatIsoGmt6(appointment.CreatedAtUtc),
            Gmt6Clock.FormatIsoGmt6(appointment.UpdatedAtUtc),
            appointment.History
                .OrderBy(h => h.OccurredAtUtc)
                .Select(h => new AppointmentHistoryItemDto(
                    h.Id,
                    h.EventType.ToString(),
                    h.FromStatus?.ToString(),
                    h.ToStatus.ToString(),
                    h.PreviousStartUtc is DateTimeOffset prev ? Gmt6Clock.FormatIsoGmt6(prev) : null,
                    h.NewStartUtc is DateTimeOffset next ? Gmt6Clock.FormatIsoGmt6(next) : null,
                    h.PreviousDurationMinutes,
                    h.NewDurationMinutes,
                    h.ActorAccountId,
                    h.ActorName,
                    h.Note,
                    Gmt6Clock.FormatIsoGmt6(h.OccurredAtUtc)))
                .ToArray());
}
