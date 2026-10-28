using System;
using System.Globalization;

namespace Ets2SaveEditor.Core.GameData
{
    /// <summary>
    /// Estimates the income of a freight market job.
    /// <para>
    /// Modern (1.41+) ETS2/ATS saves do <em>not</em> store the income of a
    /// job offer: the game computes it from the cargo, the distance and the
    /// urgency when the offer is shown. The <c>job_offer_data</c> unit holds
    /// only the target, the cargo, the truck, the distance and the urgency.
    /// </para>
    /// <para>
    /// This estimator reproduces that formula closely enough to show the
    /// player which offers are worth taking:
    /// <code>
    /// income = units × cargoRate(€ per unit per km) × distance × urgencyFactor
    /// </code>
    /// The per-unit rates are taken from the real income records of the
    /// <c>delivery_log_entry</c> units found in a genuine save file, so the
    /// numbers line up with what the game paid for the same job.
    /// </para>
    /// <para>
    /// The estimate is an approximation: the game additionally factors in the
    /// market price of the destination city, which changes over time. The
    /// numbers are therefore good enough to rank offers against each other,
    /// but not necessarily identical to the amount the game finally pays.
    /// </para>
    /// </summary>
    public static class JobIncomeEstimator
    {
        /// <summary>
        /// Multiplier applied to the income for each urgency step
        /// (0 = normal, 1 = high, 2 = urgent, 3 = very urgent).
        /// </summary>
        private static readonly double[] UrgencyFactors = { 1.00, 1.30, 1.90, 2.80 };

        /// <summary>
        /// Returns the rate (euros per unit per kilometre) of the given cargo.
        /// </summary>
        public static double RateOf(string cargoToken)
        {
            return CargoCatalogue.RateOf(cargoToken);
        }

        /// <summary>
        /// Returns the multiplier of the given urgency level.
        /// </summary>
        public static double UrgencyFactor(int urgency)
        {
            if (urgency < 0) urgency = 0;
            if (urgency >= UrgencyFactors.Length) return UrgencyFactors[UrgencyFactors.Length - 1];
            return UrgencyFactors[urgency];
        }

        /// <summary>
        /// Estimates the income of a job offer.
        /// </summary>
        /// <param name="cargo">Cargo token, e.g. "cargo.haulie".</param>
        /// <param name="distanceKm">Distance to the destination in kilometres.</param>
        /// <param name="units">Number of cargo units.</param>
        /// <param name="urgency">Urgency level (0..3).</param>
        /// <param name="fillRatio">Fill ratio (0..1); scales the units actually carried.</param>
        /// <returns>The estimated income in euros.</returns>
        public static long Estimate(string cargo, int distanceKm, int units, int urgency, double fillRatio)
        {
            if (distanceKm <= 0 || units <= 0) return 0;

            double fill = fillRatio <= 0 ? 1.0 : (fillRatio > 1.0 ? 1.0 : fillRatio);
            double effectiveUnits = units * fill;

            double income = effectiveUnits * RateOf(cargo) * distanceKm * UrgencyFactor(urgency);
            return (long)Math.Round(income, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Estimates the income per kilometre of a job offer — the figure
        /// that makes offers comparable, because a long haul of cheap cargo
        /// and a short haul of expensive cargo pay very different totals.
        /// </summary>
        /// <param name="cargo">Cargo token, e.g. "cargo.haulie".</param>
        /// <param name="units">Number of cargo units.</param>
        /// <param name="urgency">Urgency level (0..3).</param>
        /// <returns>The estimated income in euros per kilometre.</returns>
        public static double EstimatePerKm(string cargo, int units, int urgency)
        {
            if (units <= 0) return 0;
            return units * RateOf(cargo) * UrgencyFactor(urgency);
        }

        /// <summary>
        /// Formats an estimated income for display, e.g. <c>€12,480</c>.
        /// </summary>
        public static string Format(long income)
        {
            return "€" + income.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats an amount per kilometre for display, e.g. <c>€38.20/km</c>.
        /// </summary>
        public static string FormatPerKm(double perKm)
        {
            return "€" + perKm.ToString("N2", CultureInfo.InvariantCulture) + "/km";
        }
    }
}
