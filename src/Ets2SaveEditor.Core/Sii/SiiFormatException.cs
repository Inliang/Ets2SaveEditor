using System;

namespace Ets2SaveEditor.Core.Sii
{
    /// <summary>
    /// Raised when an SII document cannot be read, decoded or parsed.
    /// The save file on disk is never modified when this exception is thrown.
    /// </summary>
    public class SiiFormatException : Exception
    {
        public SiiFormatException()
        {
        }

        public SiiFormatException(string message) : base(message)
        {
        }

        public SiiFormatException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
