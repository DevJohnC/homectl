using HomeCtl.Connection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HomeCtl.ApiServer
{
	class Startup
	{
		private void AddCoreResourceManager<T>(IServiceCollection services)
			where T : Resources.ResourceManager
		{
			services.AddSingleton<T>();
			services.AddSingleton<Resources.ResourceManager>(sP => sP.GetRequiredService<T>());
		}

		public void ConfigureServices(IServiceCollection services)
		{
			services.AddSingleton<IEndpointClientFactory, ReuseSingleClientFactory>(
				sP => new ReuseSingleClientFactory(HttpClientHelper.CreateHttpClient())
				);
			services.AddSingleton<IServerIdentityVerifier, GrpcIdentityVerifier>();

			services.AddSingleton<Connections.ConnectionManager>();
			services.AddHostedService<Connections.ConnectionManagerHostestService>();

			services.AddSingleton<Events.EventBus>();

			services.AddSingleton<Connections.ConnectionManager>();

			services.AddSingleton<Orchestration.OrchestrationConductor>();
			services.AddHostedService<BackgroundServices.OrchestrationBackgroundService>();

			services.AddSingleton<Resources.ResourceStateManager>();
			services.AddSingleton<Resources.ResourceManagerContainer>();
			services.AddSingleton(typeof(Resources.IResourceDocumentStore<>), typeof(Resources.FileResourceDocumentStore<>));
			services.AddSingleton<Resources.IDocumentStoreFactory, Resources.FileResourceDocumentStoreFactory>();
			services.AddSingleton<Resources.ResourceStateStore>();

			services.AddSingleton<Kinds.KindManager>();
			services.AddSingleton<StartupService>(sP => new StartupService(sP.GetRequiredService<Kinds.KindManager>()));
			services.AddSingleton<Hosts.HostManager>();
			services.AddSingleton<StartupService>(sP => new StartupService(sP.GetRequiredService<Hosts.HostManager>()));
			services.AddSingleton<Devices.DeviceManager>();
			services.AddSingleton<StartupService>(sP => new StartupService(sP.GetRequiredService<Devices.DeviceManager>()));

			services.AddGrpc();
		}

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseRouting();

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapGrpcService<ProtocolServices.InformationService>();
				endpoints.MapGrpcService<ProtocolServices.ControlService>();
				endpoints.MapGrpcService<ProtocolServices.NetworkService>();
			});
		}
	}
}
