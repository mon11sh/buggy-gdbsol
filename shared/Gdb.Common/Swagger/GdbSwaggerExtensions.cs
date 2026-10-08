using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Gdb.Common.Swagger;

/// <summary>
/// One shared OpenAPI/Swagger setup for every GDB service.
/// </summary>
/// <remarks>
/// Replaces ten hand-rolled, divergent <c>AddSwaggerGen</c>/<c>UseSwaggerUI</c> blocks. The
/// document always carries a Bearer security definition (so protected endpoints can be
/// exercised from the UI) and, optionally, the internal API-key scheme. The UI and JSON
/// document are only served in Development, or when <c>EnableSwagger=true</c> is configured
/// explicitly — never by default in Production.
/// </remarks>
public static class GdbSwaggerExtensions
{
    public const string ConfigKey = "EnableSwagger";

    public static IServiceCollection AddGdbSwagger(
        this IServiceCollection services,
        string title,
        string version = "1.0.0",
        string? description = null,
        bool internalApiKey = false,
        Action<SwaggerGenOptions>? configure = null)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = title, Version = version, Description = description });
            c.EnableAnnotations();
            c.SchemaFilter<Filters.PythonContractSchemaFilter>();

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });

            if (internalApiKey)
            {
                c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
                {
                    Description = "Internal service-to-service key sent in the X-Internal-API-Key header",
                    Name = "X-Internal-API-Key",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "ApiKeyScheme"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
                        },
                        Array.Empty<string>()
                    }
                });
            }

            configure?.Invoke(c);
        });
        return services;
    }

    /// <summary>
    /// Serves the OpenAPI document and UI at <c>/docs</c> — only in Development or when
    /// <c>EnableSwagger=true</c>. In every other environment nothing is mapped, so the API
    /// surface (including internal endpoints) is not disclosed.
    /// </summary>
    public static WebApplication UseGdbSwagger(this WebApplication app, string title, string pathBase = "")
    {
        var enabled = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>(ConfigKey);
        if (!enabled)
            return app;

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint($"{pathBase}/swagger/v1/swagger.json", $"{title} v1");
            c.RoutePrefix = "docs";
        });
        return app;
    }
}
