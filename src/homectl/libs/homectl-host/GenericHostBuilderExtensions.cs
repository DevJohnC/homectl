﻿using HomeCtl.Connection;
using HomeCtl.Events;
using HomeCtl.Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Linq;

namespace Microsoft.Extensions.Hosting
{
	public static class GenericHostBuilderExtensions
	{
		public static IHostBuilder ConfigurHomeCtlHostDefaults(this IHostBuilder builder, Action<IWebHostBuilder> configure)
		{
			return builder
				.ConfigureWebHostDefaults(webBuilder => webBuilder.ConfigurHomeCtlHostDefaults(configure));
		}

		private static IWebHostBuilder ConfigurHomeCtlHostDefaults(this IWebHostBuilder builder, Action<IWebHostBuilder> configure)
		{
			var randomizedPort = PortRandomizer.GetRandomPort();

			configure?.Invoke(builder);
			builder.ConfigureServices(svcs =>
			{
				svcs.Configure<HomeCtl.Host.HostOptions>(options => {
					options.HostFile = "host.json";
					options.HostPort = randomizedPort;
				});

				svcs.AddStartupService<HostRecordsService>();
				svcs.AddStartupService<AppHost>(sP =>
					sP.GetRequiredService<IOptions<HomeCtl.Host.HostOptions>>().Value.GetAppHost());

				svcs.AddSingleton<EventBus>();
				svcs.AddGrpc();
				svcs.AddHostedService<HomeCtlHostService>();
				svcs.AddSingleton<ApiServer>();
				svcs.AddSingleton<EndpointConnectionManager>();
				svcs.AddSingleton<IEndpointClientFactory>(sP => new ReuseSingleClientFactory(new System.Net.Http.HttpClient()));

				if (!svcs.Any(q => q.ServiceType == typeof(IServer)))
				{
					builder.UseKestrel(options =>
					{
						options.ListenAnyIP(randomizedPort);
					});
				}
			});
			builder.Configure(appBuilder =>
			{
				appBuilder.UseRouting();
				appBuilder.UseEndpoints(endpoints =>
				{
					endpoints.MapGrpcService<HomeCtl.Host.ProtocolServices.HostInterfaceService>();
				});
			});
			return builder;
		}
	}
}
