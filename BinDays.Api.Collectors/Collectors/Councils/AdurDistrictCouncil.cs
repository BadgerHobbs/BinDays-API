namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;

/// <summary>
/// Collector implementation for Adur District Council.
/// </summary>
internal sealed class AdurDistrictCouncil : AdurWorthingCollectorBase, ICollector
{
	/// <inheritdoc/>
	public override string Name => "Adur District Council";

	/// <inheritdoc/>
	public override string GovUkId => "adur";
}
