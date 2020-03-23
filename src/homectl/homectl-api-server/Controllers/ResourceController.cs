﻿using homectl_api_server.Application;
using homectl_api_server.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;

namespace homectl_api_server.Controllers
{
	[ApiController]
	[Route("~/apis/{group}/{apiVersion}/{kind}")]
	public class ResourceController : Microsoft.AspNetCore.Mvc.Controller
	{
		[HttpGet]
		[Consumes(MediaTypeNames.Application.Json)]
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public ActionResult<IEnumerable<ResourceDetails>> GetMany(
			[FromRoute] string group,
			[FromRoute] string apiVersion,
			[FromRoute] string kind,
			[FromServices] ResourceManager resourceManager
			)
		{
			var resourceKind = resourceManager.GetKind(
				group, apiVersion, kind);

			if (resourceKind == default)
				return NotFound();

			var resources = resourceKind.GetAll();

			return resources.Select(q => new ResourceDetails
			{
				ApiVersion = $"{resourceKind.Kind.Group}/{resourceKind.Kind.ApiVersion}",
				Kind = resourceKind.Kind.KindName,
				Metadata = q.Metadata,
				Spec = q.Spec,
				State = q.State
			}).ToList();
		}

		[HttpGet("{identifier:guid}")]
		[Consumes(MediaTypeNames.Application.Json)]
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public ActionResult<ResourceDetails> GetSingle(
			[FromRoute] string group,
			[FromRoute] string apiVersion,
			[FromRoute] string kind,
			[FromRoute] Guid identifier,
			[FromServices] ResourceManager resourceManager
			)
		{
			var resourceKind = resourceManager.GetKind(
				group, apiVersion, kind);

			if (resourceKind == default)
				return NotFound();

			var resource = resourceKind.GetSingle(identifier);
			if (resource == default)
				return NotFound();

			return new ResourceDetails
			{
				ApiVersion = $"{resourceKind.Kind.Group}/{resourceKind.Kind.ApiVersion}",
				Kind = resourceKind.Kind.KindName,
				Metadata = resource.Metadata,
				Spec = resource.Spec,
				State = resource.State
			};
		}

		[HttpPost]
		[Consumes(MediaTypeNames.Application.Json)]
		[ProducesResponseType(StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public ActionResult<ResourceDetails> Create(
			[FromRoute] string group,
			[FromRoute] string apiVersion,
			[FromRoute] string kind,
			[FromBody] ResourceManifest manifest,
			[FromServices] ResourceManager resourceManager
			)
		{
			var resourceKind = resourceManager.GetKind(
				group, apiVersion, kind);

			if (resourceKind == default)
				return NotFound();

			//  todo: get valiation and creation errors out of this API call and return them to the caller on failure
			if (!resourceKind.Kind.Spec.Validate(manifest.Spec))
				return BadRequest();

			var resource = resourceKind.Create(manifest.Metadata, manifest.Spec);
			if (resource == default)
				return NotFound();

			return CreatedAtAction(nameof(GetSingle), new { identifier = resource.Metadata.Id }, new ResourceDetails
			{
				ApiVersion = $"{resourceKind.Kind.Group}/{resourceKind.Kind.ApiVersion}",
				Kind = resourceKind.Kind.KindName,
				Metadata = resource.Metadata,
				Spec = resource.Spec,
				State = resource.State
			});
		}

		[HttpPut("{identifier:guid}")]
		[Consumes(MediaTypeNames.Application.Json)]
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public ActionResult<ResourceDetails> UpdateFull(
			[FromRoute] string group,
			[FromRoute] string apiVersion,
			[FromRoute] string kind,
			[FromRoute] Guid identifier,
			[FromBody] ResourceManifest manifest,
			[FromServices] ResourceManager resourceManager
			)
		{
			var resourceKind = resourceManager.GetKind(
				group, apiVersion, kind);

			if (resourceKind == default)
				return NotFound();

			var resource = resourceKind.GetSingle(identifier);
			if (resource == default)
				return NotFound();

			if (!resourceKind.Kind.Spec.Validate(manifest.Spec))
				return BadRequest();

			resourceKind.UpdateSpec(resource, manifest.Metadata, manifest.Spec);

			return new ResourceDetails
			{
				ApiVersion = $"{resourceKind.Kind.Group}/{resourceKind.Kind.ApiVersion}",
				Kind = resourceKind.Kind.KindName,
				Metadata = resource.Metadata,
				Spec = resource.Spec,
				State = resource.State
			};
		}

		[HttpPatch("{identifier:guid}")]
		[Consumes(MediaTypeNames.Application.Json)]
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public ActionResult<ResourceDetails> UpdatePatch(
			[FromRoute] string group,
			[FromRoute] string apiVersion,
			[FromRoute] string kind,
			[FromRoute] Guid identifier,
			[FromBody] JsonPatchDocument<ResourceManifest> patchDocument,
			[FromServices] ResourceManager resourceManager
			)
		{
			var resourceKind = resourceManager.GetKind(
				group, apiVersion, kind);

			if (resourceKind == default)
				return NotFound();

			var resource = resourceKind.GetSingle(identifier);
			if (resource == default)
				return NotFound();

			var manifest = new ResourceManifest
			{
				Metadata = resource.Metadata,
				Spec = resource.Spec
			};
			patchDocument.ApplyTo(manifest, ModelState);
			if (!ModelState.IsValid || !resourceKind.Kind.Spec.Validate(manifest.Spec))
				return BadRequest(ModelState);

			resourceKind.UpdateSpec(resource, manifest.Metadata, manifest.Spec);

			return new ResourceDetails
			{
				ApiVersion = $"{resourceKind.Kind.Group}/{resourceKind.Kind.ApiVersion}",
				Kind = resourceKind.Kind.KindName,
				Metadata = resource.Metadata,
				Spec = resource.Spec,
				State = resource.State
			};
		}

		[HttpDelete("{identifier:guid}")]
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public IActionResult Delete(
			[FromRoute] string group,
			[FromRoute] string apiVersion,
			[FromRoute] string kind,
			[FromRoute] Guid identifier,
			[FromServices] ResourceManager resourceManager
			)
		{
			var resourceKind = resourceManager.GetKind(
				group, apiVersion, kind);

			if (resourceKind == default)
				return NotFound();

			var resource = resourceKind.GetSingle(identifier);
			if (resource == default)
				return NotFound();

			resourceKind.Remove(resource);

			return Ok();
		}

		public class ResourceDetails
		{
			public string ApiVersion { get; set; }

			public string Kind { get; set; }

			public ResourceMetadata Metadata { get; set; }

			public ResourceSpec Spec { get; set; }

			public ResourceState State { get; set; }
		}

		public class ResourceManifest
		{
			public ResourceMetadata Metadata { get; set; }

			public ResourceSpec Spec { get; set; }
		}
	}
}
