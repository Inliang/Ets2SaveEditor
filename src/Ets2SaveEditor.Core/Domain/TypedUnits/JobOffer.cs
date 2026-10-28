using System;
using System.Collections.Generic;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain.TypedUnits
{
    /// <summary>
    /// Typed wrapper around the <c>job_offer_data</c> unit.
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

        public string SourceCity
        {
            get { return _unit != null ? _unit.GetString("source_city") : string.Empty; }
            set { if (_unit != null) _unit.SetScalar("source_city", value); }
        }

        public string TargetCity
        {
            get { return _unit != null ? _unit.GetString("target_city") : string.Empty; }
            set { if (_unit != null) _unit.SetScalar("target_city", value); }
        }

        public string Cargo
        {
            get { return _unit != null ? _unit.GetString("cargo") : string.Empty; }
            set { if (_unit != null) _unit.SetScalar("cargo", value); }
        }

        public long Reward
        {
            get { return _unit != null ? _unit.GetLong("reward") : 0; }
            set { if (_unit != null) _unit.SetScalar("reward", value); }
        }

        /// <summary>
        /// Creates a new <c>job_offer_data</c> unit, appends it to the document
        /// and returns a wrapper around it.
        /// </summary>
        public static JobOffer Create(SiiDocument doc, string id, string sourceCity, string targetCity, string cargo, long reward)
        {
            if (doc == null) return null;
            if (string.IsNullOrEmpty(id))
            {
                id = "_job_offer." + Guid.NewGuid().ToString("N").Substring(0, 8);
            }

            var unit = new SiiUnit(Constants.JobOfferType, id);
            unit.SetScalar("source_city", sourceCity ?? string.Empty);
            unit.SetScalar("target_city", targetCity ?? string.Empty);
            unit.SetScalar("cargo", cargo ?? string.Empty);
            unit.SetScalar("reward", reward);
            doc.Units.Add(unit);
            return new JobOffer(unit, doc);
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
