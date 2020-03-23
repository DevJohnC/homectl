﻿using homectl_api_server.Resources;

namespace homectl_api_server.Application
{
	/// <summary>
	/// Manages kind resources.
	/// </summary>
	public class KindResourceManager : KindManager
	{
		private readonly ResourceManager _resourceManager;

		public KindResourceManager(ResourceKind kind, ResourceManager resourceManager) :
			base(kind)
		{
			_resourceManager = resourceManager;
		}

		public override Resource Create(ResourceMetadata metadata, ResourceSpec spec)
		{
			var kind = new ResourceKind(null, null, null);
			var manager = new KindManager(kind);
			_resourceManager.CreateKind(kind, manager);
			return kind;
		}
	}
}
