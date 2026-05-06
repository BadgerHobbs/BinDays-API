namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using System;

/// <summary>
/// Collector implementation for Vale of White Horse District Council.
/// </summary>
internal sealed class ValeOfWhiteHorseDistrictCouncil : BinzoneCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Vale of White Horse District Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.whitehorsedc.gov.uk/vale-of-white-horse-district-council/recycling-rubbish-and-waste/bindays/");

	/// <inheritdoc/>
	public override string GovUkId => "vale-of-white-horse";

	/// <inheritdoc/>
	protected override string CouncilCode => "V";
}
