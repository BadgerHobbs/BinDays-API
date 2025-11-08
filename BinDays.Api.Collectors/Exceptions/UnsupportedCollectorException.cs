namespace BinDays.Api.Collectors.Exceptions
{
	using System;

	/// <summary>
	/// Exception thrown when a collector returned by gov.uk is not supported by BinDays.
	/// </summary>
	public sealed class UnsupportedCollectorException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="UnsupportedCollectorException"/> class.
		/// </summary>
		/// <param name="govUkId">The gov.uk identifier for the unsupported collector.</param>
		/// <param name="collectorName">The human-friendly collector name.</param>
		public UnsupportedCollectorException(string govUkId, string collectorName)
			: base($"Unsupported collector returned by gov.uk. Gov.uk ID: {govUkId}, Collector: {collectorName}")
		{
			GovUkId = govUkId;
			CollectorName = collectorName;
		}

		/// <summary>
		/// Gets the gov.uk identifier for the unsupported collector.
		/// </summary>
		public string GovUkId { get; }

		/// <summary>
		/// Gets the human-friendly collector name.
		/// </summary>
		public string CollectorName { get; }
	}
}
