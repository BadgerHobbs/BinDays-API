namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;

/// <summary>
/// Collector implementation for Newport City Council.
/// </summary>
internal sealed class NewportCityCouncil : ITouchVisionCollectorBase, ICollector
{
	/// <inheritdoc/>
	public string Name => "Newport City Council";

	/// <inheritdoc/>
	public Uri WebsiteUrl => new("https://www.newport.gov.uk/recycling-and-waste/collections/check-your-collection-day");

	/// <inheritdoc/>
	public override string GovUkId => "newport";

	/// <inheritdoc/>
	protected override int ClientId => 130;

	/// <inheritdoc/>
	protected override int CouncilId => 260;

	/// <inheritdoc/>
	protected override string ApiBaseUrl => "https://iweb.itouchvision.com/portal/itouchvision/";

	/// <inheritdoc/>
	protected override IReadOnlyCollection<Bin> BinTypes =>
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "HOUSEHOLD WASTE" ],
		},
		new()
		{
			Name = "Plastics & Metals Recycling",
			Colour = BinColour.Red,
			Keys = [ "RECYCLING" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Paper & Cardboard Recycling",
			Colour = BinColour.Blue,
			Keys = [ "RECYCLING" ],
			Type = BinType.Bag,
		},
		new()
		{
			Name = "Glass & Small Electricals Recycling",
			Colour = BinColour.Green,
			Keys = [ "RECYCLING" ],
			Type = BinType.Box,
		},
		new()
		{
			Name = "Food Waste",
			Colour = BinColour.Brown,
			Keys = [ "RECYCLING" ],
			Type = BinType.Caddy,
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Orange,
			Keys = [ "GARDEN WASTE" ],
		},
	];
}
