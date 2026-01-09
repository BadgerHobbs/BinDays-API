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
	public Uri WebsiteUrl => new("https://www.whitehorsedc.gov.uk/java/support/formcall.jsp?F=BINZONE_DESKTOP");

	/// <inheritdoc/>
	public override string GovUkId => "vale-of-white-horse";

	/// <inheritdoc/>
	protected override string EformBaseUrl => "https://eform.whitehorsedc.gov.uk";

	/// <inheritdoc/>
	protected override string ServiceId => "VALE";
}
