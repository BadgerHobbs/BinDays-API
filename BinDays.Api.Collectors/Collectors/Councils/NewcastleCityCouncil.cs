namespace BinDays.Api.Collectors.Collectors.Councils;

using BinDays.Api.Collectors.Collectors.Vendors;
using BinDays.Api.Collectors.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// Collector implementation for Newcastle City Council.
/// </summary>
internal sealed class NewcastleCityCouncil : RecollectCollectorBase, ICollector
{
	/// <inheritdoc/>
	public override string Name => "Newcastle City Council";

	/// <inheritdoc/>
	public override Uri WebsiteUrl => new("https://new.newcastle.gov.uk/");

	/// <inheritdoc/>
	public override string GovUkId => "newcastle-upon-tyne";

	/// <inheritdoc/>
	protected override string AreaName => "NewcastleuponTyneUK";

	/// <inheritdoc/>
	protected override string ServiceId => "50007";

	/// <inheritdoc/>
	protected override IReadOnlyCollection<Bin> BinTypes =>
	[
		new()
		{
			Name = "General Waste",
			Colour = BinColour.Green,
			Keys = [ "GeneralWaste" ],
		},
		new()
		{
			Name = "Recycling",
			Colour = BinColour.Blue,
			Keys = [ "Recycling" ],
		},
		new()
		{
			Name = "Garden Waste",
			Colour = BinColour.Brown,
			Keys = [ "Garden" ],
		},
	];
}
