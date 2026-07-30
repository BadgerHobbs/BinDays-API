namespace BinDays.Api.Collectors.Exceptions;

using System;

/// <summary>
/// Exception thrown when a collector returns bin days but none of them matched any bin type.
/// This indicates the collector's bin type keys are out of date (e.g. the council renamed or
/// added a collection service), not that the address genuinely has no scheduled collections.
/// </summary>
public sealed class AllBinDaysUnmatchedException : Exception
{
	/// <summary>
	/// The gov.uk identifier for the collector.
	/// </summary>
	public string GovUkId { get; }

	/// <summary>
	/// The postcode for the address.
	/// </summary>
	public string Postcode { get; }

	/// <summary>
	/// The unique identifier for the address.
	/// </summary>
	public string Uid { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="AllBinDaysUnmatchedException"/> class.
	/// </summary>
	public AllBinDaysUnmatchedException(string govUkId, string postcode, string uid)
		: base($"All bin days matched no bin types for gov.uk ID: {govUkId}, postcode: {postcode}, UID: {uid}")
	{
		GovUkId = govUkId;
		Postcode = postcode;
		Uid = uid;
	}

}
