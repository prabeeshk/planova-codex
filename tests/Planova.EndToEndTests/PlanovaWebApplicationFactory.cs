using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Planova.Identity.Infrastructure;
using Planova.Infrastructure.Events;
using Planova.Infrastructure.Persistence;
using Planova.Projects.Infrastructure;

namespace Planova.EndToEndTests;

public sealed class PlanovaWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly IServiceProvider InMemoryServices = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly ExceptionCaptureLoggerProvider _exceptionLogger = new();

    public Exception? LastException => _exceptionLogger.LastException;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("ConnectionStrings:Planova", "Host=unused;Database=unused;Username=unused");
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ILoggerProvider>(_exceptionLogger);
            services.RemoveAll<DbContextOptions<IdentityDbContext>>();
            services.RemoveAll<IdentityDbContext>();
            services.RemoveAll<DbContextOptions<PlatformDbContext>>();
            services.RemoveAll<PlatformDbContext>();
            services.RemoveAll<DbContextOptions<ProjectsDbContext>>();
            services.RemoveAll<ProjectsDbContext>();
            services.RemoveAll<IHostedService>();
            services.AddDbContext<IdentityDbContext>(options => options
                .UseInMemoryDatabase(_databaseName + "-identity")
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .UseInternalServiceProvider(InMemoryServices));
            services.AddDbContext<PlatformDbContext>(options => options
                .UseInMemoryDatabase(_databaseName + "-platform")
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .UseInternalServiceProvider(InMemoryServices));
            services.AddDbContext<ProjectsDbContext>(options => options
                .UseInMemoryDatabase(_databaseName + "-projects")
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .UseInternalServiceProvider(InMemoryServices));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        DevelopmentIdentitySeeder.SeedAsync(identity).GetAwaiter().GetResult();
        return host;
    }

    private sealed class ExceptionCaptureLoggerProvider : ILoggerProvider
    {
        public Exception? LastException { get; private set; }

        public ILogger CreateLogger(string categoryName) => new ExceptionCaptureLogger(this);
        public void Dispose() { }

        private sealed class ExceptionCaptureLogger(ExceptionCaptureLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (exception is not null) provider.LastException = exception;
            }
        }
    }
}
