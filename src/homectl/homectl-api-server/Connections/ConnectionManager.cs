﻿using HomeCtl.ApiServer.Hosts;
using HomeCtl.Connection;
using HomeCtl.Events;
using HomeCtl.Kinds;
using HomeCtl.Services.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HomeCtl.ApiServer.Connections
{
	class ConnectionManagerHostestService : BackgroundService
	{
		private readonly ConnectionManager _connectionManager;

		public ConnectionManagerHostestService(ConnectionManager connectionManager)
		{
			_connectionManager = connectionManager;
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken)
			=> _connectionManager.Run(stoppingToken);
	}

	class ConnectionManager
	{
		private readonly Dictionary<Guid, EndpointConnectionRunner> _hostConnections =
			new Dictionary<Guid, EndpointConnectionRunner>();
		private readonly EventBus _eventBus;
		private readonly ILoggerFactory _loggerFactory;
		private readonly ILogger<ConnectionManager> _logger;

		public ConnectionManager(EventBus eventBus, ILoggerFactory loggerFactory)
		{
			_eventBus = eventBus;
			_loggerFactory = loggerFactory;
			_logger = loggerFactory.CreateLogger<ConnectionManager>();
		}

		public void CreateConnection(HostServer host)
		{
			_hostConnections.Add(
				host.Host.Metadata.HostId,
				new EndpointConnectionRunner(
					host.Host,
					host.ConnectionManager,
					_logger,
					_eventBus,
					_loggerFactory));
		}

		private async Task<EndpointConnectionRunner?> Delay(TimeSpan timeSpan)
		{
			try
			{
				await Task.Delay(timeSpan);
			}
			//  prevent throwing an exception for being cancelled
			catch { }

			return null;
		}

		public async Task Run(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				var finishedTask = await Task.WhenAny(
					_hostConnections.Values.Select(q => q.WaitForFinish(stoppingToken))
						.Concat(new[] { Delay(TimeSpan.FromSeconds(1)) })
					);

				var runner = await finishedTask;

				if (runner == null) // delay task
					continue;

				_hostConnections.Remove(runner.Host.Metadata.HostId);
			}
		}

		private class EndpointConnectionRunner
		{
			public Kinds.Host Host { get; }

			private readonly EndpointConnectionManager _connectionManager;
			private readonly ILogger<ConnectionManager> _logger;
			private readonly EventBus _eventBus;
			private readonly ILoggerFactory _loggerFactory;
			private Task<EndpointConnectionRunner?>? _runningTask;

			public EndpointConnectionRunner(
				Kinds.Host host,
				EndpointConnectionManager connectionManager,
				ILogger<ConnectionManager> logger,
				EventBus eventBus,
				ILoggerFactory loggerFactory)
			{
				Host = host;
				_connectionManager = connectionManager;
				_logger = logger;
				_eventBus = eventBus;
				_loggerFactory = loggerFactory;
			}

			private async Task<EndpointConnectionRunner?> Run(CancellationToken stoppingToken)
			{
				try
				{
					await _connectionManager.Run(
							new[] { StaticApiServer.AnyOnUri(Host.State.Endpoint) },
							new[] { new NetworkErrorLivelinessMonitor(_eventBus,
								_loggerFactory.CreateLogger<NetworkErrorLivelinessMonitor>()) },
							stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Encountered an exception while running endpoint connection.");
				}

				return this;
			}

			public Task<EndpointConnectionRunner?> WaitForFinish(CancellationToken stoppingToken)
			{
				if (_runningTask == null)
					_runningTask = Run(stoppingToken);
				return _runningTask;
			}
		}
	}
}