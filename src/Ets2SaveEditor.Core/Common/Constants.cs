namespace Ets2SaveEditor.Core.Common
{
    /// <summary>
    /// Shared constants for the Ets2SaveEditor Core library.
    /// </summary>
    public static class Constants
    {
        /// <summary>SII file header marker.</summary>
        public const string SiiHeader = "SiiNunit";

        // Unit type names.
        public const string BankType = "bank";
        public const string GameType = "game";
        public const string PlayerType = "player";
        public const string GarageType = "garage";
        public const string VehicleType = "vehicle";
        public const string TrailerType = "trailer";
        public const string JobOfferType = "job_offer_data";
        public const string EconomyType = "economy";

        /// <summary>Backup file extension prefix.</summary>
        public const string BackupExtension = ".bak";

        /// <summary>Timestamp format used for backup file names (yyyyMMddHHmmss).</summary>
        public const string BackupTimestampFormat = "yyyyMMddHHmmss";

        /// <summary>SII file extension.</summary>
        public const string SiiFileExtension = ".sii";
    }
}
