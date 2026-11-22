using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Saas.Agenda.Application.UseCases.Schedules;

namespace Saas.Agenda.Api.Endpoints;

public sealed record UpdateClinicScheduleHttpRequest(
    IReadOnlyList<ScheduleSlotInput> Slots);

public sealed record UpdateSpecialistScheduleHttpRequest(
    bool UseClinicFallback,
    IReadOnlyList<ScheduleSlotInput> Slots);

public sealed record UpsertSpecialistExceptionHttpRequest(
    string DateGmt6,
    bool IsUnavailable,
    string? CustomStartTime,
    string? CustomEndTime,
    string Reason);

internal static class ScheduleEndpoints
{
    public static void MapScheduleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/schedules")
            .WithTags("Schedules")
            .RequireAuthorization();

        group.MapGet("/clinic", async (
                ClaimsPrincipal user,
                GetClinicScheduleUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(actor, cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("GetClinicSchedule")
            .WithSummary("Consultar horario laboral base de la clínica (GMT-6)")
            .WithDescription("Devuelve los bloques semanales de la clínica usados como respaldo cuando un especialista no define horario propio.")
            .Produces<ClinicScheduleDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        group.MapPut("/clinic", async (
                ClaimsPrincipal user,
                UpdateClinicScheduleHttpRequest request,
                UpdateClinicScheduleUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(
                    new UpdateClinicScheduleCommand(actor, request.Slots ?? []),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("UpdateClinicSchedule")
            .WithSummary("Configurar horario laboral base de la clínica (solo AdminClinica)")
            .WithDescription("Valida bloques sin traslapes internos y bloquea el cambio (409) si existen citas activas futuras de especialistas que heredan este horario.")
            .Produces<ClinicScheduleDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/specialists/{specialistId:guid}", async (
                Guid specialistId,
                ClaimsPrincipal user,
                GetSpecialistScheduleUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(actor, specialistId, cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("GetSpecialistSchedule")
            .WithSummary("Consultar horario propio, respaldo efectivo y excepciones de un especialista")
            .WithDescription("AdminClinica y Auxiliar consultan cualquier especialista del tenant; Especialista solo consulta su propio horario.")
            .Produces<SpecialistScheduleDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        group.MapPut("/specialists/{specialistId:guid}", async (
                Guid specialistId,
                ClaimsPrincipal user,
                UpdateSpecialistScheduleHttpRequest request,
                UpdateSpecialistScheduleUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(
                    new UpdateSpecialistScheduleCommand(
                        actor,
                        specialistId,
                        request.UseClinicFallback,
                        request.Slots ?? []),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("UpdateSpecialistSchedule")
            .WithSummary("Configurar horario propio del especialista o usar respaldo de clínica")
            .WithDescription("Autorizado para AdminClinica o el propio Especialista. Rechaza con 409 si el recorte deja citas futuras activas fuera de jornada.")
            .Produces<SpecialistScheduleDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/specialists/{specialistId:guid}/exceptions", async (
                Guid specialistId,
                ClaimsPrincipal user,
                UpsertSpecialistExceptionHttpRequest request,
                UpsertSpecialistExceptionUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(
                    new UpsertSpecialistExceptionCommand(
                        actor,
                        specialistId,
                        request.DateGmt6,
                        request.IsUnavailable,
                        request.CustomStartTime,
                        request.CustomEndTime,
                        request.Reason),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("UpsertSpecialistException")
            .WithSummary("Registrar día inhábil o jornada especial en fecha GMT-6")
            .WithDescription("Valida fecha no pasada, motivo obligatorio y ausencia de conflictos con citas futuras activas en esa fecha.")
            .Produces<ScheduleExceptionDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        group.MapDelete("/specialists/{specialistId:guid}/exceptions/{exceptionId:guid}", async (
                Guid specialistId,
                Guid exceptionId,
                ClaimsPrincipal user,
                DeleteSpecialistExceptionUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(
                    new DeleteSpecialistExceptionCommand(actor, specialistId, exceptionId),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.NoContent()
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("DeleteSpecialistException")
            .WithSummary("Eliminar excepción de fecha volviendo al horario semanal efectivo")
            .WithDescription("Verifica antes de borrar que el horario semanal ordinario cubra todas las citas futuras activas de esa fecha.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/specialists/{specialistId:guid}/availability", async (
                Guid specialistId,
                [FromQuery] string dateGmt6,
                [FromQuery] int? durationMinutes,
                ClaimsPrincipal user,
                GetSpecialistAvailabilityUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(
                    actor,
                    specialistId,
                    dateGmt6,
                    durationMinutes ?? 30,
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("GetSpecialistAvailability")
            .WithSummary("Consultar disponibilidad real del especialista en fecha GMT-6 (sin PII)")
            .WithDescription("Combina horario efectivo, excepciones, hora actual GMT-6 y citas activas ocupadas para calcular espacios libres disponibles.")
            .Produces<SpecialistAvailabilityDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);
    }
}
