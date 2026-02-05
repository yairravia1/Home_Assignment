namespace HomeAssignment.Infrastructure.Configuration.Settings.MongoSettings;

public record MongoSettings(string ConnectionString, string DatabaseName, string CollectionName);