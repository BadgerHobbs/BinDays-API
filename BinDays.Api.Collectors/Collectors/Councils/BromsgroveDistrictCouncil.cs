namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using System;

/// <summary>
/// Collector implementation for Bromsgrove District Council.
/// </summary>
internal sealed class BromsgroveDistrictCouncil : BromsgroveRedditchCollectorBase, ICollector
{
	/// <inheritdoc/>
	public override string Name => "Bromsgrove District Council";

	/// <inheritdoc/>
	public override Uri WebsiteUrl => new("https://www.bromsgrove.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "bromsgrove";

	/// <inheritdoc/>
	protected override string BaseUrl => "https://bincollections.bromsgrove.gov.uk/";
}
