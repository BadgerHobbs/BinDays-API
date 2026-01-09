namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using System;

/// <summary>
/// Collector implementation for South Oxfordshire District Council.
/// </summary>
internal sealed class SouthOxfordshireDistrictCouncil : BinzoneCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "South Oxfordshire District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.southoxon.gov.uk/south-oxfordshire-district-council/recycling-rubbish-and-waste/when-is-your-collection-day/");

	/// <inheritdoc/>
	public override string GovUkId => "south-oxfordshire";

	/// <inheritdoc/>
	protected override string EformBaseUrl => "https://eform.southoxon.gov.uk";

	/// <inheritdoc/>
	protected override string ServiceId => "SOUTH";
}
