using System;
using System.Collections.Generic;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.GameData;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain.TypedUnits
{
    /// <summary>
    /// Typed wrapper around the <c>job_offer_data</c> unit.
    /// <para>
    /// A job offer of the modern save format describes the cargo market entry
    /// rather than a concrete job: it holds the destination city
    /// (<c>target</c>), the cargo (<c>cargo</c>), the company truck
    /// (<c>company_truck</c>), the urgency and the distance.
    /// </para>
    /// </summary>
    public class JobOffer
    {
        private readonly SiiUnit _unit;
        private readonly SiiDocument _doc;

        /// <summary>The underlying SII unit.</summary>
        public SiiUnit Unit => _unit;

        public JobOffer(SiiUnit unit, SiiDocument doc)
        {
            _unit = unit;
            _doc = doc;
        }

        /// <summary>
        /// Destination company of the offer (<c>target</c>), in the short form
        /// the game uses, e.g. <c>stokes.erfurt</c>.
        /// </summary>
        public string Target
        {
            get { return _unit != null ? _unit.GetString("target") : string.Empty; }
            set { if (_unit != null) _unit.SetScalarPreserveStyle("target", value); }
        }

        /// <summary>
        /// City of the destination company, e.g. <c>erfurt</c>. The target
        /// reference has the form <c>&lt;brand&gt;.&lt;city&gt;</c>.
        /// </summary>
        public string TargetCity
        {
            get
            {
                string target = Target;
                if (string.IsNullOrEmpty(target)) return string.Empty;
                int dot = target.LastIndexOf('.');
                return dot >= 0 && dot < target.Length - 1 ? target.Substring(dot + 1) : target;
            }
        }

        /// <summary>
        /// Brand of the destination company, e.g. <c>stokes</c>.
        /// </summary>
        public string TargetBrand
        {
            get
            {
                string target = Target;
                if (string.IsNullOrEmpty(target)) return string.Empty;
                int dot = target.LastIndexOf('.');
                return dot > 0 ? target.Substring(0, dot) : target;
            }
        }

        /// <summary>Cargo of the offer (<c>cargo</c>, an id such as <c>cargo.haulie</c>).</summary>
        public string Cargo
        {
            get { return _unit != null ? _unit.GetReference("cargo") : string.Empty; }
            set { if (_unit != null) _unit.SetScalarPreserveStyle("cargo", value); }
        }

        /// <summary>Company truck of the offer (<c>company_truck</c>).</summary>
        public string Truck
        {
            get { return _unit != null ? _unit.GetString("company_truck") : string.Empty; }
            set { if (_unit != null) _unit.SetScalarPreserveStyle("company_truck", value); }
        }

        /// <summary>Trailer variant of the offer (<c>trailer_variant</c>).</summary>
        public string TrailerVariant
        {
            get { return _unit != null ? _unit.GetReference("trailer_variant") : string.Empty; }
            set { if (_unit != null) _unit.SetScalarPreserveStyle("trailer_variant", value); }
        }

        /// <summary>Trailer definition of the offer (<c>trailer_definition</c>).</summary>
        public string TrailerDefinition
        {
            get { return _unit != null ? _unit.GetReference("trailer_definition") : string.Empty; }
            set { if (_unit != null) _unit.SetScalarPreserveStyle("trailer_definition", value); }
        }

        /// <summary>Urgency of the offer (0 = normal, higher = more urgent).</summary>
        public int Urgency
        {
            get { return _unit != null ? (int)_unit.GetLong("urgency") : 0; }
            set { if (_unit != null) _unit.SetScalar("urgency", (long)value); }
        }

        /// <summary>Game time at which the offer expires.</summary>
        public long ExpirationTime
        {
            get { return _unit != null ? _unit.GetLong("expiration_time") : 0; }
            set { if (_unit != null) _unit.SetScalar("expiration_time", value); }
        }

        /// <summary>Shortest distance to the destination in kilometres.</summary>
        public int DistanceKm
        {
            get { return _unit != null ? (int)_unit.GetLong("shortest_distance_km") : 0; }
            set { if (_unit != null) _unit.SetScalar("shortest_distance_km", (long)value); }
        }

        /// <summary>Ferry crossing time.</summary>
        public int FerryTime
        {
            get { return _unit != null ? (int)_unit.GetLong("ferry_time") : 0; }
            set { if (_unit != null) _unit.SetScalar("ferry_time", (long)value); }
        }

        /// <summary>Ferry crossing price.</summary>
        public int FerryPrice
        {
            get { return _unit != null ? (int)_unit.GetLong("ferry_price") : 0; }
            set { if (_unit != null) _unit.SetScalar("ferry_price", (long)value); }
        }

        /// <summary>Number of cargo units offered.</summary>
        public int UnitsCount
        {
            get { return _unit != null ? (int)_unit.GetLong("units_count") : 0; }
            set { if (_unit != null) _unit.SetScalar("units_count", (long)value); }
        }

        /// <summary>Fill ratio of the offered cargo (0..1).</summary>
        public double FillRatio
        {
            get { return _unit != null ? _unit.GetDouble("fill_ratio") : 0; }
            set { if (_unit != null) _unit.SetFloat("fill_ratio", value); }
        }

        /// <summary>
        /// Estimated income of the offer in euros.
        /// <para>
        /// The modern save format does not store the income of a job offer —
        /// the game computes it from cargo, distance and urgency. This
        /// property reproduces that computation so the player can see which
        /// offers are worth taking.
        /// </para>
        /// </summary>
        public long EstimatedIncome
        {
            get
            {
                return JobIncomeEstimator.Estimate(
                    Cargo, DistanceKm, UnitsCount, Urgency, FillRatio);
            }
        }

        /// <summary>Estimated income of the offer, formatted for display.</summary>
        public string EstimatedIncomeText => JobIncomeEstimator.Format(EstimatedIncome);

        /// <summary>
        /// Estimated income of the offer per kilometre — the figure that makes
        /// offers comparable regardless of their length.
        /// </summary>
        public double EstimatedIncomePerKm =>
            JobIncomeEstimator.EstimatePerKm(Cargo, UnitsCount, Urgency);

        /// <summary>Estimated income per kilometre, formatted for display.</summary>
        public string EstimatedIncomePerKmText =>
            JobIncomeEstimator.FormatPerKm(EstimatedIncomePerKm);

        /// <summary>
        /// Name of the target city (resolved through the city catalogue).
        /// </summary>
        public string TargetCityName => CityCatalogue.DisplayName(TargetCity);
        /// <summary>
        /// Name of the cargo (resolved through the cargo catalogue).
        /// </summary>
        public string CargoName => CargoCatalogue.DisplayName(Cargo);

        /// <summary>
        /// Display name of the receiving company (e.g. <c>Stokes</c>).
        /// <para>
        /// The offer stores the destination as <c>&lt;brand&gt;.&lt;city&gt;</c>.
        /// The brand part is resolved through the brand table extracted from
        /// the game data files; when that fails the token is shown in a
        /// readable form instead.
        /// </para>
        /// </summary>
        public string TargetCompanyName
        {
            get
            {
                string brand = TargetBrand;
                if (string.IsNullOrEmpty(brand)) return string.Empty;

                // 品牌表里的键是 company.permanent.<brand>
                string name = GameData.GameNames.Lookup(
                    GameData.GameNames.Company, "company.permanent." + brand);
                if (!string.IsNullOrEmpty(name)) return name;

                return Titleize(brand);
            }
        }

        /// <summary>
        /// Display name of the company truck (e.g. <c>DAF 6x2 A</c>).
        /// <para>
        /// The save file stores a model token such as <c>daf_6x2_a</c>. The
        /// game's own model names are not part of the save file, so the token
        /// is turned into a readable form: the manufacturer is separated from
        /// the axle layout.
        /// </para>
        /// </summary>
        public string TruckName
        {
            get
            {
                string truck = Truck;
                if (string.IsNullOrEmpty(truck)) return string.Empty;

                string manufacturer = truck;
                string rest = string.Empty;
                int underscore = truck.IndexOf('_');
                if (underscore > 0)
                {
                    manufacturer = truck.Substring(0, underscore);
                    rest = truck.Substring(underscore + 1);
                }

                string result = Titleize(manufacturer);
                if (!string.IsNullOrEmpty(rest)) result += " " + Titleize(rest);
                return result;
            }
        }

        /// <summary>
        /// Turns a token into a readable form: underscores become spaces and
        /// every word starts with a capital letter.
        /// </summary>
        private static string Titleize(string token)
        {
            if (string.IsNullOrEmpty(token)) return string.Empty;

            var parts = token.Split('_');
            var builder = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                if (builder.Length > 0) builder.Append(' ');
                builder.Append(char.ToUpperInvariant(part[0]));
                builder.Append(part.Substring(1));
            }
            return builder.ToString();
        }

        /// <summary>
        /// Creates a new <c>job_offer_data</c> unit with the complete field
        /// set of the modern format and appends it to the document.
        /// <para>
        /// The unit is created but <em>not</em> registered with a company, so
        /// the game will not show it. Use
        /// <see cref="CreateAndRegister"/> to create a job that actually
        /// shows up in the freight market.
        /// </para>
        /// </summary>
        public static JobOffer Create(SiiDocument doc, string id, string targetCity, string cargo, string truck)
        {
            if (doc == null) return null;
            if (string.IsNullOrEmpty(id))
            {
                id = CreateNamelessId();
            }

            var unit = new SiiUnit(Constants.JobOfferType, id);
            unit.SetAttribute("target", SiiUnit.CreateTextValue(targetCity ?? string.Empty));
            unit.SetScalar("expiration_time", 0L);
            unit.SetScalar("urgency", 0L);
            unit.SetScalar("shortest_distance_km", 0L);
            unit.SetScalar("ferry_time", 0L);
            unit.SetScalar("ferry_price", 0L);
            unit.SetScalar("cargo", new ReferenceValue(cargo ?? string.Empty));
            unit.SetAttribute("company_truck", SiiUnit.CreateTextValue(truck ?? string.Empty));
            unit.SetScalar("trailer_variant", new ReferenceValue(string.Empty));
            unit.SetScalar("trailer_definition", new ReferenceValue(string.Empty));
            unit.SetScalar("units_count", 0L);
            unit.SetFloat("fill_ratio", 0.0);
            unit.SetArrayItems("trailer_place", new List<SiiValue>());

            doc.AddUnit(unit);
            return new JobOffer(unit, doc);
        }

        /// <summary>
        /// Creates a custom job offer that the game will actually display.
        /// <para>
        /// The modern save format stores the freight market as a two-way
        /// relation: a <c>job_offer_data</c> unit describes the offer and
        /// the owning company references it in its <c>job_offer</c> array.
        /// An offer that no company references is invisible to the game, so
        /// this method does both: it creates the offer unit and registers it
        /// with the given company (and marks the company as discovered when
        /// it was not yet).
        /// </para>
        /// </summary>
        /// <param name="doc">Document to add the offer to.</param>
        /// <param name="company">Company that publishes the offer.</param>
        /// <param name="cargo">Cargo token, e.g. <c>cargo.haulie</c>.</param>
        /// <param name="truck">Company truck model id.</param>
        /// <param name="distanceKm">Distance to the destination in kilometres.</param>
        /// <param name="units">Number of cargo units offered.</param>
        /// <param name="urgency">Urgency level (0..3).</param>
        /// <returns>The new offer, or null when the arguments are invalid.</returns>
        public static JobOffer CreateAndRegister(
            SiiDocument doc,
            Company company,
            string cargo,
            string truck,
            int distanceKm,
            int units,
            int urgency)
        {
            if (doc == null || company == null) return null;

            // The target of an offer is the short reference of the company
            // that receives the goods, e.g. "stokes.erfurt".
            var offer = Create(doc, null, company.ShortReference, cargo, truck);
            if (offer == null) return null;

            offer.DistanceKm = distanceKm;
            offer.UnitsCount = units;
            offer.Urgency = urgency;
            offer.FillRatio = 1.0;

            company.AddJobOffer(offer.Unit.Id);
            if (!company.Discovered)
            {
                company.Discovered = true;
            }

            return offer;
        }

        /// <summary>
        /// Removes the offer from the document and unregisters it from every
        /// company that references it.
        /// </summary>
        public bool Remove()
        {
            if (_unit == null || _doc == null) return false;

            foreach (var company in Company.AllFromDocument(_doc))
            {
                company.RemoveJobOffer(_unit.Id);
            }

            _doc.RemoveUnit(_unit);
            return true;
        }

        /// <summary>
        /// Builds an identifier in the nameless format the game uses
        /// (<c>_nameless.1b7.3c41.5d68</c>).
        /// </summary>
        private static string CreateNamelessId()
        {
            byte[] bytes = new byte[6];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            uint high = (uint)(bytes[0] | (bytes[1] << 8));
            ushort mid = (ushort)(bytes[2] | (bytes[3] << 8));
            ushort low = (ushort)(bytes[4] | (bytes[5] << 8));

            return "_nameless." + high.ToString("x", System.Globalization.CultureInfo.InvariantCulture) +
                   "." + mid.ToString("x4", System.Globalization.CultureInfo.InvariantCulture) +
                   "." + low.ToString("x4", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static List<JobOffer> AllFromDocument(SiiDocument doc)
        {
            var list = new List<JobOffer>();
            if (doc == null) return list;
            foreach (var u in doc.GetUnitsByType(Constants.JobOfferType))
            {
                list.Add(new JobOffer(u, doc));
            }
            return list;
        }
    }
}
