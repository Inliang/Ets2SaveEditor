using System.Collections.Generic;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain.TypedUnits
{
    /// <summary>
    /// Typed wrapper around the <c>bank</c> unit. Reads/writes only the known
    /// fields; the rest of the unit is preserved by the underlying model.
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
        /// Account balance. In ETS2 the <c>bank</c> unit stores the balance under
        /// the field named "bank" as well.
        /// </summary>
        public long Money
        {
            get { return _unit != null ? _unit.GetLong("bank") : 0; }
            set { if (_unit != null) _unit.SetScalar("bank", value); }
        }

        /// <summary>
        /// Outstanding bank loan. Summed across all <c>bank_loan</c> units'
        /// "amount" field; setting it updates the first such unit.
        /// </summary>
        public long Loan
        {
            get
            {
                if (_doc == null) return 0;
                long total = 0;
                foreach (var loan in _doc.GetUnitsByType("bank_loan"))
                {
                    total += loan.GetLong("amount");
                }
                return total;
            }
            set
            {
                if (_doc == null) return;
                var loan = _doc.GetFirstByType("bank_loan");
                if (loan != null) loan.SetScalar("amount", value);
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
