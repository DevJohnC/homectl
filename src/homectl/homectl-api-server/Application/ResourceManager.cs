﻿using homectl_api_server.Resources;
using System;
using System.Collections.Generic;

namespace homectl_api_server.Application
{
	/// <summary>
	/// Manages instances of resources.
	/// </summary>
	public class ResourceManager
	{
		public const string CoreKindGroup = "core";
		public const string AlphaKindVersion = "v1alpha1";

		public readonly static ResourceKind ResourceKind = new ResourceKind(CoreKindGroup, AlphaKindVersion, "resource");

		public readonly static ResourceKind KindKind = new ResourceKind(CoreKindGroup, AlphaKindVersion, "kind");

		public readonly static ResourceKind ControllerKind = new ResourceKind(CoreKindGroup, AlphaKindVersion, "controller");

		public readonly static ResourceKind DeviceKind = new ResourceKind(CoreKindGroup, AlphaKindVersion, "device");

		public ResourceManager()
		{
			CreateKind(ResourceKind, new KindManager(ResourceKind));
			CreateKind(KindKind, new KindResourceManager(KindKind, this));
			CreateKind(ControllerKind, new ControllerResourceManager(ControllerKind));
			CreateKind(DeviceKind, new DeviceResourceManager(DeviceKind));
		}

		private readonly Dictionary<(string group, string apiVersion, string kindName), KindManager> _kinds =
			new Dictionary<(string group, string apiVersion, string kindName), KindManager>();

		public void CreateKind(ResourceKind resourceKind, KindManager manager)
		{
			if (manager == null)
				throw new ArgumentNullException(nameof(manager));

			var key = (resourceKind.Group, resourceKind.ApiVersion, resourceKind.KindName);
			_kinds.Add(key, manager);
		}

		public KindManager GetKind(string group, string apiVersion, string kindName)
		{
			var key = (group, apiVersion, kindName);
			_kinds.TryGetValue(key, out var kind);
			return kind;
		}
	}
}
