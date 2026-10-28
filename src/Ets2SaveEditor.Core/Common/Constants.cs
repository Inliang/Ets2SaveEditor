namespace Ets2SaveEditor.Core.Common
{
    /// <summary>
    /// Shared constants for the Ets2SaveEditor Core library.
    /// </summary>
    public static class Constants
    {
        /// <summary>SII file header marker.</summary>
        public const string SiiHeader = "SiiNunit";

        // Unit type names (as used by the modern ETS2/ATS save format).
        public const string BankType = "bank";
        public const string GameType = "game";
        public const string PlayerType = "player";
        public const string EconomyType = "economy";
        public const string GarageType = "garage";
        public const string VehicleType = "vehicle";
        public const string TrailerType = "trailer";
        public const string JobOfferType = "job_offer_data";
        public const string JobInfoType = "job_info";
        public const string CompanyType = "company";
        public const string ProfitLogType = "profit_log";
        public const string DeliveryLogType = "delivery_log_entry";
        public const string BankLoanType = "bank_loan";

        // Frequently used attribute names of the modern save format.
        public const string BankMoneyAttribute = "money_account";
        public const string BankLoanLimitAttribute = "loan_limit";
        public const string BankLoansAttribute = "loans";
        public const string EconomyExperienceAttribute = "experience_points";
        public const string EconomyVisitedCitiesAttribute = "visited_cities";

        /// <summary>Backup file extension prefix.</summary>
        public const string BackupExtension = ".bak";

        /// <summary>
        /// Timestamp format used for backup file names (yyyyMMddHHmmssfff).
        /// Millisecond precision plus a numeric suffix keeps every backup name
        /// unique — two saves within the same second must not overwrite each
        /// other.
        /// </summary>
        public const string BackupTimestampFormat = "yyyyMMddHHmmssfff";

        /// <summary>SII file extension.</summary>
        public const string SiiFileExtension = ".sii";
    }
}
