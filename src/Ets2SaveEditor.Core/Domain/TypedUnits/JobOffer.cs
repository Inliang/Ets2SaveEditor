using System;
using System.Collections.Generic;
using Ets2SaveEditor.Core.Common;
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

        /// <summary>Destination city of the offer (<c>target</c>).</summary>
        public string TargetCity
        {
            get { return _unit != null ? _unit.GetString("target") : string.Empty; }
            set { if (_unit != null) _unit.SetScalarPreserveStyle("target", value); }
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
        /// Creates a new <c>job_offer_data</c> unit with the complete field
        /// set of the modern format and appends it to the document.
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

            doc.Units.Add(unit);
            return new JobOffer(unit, doc);
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
