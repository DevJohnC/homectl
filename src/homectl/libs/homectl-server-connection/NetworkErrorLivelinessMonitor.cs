﻿using Grpc.Core;
using Grpc.Core.Logging;
using HomeCtl.Events;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HomeCtl.Connection
{
	public class NetworkErrorLivelinessMonitor : IServerLivelinessMonitor
	{
		private readonly EventBus _eventBus;
		private readonly ILogger<NetworkErrorLivelinessMonitor> _logger;
		private TaskCompletionSource<bool>? _tcs;
		private ServerEndpoint _serverEndpoint;

		public NetworkErrorLivelinessMonitor(EventBus eventBus, ILogger<NetworkErrorLivelinessMonitor> logger)
		{
			_eventBus = eventBus;
			_logger = logger;
		}

		private void RegisterEventHandlers()
		{
			_eventBus.Subscribe<ServicesChannelEvents.ExceptionThrown>(HandleChannelException);
		}

		private void UnregisterEventHandlers()
		{
			_eventBus.Unsubscribe<ServicesChannelEvents.ExceptionThrown>(HandleChannelException);
		}

		private void HandleChannelException(ServicesChannelEvents.ExceptionThrown args)
		{
			if (args.ServerEndpoint.Uri.OriginalString == _serverEndpoint.Uri?.OriginalString &&
				args.Exception is HttpRequestException ||
				(args.Exception is RpcException rpcEx && rpcEx.Status.StatusCode == StatusCode.Internal))
			{
				_logger.LogError(args.Exception, $"Error communicating with endpoint {args.ServerEndpoint.Uri}");
				_tcs?.TrySetResult(true);
			}
		}

		private Task WaitForExceptionOrCancellation(CancellationToken stoppingToken)
		{
			var tcs = new TaskCompletionSource<bool>();
			stoppingToken.Register(() => tcs.TrySetResult(false));
			_tcs = tcs;
			return tcs.Task;
		}

		public async Task MonitorForDisconnect(ServerEndpoint serverEndpoint, CancellationToken stoppingToken)
		{
			_serverEndpoint = serverEndpoint;

			RegisterEventHandlers();
			//  use a local stopping token tied to the original token
			//  so that we can register callbacks when the token is cancelled
			//  without leaving garbage on the token over successive calls
			using (var localStoppingToken = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
			{
				await WaitForExceptionOrCancellation(localStoppingToken.Token);
			}
			UnregisterEventHandlers();
		}
	}
}
