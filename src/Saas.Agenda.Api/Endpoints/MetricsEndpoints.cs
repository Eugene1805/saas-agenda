using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Saas.Agenda.Application.UseCases.Metrics;

namespace Saas.Agenda.Api.Endpoints;

internal static class MetricsEndpoints
{
    public static void MapMetricsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/metrics")
            .WithTags("Metrics")
            .RequireAuthorization();

        group.MapGet("/clinic/activity", async (
                ClaimsPrincipal user,
                [FromQuery] string? fromDateGmt6,
                [FromQuery] string? toDateGmt6,
                GetClinicAgendaMetricsUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(
                    actor,
                    fromDateGmt6,
                    toDateGmt6,
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("GetClinicAgendaMetrics")
            .WithSummary("Consultar KPI AGENDA_ACTIVITY de la clínica en GMT-6 (CU-16)")
            .WithDescription("Devuelve conteos agregados por estado cerrado de cita para AdminClinica, distinguiendo 0 como valor AVAILABLE.")
            .Produces<KpiEnvelopeDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/platform/activity", async (
                ClaimsPrincipal user,
                [FromQuery] string? fromDateGmt6,
                [FromQuery] string? toDateGmt6,
                GetPlatformAgendaMetricsUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(
                    actor,
                    fromDateGmt6,
                    toDateGmt6,
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("GetPlatformAgendaMetrics")
            .WithSummary("Consultar KPI global APPOINTMENT_ACTIVITY para SuperAdmin sin PII (CU-16)")
            .WithDescription("Agrega actividad de citas en todas las clínicas para el panel BI de plataforma sin exponer pacientes ni notas.")
            .Produces<KpiEnvelopeDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);
    }
}
