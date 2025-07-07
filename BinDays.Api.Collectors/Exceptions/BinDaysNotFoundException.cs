namespace BinDays.Api.Collectors.Exceptions
{
    using System;

    /// <summary>
    /// Exception thrown when no bin days are found for a given address.
    /// </summary>
    public sealed class BinDaysNotFoundException : Exception
    {
        /// <summary>
        /// The gov.uk identifier for the collector.
        /// </summary>
        public string GovUkId { get; }

        /// <summary>
        /// The postcode for which bin days were not found.
        /// </summary>
        public string Postcode { get; }

        /// <summary>
        /// The unique identifier for the address.
        /// </summary>
        public string Uid { get; }

        public BinDaysNotFoundException(string govUkId, string postcode, string uid)
            : base($"No bin days found for gov.uk ID: {govUkId}, postcode: {postcode}, UID: {uid}")
        {
            GovUkId = govUkId;
            Postcode = postcode;
            Uid = uid;
        }
    
    }
}