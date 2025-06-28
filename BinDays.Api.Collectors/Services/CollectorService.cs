namespace BinDays.Api.Collectors.Services
{
	using BinDays.Api.Collectors.Exceptions;
	using BinDays.Api.Collectors.Collectors;
	using System.Collections.ObjectModel;

	/// <summary>
	/// Service for returning specific or all collectors.
	/// </summary>
	public sealed class CollectorService
	{
		/// <summary>
		/// The list of collectors acquired via dependency injection.
		/// </summary>
		private readonly ReadOnlyCollection<ICollector> collectors;

		/// <summary>
		/// Initializes a new instance of the <see cref="CollectorService"/> class.
		/// </summary>
		/// <param name="collectors">The collectors.</param>
		/// <exception cref="ArgumentNullException">Thrown when collectors is null.</exception>
		public CollectorService(IEnumerable<ICollector> collectors)
		{
			this.collectors = new ReadOnlyCollection<ICollector>([.. collectors]);
		}

		/// <summary>
		/// Gets the collectors.
		/// </summary>
		/// <returns>The collectors.</returns>
		public ReadOnlyCollection<ICollector> GetCollectors()
		{
			return collectors;
		}

		/// <summary>
		/// Gets the collector for a given gov.uk identifier.        
		/// </summary>
		/// <param name="govUkId">The gov.uk identifier.</param>
		/// <returns>The collector if found.</returns>
		/// <exception cref="SupportedCollectorNotFoundException">Thrown when no collector matches the given govUkId.</exception>
		public ICollector GetCollector(string govUkId)
		{
			var collector = collectors.SingleOrDefault(collector => collector.GovUkId == govUkId);
			return collector ?? throw new SupportedCollectorNotFoundException(govUkId);
		}
	}
}
