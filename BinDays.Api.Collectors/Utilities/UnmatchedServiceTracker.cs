namespace BinDays.Api.Collectors.Utilities;

using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Records the council service names that matched none of a collector's bin types during a
/// single bin day lookup.
/// </summary>
/// <remarks>
/// Every collector maps council service names to bin types through
/// <see cref="ProcessingUtilities.GetMatchingBins"/>, which is the only point that sees each
/// service's raw text and whether it matched. Collectors then handle an unmatched service in
/// different ways (skipping it, leaving an empty bin day that is later merged away, or
/// throwing), so by the time their response reaches <c>CollectorService</c> it can no longer
/// tell what was lost. Recording at the point of matching makes the result independent of
/// how each collector handles it, without every collector having to report it explicitly.
/// The scope is held in an <see cref="AsyncLocal{T}"/>, so concurrent lookups never see each
/// other's services, and recording outside a scope does nothing.
/// </remarks>
internal static class UnmatchedServiceTracker
{
	/// <summary>
	/// The services recorded in the current lookup's scope, if one is active.
	/// </summary>
	private static readonly AsyncLocal<List<string>?> _currentServices = new();

	/// <summary>
	/// Starts recording unmatched services for the current lookup.
	/// </summary>
	/// <returns>The scope, which stops recording when disposed.</returns>
	public static Scope BeginScope()
	{
		var services = new List<string>();
		_currentServices.Value = services;

		return new Scope(services);
	}

	/// <summary>
	/// Records a service that matched no bin types, if a scope is active.
	/// </summary>
	/// <param name="service">The council's service name.</param>
	public static void Record(string service)
	{
		_currentServices.Value?.Add(service);
	}

	/// <summary>
	/// A lookup's recording scope.
	/// </summary>
	internal sealed class Scope : IDisposable
	{
		private readonly List<string> _services;

		/// <summary>
		/// Initializes a new instance of the <see cref="Scope"/> class.
		/// </summary>
		/// <param name="services">The list that services are recorded into.</param>
		public Scope(List<string> services)
		{
			_services = services;
		}

		/// <summary>
		/// Gets the unmatched services recorded so far, one entry per occurrence.
		/// </summary>
		public IReadOnlyList<string> Services => _services;

		/// <inheritdoc/>
		public void Dispose()
		{
			_currentServices.Value = null;
		}
	}
}
