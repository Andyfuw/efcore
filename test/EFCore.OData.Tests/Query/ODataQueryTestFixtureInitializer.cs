using System.Net;
using System.Net.Http;
using System.Reflection;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class ODataQueryTestFixtureInitializer
    {
        public static (string BaseAddress, IHttpClientFactory ClientFactory, IHost SelfHostServer) Initialize<T>()
        {
            SecurityHelper.AddIpListen();

            // 1) if you want to configure the service, add "protected static void UpdateConfigureServices(IServiceCollection)" method into your test class.
            // 2) if you want to configure the routing, add "protected static void UpdateConfigure(EndpointRouteConfiguration)" method into your test class.
            var testType = typeof(T);
            var configureServicesMethod = testType.GetMethod("UpdateConfigureServices", BindingFlags.NonPublic | BindingFlags.Static);
            var configureMethod = testType.GetMethod("UpdateConfigure", BindingFlags.NonPublic | BindingFlags.Static);

            string serverName = "localhost";

            var port = PortArranger.Reserve();
            var baseAddress = string.Format("http://{0}:{1}", serverName, port.ToString());

            var clientFactory = default(IHttpClientFactory);
            var selfHostServer = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder => webBuilder
                    .UseKestrel(options => options.Listen(IPAddress.Loopback, port))
                    .ConfigureServices(services =>
                    {
                        services.AddHttpClient();
                        services.AddOData();
                        services.AddRouting();

                        // Apply custom services for each test class
                        configureServicesMethod?.Invoke(null, new object[] { services });
                    })
                    .Configure(app =>
                    {
                         clientFactory = app.ApplicationServices.GetRequiredService<IHttpClientFactory>();

                        app.UseODataBatching();
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            // Apply test configuration.
                            var config = new EndpointRouteConfiguration(endpoints);
                            configureMethod?.Invoke(null, new object[] { config });
                        });
                    })
                    .ConfigureLogging((hostingContext, logging) =>
                    {
                        logging.AddDebug();
                        logging.SetMinimumLevel(LogLevel.Warning);
                    }
                )).Build();

            selfHostServer.Start();

            return (baseAddress, clientFactory, selfHostServer);
        }
    }
}

