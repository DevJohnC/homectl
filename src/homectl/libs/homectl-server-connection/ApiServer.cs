﻿using Grpc.Core.Logging;
using HomeCtl.Events;
using HomeCtl.Kinds;
using HomeCtl.Services;
using HomeCtl.Services.Server;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace HomeCtl.Connection
{
	public class ApiServer : Server
	{
		private readonly ILogger<ApiServer> _logger;

		public ApiServer(EndpointConnectionManager connectionManager, EventBus eventBus,
			ILogger<ApiServer> logger) :
			base(connectionManager)
		{
			_logger = logger;
			RegisterEventHandlers(eventBus);
		}

		public ApiServerVersion ServerVersion { get; private set; }

		public Task Apply(IResource resource)
		{
			if (!resource.Kind.TryConvertToDocument(resource, out var resourceDocument))
				throw new System.Exception("Failed to convert resource to document.");

			return Apply(resourceDocument);
		}

		public async Task Apply(Kinds.Resources.ResourceDocument resourceDocument)
		{
			var protoResourceDocument = ResourceDocument.FromResourceDocument(resourceDocument);
			var client = new Control.ControlClient(ConnectionManager.ServicesChannel);
			await client.ApplyDocumentAsync(new ApplyDocumentRequest
			{
				ResourceDocument = protoResourceDocument
			});
		}

		private void RegisterEventHandlers(EventBus eventBus)
		{
			eventBus.Subscribe<EndpointConnectionEvents.Connected>(Handle_NewConnection);
		}

		private async void Handle_NewConnection(EndpointConnectionEvents.Connected connectedArgs)
		{
			try
			{
				var client = new Information.InformationClient(ConnectionManager.ServicesChannel);
				var version = await client.GetServerVersionAsync(Empty.Instance);
				ServerVersion = new ApiServerVersion(version.ApiServerVersion.Major, version.ApiServerVersion.Minor,
					version.ApiServerVersion.Name);

				_logger.LogDebug($"Connected to API server {ServerVersion} @ {connectedArgs.ServerEndpoint.Uri}");
			}
			catch
			{
				_logger.LogDebug($"Failed to determine the version of API server @ {connectedArgs.ServerEndpoint.Uri}");
			}
		}
	}
}
