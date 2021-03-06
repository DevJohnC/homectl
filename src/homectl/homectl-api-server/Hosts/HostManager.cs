﻿using HomeCtl.ApiServer.Connections;
using HomeCtl.ApiServer.Resources;
using HomeCtl.Connection;
using HomeCtl.Events;
using HomeCtl.Kinds;
using HomeCtl.Kinds.Resources;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HomeCtl.ApiServer.Hosts
{
	class HostManager : ResourceManager<Host>
	{
		private readonly ConnectionManager _connectionManager;
		private readonly EventBus _eventBus;
		private readonly ILoggerFactory _loggerFactory;
		private readonly IEndpointClientFactory _endpointClientFactory;
		private readonly IServerIdentityVerifier _serverIdentityVerifier;
		private readonly ResourceStateManager _resourceStateManager;
		private readonly object _lock = new object();
		private readonly Dictionary<Guid, HostServer> _hosts =
			new Dictionary<Guid, HostServer>();

		protected override Kind<Host> TypedKind => CoreKinds.Host;

		public HostManager(ConnectionManager connectionManager, EventBus eventBus,
			ILoggerFactory loggerFactory, IEndpointClientFactory endpointClientFactory,
			IServerIdentityVerifier serverIdentityVerifier, IResourceDocumentStore<Host> documentStore,
			ResourceStateManager resourceStateManager, ResourceManagerContainer resourceManagers) :
			base(documentStore, resourceManagers)
		{
			_connectionManager = connectionManager;
			_eventBus = eventBus;
			_loggerFactory = loggerFactory;
			_endpointClientFactory = endpointClientFactory;
			_serverIdentityVerifier = serverIdentityVerifier;
			_resourceStateManager = resourceStateManager;
		}

		protected override async Task Created(Host resource)
		{
			await resource.SetConnectedState(_resourceStateManager, Host.ConnectedState.NotConnected);

			var hostServer = new HostServer(resource,
				new EndpointConnectionManager(
					_eventBus, _endpointClientFactory, _serverIdentityVerifier,
					_loggerFactory.CreateLogger<EndpointConnectionManager>()
					),
				_eventBus,
				_loggerFactory.CreateLogger<HostServer>(),
				this,
				_resourceStateManager
				);

			lock (_lock)
			{
				_hosts.Add(resource.HostId, hostServer);
			}

			_connectionManager.CreateConnection(hostServer);
		}

		protected override Host? CreateFromDocument(ResourceDocument resourceDocument)
		{
			CoreKinds.Host.TryConvertToResourceInstance(resourceDocument, out var host);
			return host;
		}

		protected override void CopyData(Host target, Host source)
		{
			target.Endpoint = source.Endpoint;
			target.MachineName = source.MachineName;
		}

		protected override Task Updated(Host resource)
		{
			if (!_hosts.TryGetValue(resource.HostId, out var host))
				return Task.CompletedTask;

			_connectionManager.UpdateConnection(host);
			return Task.CompletedTask;
		}
	}
}
