using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Ets2SaveEditor.Core.Common;

namespace Ets2SaveEditor.Core.Sii
{
    /// <summary>Detected SII container format.</summary>
    public enum SiiFormat
    {
        Plaintext,
        Zlib,
        Binary,
        Unknown
    }

    /// <summary>
    /// Detects the SII container format and decompresses zlib / deflate payloads
    /// using managed BCL streams (zero extra dependencies).
    /// </summary>
    public static class SiiFormatDetector
    {
        private const string HeaderMarker = "SiiNunit";

        /// <summary>
        /// Inspects the raw bytes and classifies the SII format.
        /// </summary>
        public static SiiFormat Detect(byte[] raw)
        {
            if (raw == null || raw.Length < HeaderMarker.Length)
            {
                return SiiFormat.Unknown;
            }

            // Plaintext SII starts with the ASCII header marker.
            string head = Encoding.ASCII.GetString(raw, 0, HeaderMarker.Length);
            if (head == HeaderMarker)
            {
                return SiiFormat.Plaintext;
            }

            // Otherwise try decompressing; zlib/deflate SII decompresses to plaintext.
            try
            {
                string text = Decompress(raw);
                if (text != null && text.StartsWith(HeaderMarker, StringComparison.Ordinal))
                {
                    return SiiFormat.Zlib;
                }
            }
            catch
            {
                // Ignore and fall through to Unknown.
            }

            return SiiFormat.Unknown;
        }

        /// <summary>
        /// Decompresses zlib-compressed SII data to its plaintext representation.
        /// Falls back to raw deflate if the zlib wrapper fails.
        /// </summary>
        /// <exception cref="SiiFormatException">When neither decoder succeeds.</exception>
        public static string Decompress(byte[] raw)
        {
            if (raw == null || raw.Length == 0)
            {
                throw new SiiFormatException("Cannot decompress an empty buffer.");
            }

            // Attempt zlib (ZLibStream expects the zlib header/trailer).
            try
            {
                using (var input = new MemoryStream(raw))
                using (var zlib = new ZLibStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    zlib.CopyTo(output);
                    return Encoding.UTF8.GetString(output.ToArray());
                }
            }
            catch
            {
                // Fall through to raw deflate.
            }

            // Fallback: raw deflate stream (no zlib wrapper).
            try
            {
                using (var input = new MemoryStream(raw))
                using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    deflate.CopyTo(output);
                    return Encoding.UTF8.GetString(output.ToArray());
                }
            }
            catch (Exception ex)
            {
                throw new SiiFormatException("Failed to decompress SII data: not a zlib or deflate stream.", ex);
            }
        }
    }
}
