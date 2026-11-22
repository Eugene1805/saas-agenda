using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Saas.Agenda.Api.OpenApi;

internal static class OpenApiExtensions
{
    public static IServiceCollection AddAgendaOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<AgendaOpenApiDocumentTransformer>();
        });

        return services;
    }

    public static void MapAgendaOpenApi(this WebApplication app)
    {
        var openApiEnabled = app.Environment.IsDevelopment() ||
            app.Configuration.GetValue("OpenApi:Enabled", false);

        if (!openApiEnabled)
        {
            return;
        }

        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("Vital - saas-agenda API v1");
        });
    }
}

internal sealed class AgendaOpenApiDocumentTransformer(
    IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Vital - saas-agenda API",
            Version = "v1",
            Description = "Microservicio de agenda interna multitenant (CU-18 estados cerrados, CU-10 alcance por ámbito/permisos, bloqueo de traslapes sin sobrecupo en PostgreSQL, KPI CU-16 y horario fijo GMT-6).",
        };

        var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(s => string.Equals(s.Name, "Bearer", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Access Token JWT emitido por saas-identity (/v1/auth/login o /v1/auth/platform/login).",
        };

        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        };

        foreach (var (pathKey, pathItem) in document.Paths)
        {
            if (pathItem.Operations is null || !pathKey.StartsWith("/v1/", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var operation in pathItem.Operations.Values)
            {
                operation.Security ??= [];
                operation.Security.Add(requirement);
            }
        }
    }
}
