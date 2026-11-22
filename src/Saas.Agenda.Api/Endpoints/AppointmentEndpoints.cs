using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Saas.Agenda.Application.UseCases.Appointments;

namespace Saas.Agenda.Api.Endpoints;

public sealed record CreateAppointmentHttpRequest(
    Guid PatientId,
    Guid SpecialistId,
    string ScheduledStartGmt6,
    int DurationMinutes,
    string Reason);

public sealed record RescheduleAppointmentHttpRequest(
    string ScheduledStartGmt6,
    int DurationMinutes,
    string? Note);

public sealed record TransitionAppointmentStatusHttpRequest(
    string NextStatus,
    string? Note);

internal static class AppointmentEndpoints
{
    public static void MapAppointmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/appointments")
            .WithTags("Appointments")
            .RequireAuthorization();

        group.MapGet("/", async (
                ClaimsPrincipal user,
                [FromQuery] Guid? specialistId,
                [FromQuery] Guid? patientId,
                [FromQuery] string? status,
                [FromQuery] string? fromDateGmt6,
                [FromQuery] string? toDateGmt6,
                ListAppointmentsUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(
                    new ListAppointmentsQuery(
                        actor,
                        specialistId,
                        patientId,
                        status,
                        fromDateGmt6,
                        toDateGmt6),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("ListAppointments")
            .WithSummary("Listar citas internas de la clínica filtradas por ámbito y GMT-6 (CU-18 / CU-10)")
            .WithDescription("Devuelve citas del tenant respetando aislamiento por clínica, agenda propia para Especialista y ámbito ordinario o permiso excepcional para Auxiliar.")
            .Produces<AppointmentListResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{appointmentId:guid}", async (
                Guid appointmentId,
                ClaimsPrincipal user,
                GetAppointmentByIdUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(actor, appointmentId, cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("GetAppointmentById")
            .WithSummary("Consultar detalle e historial trazable de una cita (CU-18)")
            .WithDescription("Devuelve la cita y su bitácora inmutable de eventos en GMT-6 siempre que pertenezca al tenant y ámbito autorizado.")
            .Produces<AppointmentDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/", async (
                ClaimsPrincipal user,
                CreateAppointmentHttpRequest request,
                CreateAppointmentUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(
                    new CreateAppointmentCommand(
                        actor,
                        request.PatientId,
                        request.SpecialistId,
                        request.ScheduledStartGmt6,
                        request.DurationMinutes,
                        request.Reason),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Created($"/v1/appointments/{result.Value.Id}", result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("CreateAppointment")
            .WithSummary("Programar cita interna en estado PROGRAMADA (CU-18)")
            .WithDescription("Valida ámbito CU-10 (ordinario, pendiente de asignación inicial o permiso excepcional), especialista activo, horario estricto GMT-6 y ausencia de traslapes sin sobrecupo.")
            .Produces<AppointmentDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        group.MapPut("/{appointmentId:guid}/reschedule", async (
                Guid appointmentId,
                ClaimsPrincipal user,
                RescheduleAppointmentHttpRequest request,
                RescheduleAppointmentUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(
                    new RescheduleAppointmentCommand(
                        actor,
                        appointmentId,
                        request.ScheduledStartGmt6,
                        request.DurationMinutes,
                        request.Note),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("RescheduleAppointment")
            .WithSummary("Reprogramar fecha, hora y duración de una cita activa en GMT-6 (CU-18)")
            .WithDescription("Bloquea citas en estados terminales (COMPLETADA, CANCELADA, NO_ASISTIO), valida conflictos de horario excluyendo la propia cita y registra historial inmutable.")
            .Produces<AppointmentDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        group.MapPatch("/{appointmentId:guid}/status", async (
                Guid appointmentId,
                ClaimsPrincipal user,
                TransitionAppointmentStatusHttpRequest request,
                TransitionAppointmentStatusUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(
                    new TransitionAppointmentStatusCommand(
                        actor,
                        appointmentId,
                        request.NextStatus,
                        request.Note),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("TransitionAppointmentStatus")
            .WithSummary("Actualizar estado administrativo de cita dentro del catálogo cerrado (CU-18)")
            .WithDescription("Aplica la máquina de estados cerrada (PROGRAMADA -> CONFIRMADA/EN_CURSO/CANCELADA/NO_ASISTIO) sin generar automáticamente atención clínica.")
            .Produces<AppointmentDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);
    }
}
