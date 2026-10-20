using Npgsql;
using OrderService.Api.Configurations;
using OrderService.Api.DI;
using OrderService.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    var directory = new DirectoryInfo(builder.Environment.ContentRootPath);

    while (directory is not null)
    {
        var secretsPath = Path.Combine(directory.FullName, "docker", "secrets");
        var connectionStringPath = Path.Combine(secretsPath, "connection_string");

        if (File.Exists(connectionStringPath))
        {
            var connectionString = new NpgsqlConnectionStringBuilder(File.ReadAllText(connectionStringPath).Trim())
            {
                Host = "localhost"
            };

            var localConfiguration = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString.ConnectionString
            };

            var jwtKeyPath = Path.Combine(secretsPath, "jwt_key");

            if (File.Exists(jwtKeyPath))
            {
                localConfiguration["Jwt:SigningKey"] = File.ReadAllText(jwtKeyPath).Trim();
            }

            var passwordHashPath = Path.Combine(secretsPath, "demo_user_password_hash");

            if (File.Exists(passwordHashPath))
            {
                localConfiguration["DemoUser:PasswordHash"] = File.ReadAllText(passwordHashPath).Trim();
            }

            builder.Configuration.AddInMemoryCollection(localConfiguration);

            break;
        }

        directory = directory.Parent;
    }
}

builder.Configuration.AddKeyPerFile(
    directoryPath: "/run/secrets",
    optional: true);

// Permite que variáveis de ambiente sobrescrevam os secrets.
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

await app.ApplyDatabaseMigrationsAsync();

app.ConfigureApiPipeline();

app.Run();