using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GraphQL.Server;
using GraphQL.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.Middleware;
using NineChronicles.Headless.Properties;
using Serilog;

namespace NineChronicles.Headless
{
    public class GraphQLService
    {
        public const string LocalPolicyKey = "LocalPolicy";

        public const string SecretTokenKey = "secret";

        private GraphQLNodeServiceProperties GraphQlNodeServiceProperties { get; }

        public GraphQLService(GraphQLNodeServiceProperties properties)
        {
            GraphQlNodeServiceProperties = properties;
        }

        public IHostBuilder Configure(IHostBuilder hostBuilder, StandaloneContext standaloneContext)
        {
            var listenHost = GraphQlNodeServiceProperties.GraphQLListenHost;
            var listenPort = GraphQlNodeServiceProperties.GraphQLListenPort;

            return hostBuilder.ConfigureWebHostDefaults(builder =>
            {
                builder.UseStartup<GraphQLStartup>();
                builder.ConfigureAppConfiguration(
                    (context, builder) =>
                    {
                        if (GraphQlNodeServiceProperties.SecretToken is { } secretToken)
                        {
                            builder.AddInMemoryCollection(
                                new Dictionary<string, string>
                                {
                                    { SecretTokenKey, secretToken },
                                });   
                        }
                    });
                builder.ConfigureServices(
                    services => services.AddSingleton(standaloneContext));
                builder.UseUrls($"http://{listenHost}:{listenPort}/");
            });
        }

        class GraphQLStartup
        {
            public GraphQLStartup(IConfiguration configuration)
            {
                Configuration = configuration;
            }

            public IConfiguration Configuration { get; }

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddCors(options =>
                    options.AddPolicy(
                        "AllowAllOrigins",
                        builder =>
                            builder.AllowAnyOrigin()
                                .AllowAnyMethod()
                                .AllowAnyHeader()
                    )
                );

                services.AddTransient<LocalAuthenticationMiddleware>();

                services.AddHealthChecks();

                services.AddControllers();

                services
                    .AddSingleton<StandaloneSchema>()
                    .AddGraphQL(
                        (options, provider) =>
                        {
                            options.EnableMetrics = true;
                            options.UnhandledExceptionDelegate = context =>
                            {
                                Log.Error(
                                    context.Exception,
                                    context.ErrorMessage);
                            };
                        })
                    .AddSystemTextJson()
                    .AddWebSockets()
                    .AddDataLoader()
                    .AddGraphTypes(typeof(StandaloneSchema))
                    .AddGraphQLAuthorization(
                        options => options.AddPolicy(
                            LocalPolicyKey,
                            p =>
                                p.RequireClaim(
                                    "role",
                                    "Admin")));
            }

            public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
            {
                if (env.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                }

                app.UseMiddleware<LocalAuthenticationMiddleware>();
                app.UseCors("AllowAllOrigins");

                app.UseRouting();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    endpoints.MapHealthChecks("/health-check");
                });

                // WebSocket으로 운영합니다.
                app.UseWebSockets();
                app.UseGraphQLWebSockets<StandaloneSchema>("/graphql");
                app.UseGraphQL<StandaloneSchema>("/graphql");

                // /ui/playground 옵션을 통해서 Playground를 사용할 수 있습니다.
                app.UseGraphQLPlayground();
            }
        }
    }
}
