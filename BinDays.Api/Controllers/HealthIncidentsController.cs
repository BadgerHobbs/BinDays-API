namespace BinDays.Api.Controllers;

using BinDays.Api.Incidents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

/// <summary>
/// Provides health-focused incident feeds.
/// </summary>
[ApiController]
[Route("health")]
public sealed class HealthIncidentsController : ControllerBase
{
	private readonly IIncidentStore _incidentStore;
	private readonly ILogger<HealthIncidentsController> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="HealthIncidentsController"/> class.
	/// </summary>
	/// <param name="incidentStore">The incident store.</param>
	/// <param name="logger">Logger instance.</param>
	public HealthIncidentsController(IIncidentStore incidentStore, ILogger<HealthIncidentsController> logger)
	{
		_incidentStore = incidentStore;
		_logger = logger;
	}

	/// <summary>
	/// Gets all recorded incidents ordered from newest to oldest.
	/// </summary>
	/// <returns>The incidents.</returns>
	[HttpGet("incidents")]
	public ActionResult<IReadOnlyList<IncidentRecord>> GetIncidents()
	{
		try
		{
			return Ok(_incidentStore.GetIncidents());
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to retrieve incidents.");
			return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while fetching incidents. Please try again later.");
		}
	}
}
