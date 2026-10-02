using Microsoft.OpenApi.Models;

namespace Zonar.Api.Infrastructure;

public static class SwaggerExtensions
{
    public static IServiceCollection AddZonarSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o => o.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Zonar API",
            Version = "v1",
            Description = "Scores community messages for quality and rewards valuable contributors."
        }));
        return services;
    }

    public static WebApplication UseZonarSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(o => o.DocumentTitle = "Zonar API");
        return app;
    }
}
