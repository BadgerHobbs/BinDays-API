namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;

/// <summary>
/// Collector implementation for Worthing Borough Council.
/// </summary>
internal sealed class WorthingBoroughCouncil : AdurWorthingCollectorBase, ICollector
{
	/// <inheritdoc/>
	public override string Name => "Worthing Borough Council";

	/// <inheritdoc/>
	public override string GovUkId => "worthing";
}
