using System.Text;
using HomeAssignment.Api.Configuration.Settings;
using HomeAssignment.Infrastructure.Configuration.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace HomeAssignment.Api.Configuration.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = new JwtSettings(
            SecretKey: configuration.GetRequiredString("JwtSettings:SecretKey"),
            Issuer: configuration.GetRequiredString("JwtSettings:Issuer"),
            Audience: configuration.GetRequiredString("JwtSettings:Audience"));

        services.AddSingleton(jwtSettings);

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),

                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,

                    ValidateLifetime = true
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("UserAccess", policy => policy.RequireRole("User", "Admin"))
            .AddPolicy("AdminAccess", policy => policy.RequireRole("Admin"));

        return services;
    }
}

