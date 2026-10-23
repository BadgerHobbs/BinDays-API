namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using System;

/// <summary>
/// Collector implementation for Redditch Borough Council.
/// </summary>
internal sealed class RedditchBoroughCouncil : BromsgroveRedditchCollectorBase, ICollector
{
	/// <inheritdoc/>
	public override string Name => "Redditch Borough Council";

	/// <inheritdoc/>
	public override Uri WebsiteUrl => new("https://www.redditchbc.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "redditch";

	/// <inheritdoc/>
	protected override string BaseUrl => "https://bincollections.redditchbc.gov.uk/";
}
