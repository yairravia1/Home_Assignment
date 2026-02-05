namespace HomeAssignment.Api.Configuration.Settings;

public record JwtSettings(
    string SecretKey,
    string Issuer,
    string Audience);

