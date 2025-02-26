namespace PublicServiceRegistry.Projector.Infrastructure
{
    using System;
    using System.Linq;
    using System.Reflection;
    using Be.Vlaanderen.Basisregisters.Api;
    using Be.Vlaanderen.Basisregisters.DataDog.Tracing.AspNetCore;
    using Be.Vlaanderen.Basisregisters.DataDog.Tracing.Autofac;
    using Autofac;
    using Autofac.Extensions.DependencyInjection;
    using Be.Vlaanderen.Basisregisters.Projector.ConnectedProjections;
    using Configuration;
    using HostedServices;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Mvc.ApiExplorer;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Logging;
    using Modules;
    using PublicServiceRegistry.Projections.Backoffice;
    using Swashbuckle.AspNetCore.Swagger;
    using IApplicationLifetime = Microsoft.AspNetCore.Hosting.IApplicationLifetime;
    using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;
    using TraceSource = Be.Vlaanderen.Basisregisters.DataDog.Tracing.TraceSource;

    /// <summary>Represents the startup process for the application.</summary>
    public class Startup
    {
        private const string DatabaseTag = "db";

        private IContainer _applicationContainer;

        private readonly IConfiguration _configuration;
        private readonly ILoggerFactory _loggerFactory;

        public Startup(
            IConfiguration configuration,
            ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _loggerFactory = loggerFactory;
        }

        /// <summary>Configures services for the application.</summary>
        /// <param name="services">The collection of services to configure the application with.</param>
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services
                .ConfigureDefaultForApi<Startup>(new StartupConfigureOptions
                {
                    Cors =
                    {
                        Origins = _configuration
                            .GetSection("Cors")
                            .GetChildren()
                            .Select(c => c.Value)
                            .ToArray()
                    },
                    Swagger =
                    {
                        ApiInfo = (provider, description) => new Info
                        {
                            Version = description.ApiVersion.ToString(),
                            Title = "Basisregisters Vlaanderen Public Service Registry API",
                            Description = GetApiLeadingText(description),
                            Contact = new Contact
                            {
                                Name = "Informatie Vlaanderen",
                                Email = "informatie.vlaanderen@vlaanderen.be",
                                Url = "https://legacy.basisregisters.vlaanderen"
                            }
                        },
                        XmlCommentPaths = new [] { typeof(Startup).GetTypeInfo().Assembly.GetName().Name }
                    },
                    MiddlewareHooks =
                    {
                        AfterHealthChecks = health =>
                        {
                            var connectionStrings = _configuration
                                .GetSection("ConnectionStrings")
                                .GetChildren();

                            foreach (var connectionString in connectionStrings)
                                health.AddSqlServer(
                                    connectionString.Value,
                                    name: $"sqlserver-{connectionString.Key.ToLowerInvariant()}",
                                    tags: new[] { DatabaseTag, "sql", "sqlserver" });

                            health.AddDbContextCheck<BackofficeContext>(
                                $"dbcontext-{nameof(BackofficeContext).ToLowerInvariant()}",
                                tags: new[] { DatabaseTag, "sql", "sqlserver" });
                        }
                    }
                })
                .AddSingleton<IHostedService, ClockTickingService>();

            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterModule(new ApiModule(_configuration, services, _loggerFactory));
            _applicationContainer = containerBuilder.Build();

            return new AutofacServiceProvider(_applicationContainer);
        }

        public void Configure(
            IServiceProvider serviceProvider,
            IApplicationBuilder app,
            IHostingEnvironment env,
            IApplicationLifetime appLifetime,
            ILoggerFactory loggerFactory,
            IApiVersionDescriptionProvider apiVersionProvider,
            ApiDataDogToggle datadogToggle,
            ApiDebugDataDogToggle debugDataDogToggle,
            HealthCheckService healthCheckService)
        {
            StartupHelpers.CheckDatabases(healthCheckService, DatabaseTag).GetAwaiter().GetResult();

            app
                .UseDataDog<Startup>(new DataDogOptions
                {
                    Common =
                    {
                        ServiceProvider = serviceProvider,
                        LoggerFactory = loggerFactory
                    },
                    Toggles =
                    {
                        Enable = datadogToggle,
                        Debug = debugDataDogToggle
                    },
                    Tracing =
                    {
                        ServiceName = _configuration["DataDog:ServiceName"],
                    }
                })

                .UseDefaultForApi(new StartupUseOptions
                {
                    Common =
                    {
                        ApplicationContainer = _applicationContainer,
                        ServiceProvider = serviceProvider,
                        HostingEnvironment = env,
                        ApplicationLifetime = appLifetime,
                        LoggerFactory = loggerFactory
                    },
                    Api =
                    {
                        VersionProvider = apiVersionProvider,
                        Info = groupName => $"Basisregisters Vlaanderen - Public Service Registry API {groupName}",
                        CSharpClientOptions =
                        {
                            ClassName = "PublicServiceRegistryProjector",
                            Namespace = "Be.Vlaanderen.Basisregisters"
                        },
                        TypeScriptClientOptions =
                        {
                            ClassName = "PublicServiceRegistryProjector"
                        }
                    },
                    MiddlewareHooks =
                    {
                        AfterMiddleware = x => x.UseMiddleware<AddNoCacheHeadersMiddleware>(),
                    }
                });

            var projectionsManager = serviceProvider.GetRequiredService<IConnectedProjectionsManager>();
            projectionsManager.Start();
        }

        private static string GetApiLeadingText(ApiVersionDescription description)
            => $"Momenteel leest u de documentatie voor versie {description.ApiVersion} van de Basisregisters Vlaanderen Public Service Registry API{string.Format(description.IsDeprecated ? ", **deze API versie is niet meer ondersteund * *." : ".")}";
    }
}
