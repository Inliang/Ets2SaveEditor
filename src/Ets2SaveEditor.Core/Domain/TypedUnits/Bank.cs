using System.Collections.Generic;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain.TypedUnits
{
    /// <summary>
    /// Typed wrapper around the <c>bank</c> unit. The modern save format keeps
    /// the balance in <c>money_account</c> (INT64) and the outstanding loans in
    /// the <c>loans</c> array (references to <c>bank_loan</c> units).
    /// Reads/writes only the known fields; the rest of the unit is preserved
    /// by the underlying model.
    /// </summary>
    public class Bank
    {
        private readonly SiiUnit _unit;
        private readonly SiiDocument _doc;

        /// <summary>The underlying SII unit.</summary>
        public SiiUnit Unit => _unit;

        public Bank(SiiUnit unit, SiiDocument doc)
        {
            _unit = unit;
            _doc = doc;
        }

        /// <summary>
        /// Account balance (<c>money_account</c>).
        /// </summary>
        public long Money
        {
            get { return _unit != null ? _unit.GetLong(Constants.BankMoneyAttribute) : 0; }
            set { if (_unit != null) _unit.SetScalar(Constants.BankMoneyAttribute, value); }
        }

        /// <summary>
        /// Credit limit granted by the bank (<c>loan_limit</c>).
        /// </summary>
        public long LoanLimit
        {
            get { return _unit != null ? _unit.GetLong(Constants.BankLoanLimitAttribute) : 0; }
            set { if (_unit != null) _unit.SetScalar(Constants.BankLoanLimitAttribute, value); }
        }

        /// <summary>Whether the player currently overdrawn the account.</summary>
        public bool Overdraft
        {
            get { return _unit != null && _unit.GetBool("overdraft"); }
            set { if (_unit != null) _unit.SetScalar("overdraft", value); }
        }

        /// <summary>Whether the bank app is enabled for this profile.</summary>
        public bool AppEnabled
        {
            get { return _unit != null && _unit.GetBool("app_enabled"); }
            set { if (_unit != null) _unit.SetScalar("app_enabled", value); }
        }

        /// <summary>Coinsurance ratio (<c>coinsurance_ratio</c>, 0..1).</summary>
        public double CoinInsuranceRatio
        {
            get { return _unit != null ? _unit.GetDouble("coinsurance_ratio") : 0; }
            set { if (_unit != null) _unit.SetFloat("coinsurance_ratio", value); }
        }

        /// <summary>Fixed coinsurance fee (<c>coinsurance_fixed</c>).</summary>
        public long CoinInsuranceFixed
        {
            get { return _unit != null ? _unit.GetLong("coinsurance_fixed") : 0; }
            set { if (_unit != null) _unit.SetScalar("coinsurance_fixed", value); }
        }

        /// <summary>Severity of the latest accident (<c>accident_severity</c>).</summary>
        public double AccidentSeverity
        {
            get { return _unit != null ? _unit.GetDouble("accident_severity") : 0; }
            set { if (_unit != null) _unit.SetFloat("accident_severity", value); }
        }

        /// <summary>
        /// Outstanding bank loan. The <c>bank</c> unit only stores references to
        /// <c>bank_loan</c> units in its <c>loans</c> array, so the total is
        /// summed up from the referenced units.
        /// </summary>
        public long Loan
        {
            get
            {
                if (_unit == null || _doc == null) return 0;
                long total = 0;
                foreach (var item in _unit.GetArrayItems(Constants.BankLoansAttribute))
                {
                    string id = item.AsId();
                    if (string.IsNullOrEmpty(id)) continue;
                    var loanUnit = _doc.GetUnitById(id);
                    if (loanUnit != null) total += loanUnit.GetLong("amount");
                }
                return total;
            }
            set
            {
                if (_unit == null || _doc == null) return;
                var loanUnit = _doc.GetFirstByType(Constants.BankLoanType);
                if (loanUnit != null) loanUnit.SetScalar("amount", value);
            }
        }

        /// <summary>Wraps the first <c>bank</c> unit of the document.</summary>
        public static Bank FromDocument(SiiDocument doc)
        {
            if (doc == null) return null;
            var unit = doc.GetFirstByType(Constants.BankType);
            if (unit == null) return null;
            return new Bank(unit, doc);
        }
    }
}
