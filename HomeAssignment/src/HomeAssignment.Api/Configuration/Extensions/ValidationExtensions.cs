using Microsoft.AspNetCore.Mvc;

namespace HomeAssignment.Api.Configuration.Extensions;

public static class ValidationExtensions
{
    public static IServiceCollection AddInputValidation(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                    );

                var response = new
                {
                    Message = "Validation failed",
                    Errors = errors
                };

                return new BadRequestObjectResult(response);
            };
        });

        return services;
    }
}

