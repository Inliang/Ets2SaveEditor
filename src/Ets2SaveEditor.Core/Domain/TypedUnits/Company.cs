using System;
using System.Collections.Generic;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain.TypedUnits
{
    /// <summary>
    /// Typed wrapper around the <c>company</c> unit.
    /// <para>
    /// A company is the freight company of a city (its id is
    /// <c>company.volatile.&lt;brand&gt;.&lt;city&gt;</c>). It owns the job
    /// offers the player can accept there: the <c>job_offer</c> array holds
    /// the ids of the <c>job_offer_data</c> units it publishes. A
    /// <c>job_offer_data</c> unit that is not referenced by any company is
    /// invisible to the game, so creating a custom job means creating the
    /// offer <em>and</em> registering it with a company.
    /// </para>
    /// </summary>
    public class Company
    {
        private readonly SiiUnit _unit;

        /// <summary>The underlying SII unit.</summary>
        public SiiUnit Unit => _unit;

        public Company(SiiUnit unit)
        {
            _unit = unit;
        }

        /// <summary>Full identifier, e.g. <c>company.volatile.stokes.erfurt</c>.</summary>
        public string Id => _unit != null ? _unit.Id : string.Empty;

        /// <summary>
        /// Brand part of the identifier (e.g. <c>stokes</c>). The city part
        /// is the last segment of the identifier.
        /// </summary>
        public string Brand
        {
            get
            {
                string id = Id;
                if (string.IsNullOrEmpty(id)) return string.Empty;
                string[] parts = id.Split('.');
                return parts.Length >= 4 ? parts[2] : string.Empty;
            }
        }

        /// <summary>
        /// City part of the identifier (e.g. <c>erfurt</c>).
        /// </summary>
        public string CityToken
        {
            get
            {
                string id = Id;
                if (string.IsNullOrEmpty(id)) return string.Empty;
                int dot = id.LastIndexOf('.');
                return dot >= 0 && dot < id.Length - 1 ? id.Substring(dot + 1) : string.Empty;
            }
        }

        /// <summary>
        /// The short target reference used by the <c>target</c> attribute of a
        /// job offer: <c>&lt;brand&gt;.&lt;city&gt;</c>
        /// (e.g. <c>stokes.erfurt</c>).
        /// </summary>
        public string ShortReference => Brand + "." + CityToken;

        /// <summary>
        /// Identifier of the company's permanent definition, e.g.
        /// <c>company.permanent.acc</c> (from the <c>permanent_data</c> attribute).
        /// </summary>
        public string PermanentDataId
        {
            get { return _unit != null ? _unit.GetReference("permanent_data") : string.Empty; }
        }

        /// <summary>
        /// Display name of the company (e.g. <c>ACC</c>).
        /// <para>
        /// The save file only stores identifiers; the display names live in
        /// the game's data files and are extracted into
        /// <see cref="GameData.GameNames.Company"/> by
        /// <c>tools/extract_names.py</c>. When the company cannot be resolved
        /// the brand token is returned instead.
        /// </para>
        /// </summary>
        public string DisplayName
        {
            get
            {
                string name = GameData.GameNames.Lookup(GameData.GameNames.Company, PermanentDataId);
                if (!string.IsNullOrEmpty(name)) return name;

                // 回退：品牌 token 本身（去掉 company.permanent. 前缀后的部分）
                string brand = Brand;
                if (string.IsNullOrEmpty(brand)) return Id;
                return char.ToUpperInvariant(brand[0]) + brand.Substring(1).Replace('_', ' ');
            }
        }

        /// <summary>Whether the player has discovered this company.</summary>
        public bool Discovered
        {
            get { return _unit != null && _unit.GetBool("discovered"); }
            set { if (_unit != null) _unit.SetScalar("discovered", value); }
        }

        /// <summary>Ids of the <c>job_offer_data</c> units this company publishes.</summary>
        public List<string> JobOfferIds
        {
            get
            {
                var list = new List<string>();
                if (_unit == null) return list;
                foreach (var item in _unit.GetArrayItems("job_offer"))
                {
                    list.Add(item.AsId());
                }
                return list;
            }
        }

        /// <summary>Number of job offers this company publishes.</summary>
        public int JobOfferCount => JobOfferIds.Count;

        /// <summary>
        /// Registers the given job offer with this company so that the game
        /// shows it in the freight market of the city.
        /// </summary>
        /// <param name="jobOfferId">Id of the <c>job_offer_data</c> unit.</param>
        /// <returns>True when the offer was added, false when it was already
        /// registered.</returns>
        public bool AddJobOffer(string jobOfferId)
        {
            if (_unit == null || string.IsNullOrEmpty(jobOfferId)) return false;

            var items = new List<SiiValue>();
            foreach (var item in _unit.GetArrayItems("job_offer"))
            {
                if (string.Equals(item.AsId(), jobOfferId, StringComparison.Ordinal))
                {
                    return false;
                }
                items.Add(item);
            }

            items.Add(new ReferenceValue(jobOfferId));
            _unit.SetArrayItems("job_offer", items);
            return true;
        }

        /// <summary>
        /// Removes the given job offer from this company.
        /// </summary>
        /// <returns>True when the offer was removed.</returns>
        public bool RemoveJobOffer(string jobOfferId)
        {
            if (_unit == null || string.IsNullOrEmpty(jobOfferId)) return false;

            var items = new List<SiiValue>();
            bool removed = false;
            foreach (var item in _unit.GetArrayItems("job_offer"))
            {
                if (string.Equals(item.AsId(), jobOfferId, StringComparison.Ordinal))
                {
                    removed = true;
                    continue;
                }
                items.Add(item);
            }

            if (!removed) return false;

            _unit.SetArrayItems("job_offer", items);
            return true;
        }

        /// <summary>City display name of this company.</summary>
        public string CityName => GameData.CityCatalogue.DisplayName(CityToken);

        /// <summary>
        /// Text shown in the selection box of a combo box.
        /// <para>
        /// The application uses a custom combo box template that binds to
        /// <c>SelectionBoxItem</c>; <c>DisplayMemberPath</c> does not fill the
        /// item template in that case, so the item's own
        /// <see cref="ToString"/> is used instead.
        /// </para>
        /// </summary>
        public override string ToString()
        {
            // 下拉框里显示「公司名 · 城市」，比原始标识可读得多。
            string city = CityName;
            if (string.IsNullOrEmpty(city)) return DisplayName;
            return DisplayName + " · " + city;
        }

        /// <summary>
        /// Returns all companies of the document.
        /// </summary>
        public static List<Company> AllFromDocument(SiiDocument doc)
        {
            var list = new List<Company>();
            if (doc == null) return list;
            foreach (var u in doc.GetUnitsByType(Constants.CompanyType))
            {
                list.Add(new Company(u));
            }
            return list;
        }
    }
}
