﻿using HomeCtl.Events;
using HomeCtl.Kinds.Resources;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using ResourceDocument = HomeCtl.Kinds.Resources.ResourceDocument;

namespace HomeCtl.ApiServer.Resources
{
	class ResourceStateStore
	{
		private readonly ResourceManagerContainer _resourceManagers;
		private readonly Dictionary<string, ResourceState> _resourceIdentityIndex = new Dictionary<string, ResourceState>();

		public ResourceStateStore(
			ResourceManagerContainer resourceManagers
			)
		{
			_resourceManagers = resourceManagers;
		}

		private bool TryGetIdentity(ResourceDocument resourceDocument, [NotNullWhen(true)] out string? identity)
		{
			identity = resourceDocument.Definition["identity"]?.GetString();
			return identity != null;
		}

		public async Task LoadResources()
		{
			foreach (var resourceManager in _resourceManagers.Managers)
			{
				await resourceManager.Load(this);
			}
		}

		private bool TryGetResourceState(string identity, out ResourceState fullResourceState)
		{
			return _resourceIdentityIndex.TryGetValue(identity, out fullResourceState);
		}

		private void CommitResourceState(ResourceState resourceState)
		{
			_resourceIdentityIndex[resourceState.Identity] = resourceState;
		}

		private bool TryGetKindManager(ResourceDocument partialResourceDocument, [NotNullWhen(true)] out ResourceManager? kindManager)
		{
			if (partialResourceDocument.Kind == null)
			{
				kindManager = default;
				return false;
			}

			return _resourceManagers.TryFind(q => q.Kind.GetKindDescriptor().Equals(partialResourceDocument.Kind.Value), out kindManager);
		}

		private ResourceState CreateDefaultState(string identity, ResourceDocument partialResourceState)
		{
			if (!TryGetKindManager(partialResourceState, out var manager))
				throw new System.Exception("Valid kind required.");

			//  todo: create default document from kind schema
			var stateDoc = new ResourceDocument(
				new ResourceDefinition(new List<ResourceField>
				{
					new ResourceField("identity", ResourceFieldValue.String(identity))
				}),
				kind: manager.Kind.GetKindDescriptor());

			return new ResourceState(manager, identity, stateDoc);
		}

		private ResourceState ApplyFields(ResourceDocument partialResourceState, ResourceState existingState)
		{
			return new ResourceState(
				existingState.Manager,
				existingState.Identity,
				existingState.FullDocument.Patch(partialResourceState)
				);
		}

		public bool Validate(ResourceState resourceState)
		{
			return true;
		}

		public Task Load(ResourceDocument fullResourceState)
		{
			if (!TryGetIdentity(fullResourceState, out var identity))
			{
				throw new System.Exception("Identity field is required.");
			}

			if (!TryGetKindManager(fullResourceState, out var manager))
			{
				throw new System.Exception("Valid kind required.");
			}

			var newState = new ResourceState(
				manager, identity, fullResourceState
				);

			if (!Validate(newState))
			{
				throw new System.Exception("Invalid resource parameters specified.");
			}

			CommitResourceState(newState);

			return newState.Manager.Save(newState);
		}

		public Task Apply(ResourceDocument partialResourceState)
		{
			if (!TryGetIdentity(partialResourceState, out var identity))
			{
				throw new System.Exception("Identity field is required.");
			}

			if (!TryGetResourceState(identity, out var existingState))
			{
				existingState = CreateDefaultState(identity, partialResourceState);
			}

			//  ignore any state in case it was present
			partialResourceState.State = null;

			var newState = ApplyFields(partialResourceState, existingState);

			if (!Validate(newState))
			{
				throw new System.Exception("Invalid resource parameters specified.");
			}

			CommitResourceState(newState);

			return newState.Manager.Save(newState);
		}
	}
}
