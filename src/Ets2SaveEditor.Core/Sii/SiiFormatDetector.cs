using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Ets2SaveEditor.Core.Common;

namespace Ets2SaveEditor.Core.Sii
{
    /// <summary>Detected SII container format.</summary>
    public enum SiiFormat
    {
        /// <summary>Plaintext SII text (<c>SiiNunit</c>).</summary>
        Plaintext,

        /// <summary>Bare zlib stream containing a SII payload.</summary>
        Zlib,

        /// <summary>
        /// <c>ScsC</c> container: AES-256-CBC/PKCS7 decryption followed by
        /// zlib inflation. This is the default format of modern ETS2/ATS saves.
        /// </summary>
        Encrypted,

        /// <summary>Binary <c>BSII</c> payload (optionally wrapped in zlib).</summary>
        Binary,

        /// <summary>Unrecognized container.</summary>
        Unknown
    }

    /// <summary>
    /// Detects the SII container format and decodes it to plaintext SII text.
    /// Supports:
    ///   - 明文 SII（以 <c>SiiNunit</c> 开头）
    ///   - 二进制 SII（以 <c>BSII</c> 开头，由 <see cref="SiiBsiiDecoder"/> 转换）
    ///   - zlib 压缩 SII（裸 zlib，常见于部分旧文件）
    ///   - 加密 SII（<c>ScsC</c>：AES-256-CBC 解密后再 zlib 解压）
    /// 全程使用 .NET 托管 API，零额外依赖。
    /// </summary>
    public static class SiiFormatDetector
    {
        private const string HeaderMarker = "SiiNunit";

        /// <summary>
        /// "ScsC" 作为小端 uint32：字节序 S c s C。
        /// （早期版本误写为 "SrcC"，导致所有现代加密存档被判为 Unknown。）
        /// </summary>
        private const uint SigScsC = 0x43736353;

        // SCS Software 硬编码的 AES-256 密钥（与 TS-SE-Tool / sii-decrypt 一致）。
        private static readonly byte[] SiiKey =
        {
            0x2a, 0x5f, 0xcb, 0x17, 0x91, 0xd2, 0x2f, 0xb6,
            0x02, 0x45, 0xb3, 0xd8, 0x36, 0x9e, 0xd0, 0xb2,
            0xc2, 0x73, 0x71, 0x56, 0x3f, 0xbf, 0x1f, 0x3c,
            0x9e, 0xdf, 0x6b, 0x11, 0x82, 0x5a, 0x5d, 0x0a
        };

        // 加密容器（"ScsC"）布局，共 56 字节头：
        //   [0..4)   "ScsC" 签名
        //   [4..36)  32 字节 HMAC-SHA256（密钥未公开，读时忽略，无法复现）
        //   [36..52) 16 字节 IV（AES 初始向量）
        //   [52..56) u32 明文（zlib 解压后）长度，小端
        //   [56..)   密文（AES-256-CBC / PKCS7）
        internal const int HmacOffset = 4;
        internal const int HmacLength = 32;
        internal const int IvOffset = 36;
        internal const int IvLength = 16;
        internal const int PlaintextLengthOffset = 52;
        internal const int PlaintextLengthLength = 4;
        internal const int CiphertextOffset = 56;
        internal const int ContainerHeaderLength = 56;

        /// <summary>
        /// 检查原始字节并判定 SII 格式。
        /// </summary>
        public static SiiFormat Detect(byte[] raw)
        {
            if (raw == null || raw.Length < 4)
            {
                return SiiFormat.Unknown;
            }

            uint sig = ReadUInt32LE(raw, 0);

            // 加密格式：先解密 + 解压得到明文。
            if (sig == SigScsC)
            {
                return SiiFormat.Encrypted;
            }

            // 明文：以 ASCII 的 SiiNunit 开头。
            if (raw.Length >= HeaderMarker.Length &&
                Encoding.ASCII.GetString(raw, 0, HeaderMarker.Length) == HeaderMarker)
            {
                return SiiFormat.Plaintext;
            }

            // 二进制 SII：以 ASCII 的 BSII 开头。
            if (raw.Length >= SiiBsiiDecoder.Magic.Length &&
                Encoding.ASCII.GetString(raw, 0, SiiBsiiDecoder.Magic.Length) == SiiBsiiDecoder.Magic)
            {
                return SiiFormat.Binary;
            }

            // 裸 zlib：解压后应以 SiiNunit 或 BSII 开头。
            try
            {
                byte[] inflated = InflateBytes(raw);
                if (inflated != null && StartsWithMarker(inflated))
                {
                    return SiiFormat.Zlib;
                }
            }
            catch
            {
                // 忽略，落入 Unknown。
            }

            return SiiFormat.Unknown;
        }

        /// <summary>
        /// 根据格式把原始字节解码为明文 SII 文本。
        /// </summary>
        /// <exception cref="SiiFormatException">当格式不支持或解码失败时抛出。</exception>
        public static string Decode(byte[] raw, SiiFormat format)
        {
            return Decode(raw, format, out _);
        }

        /// <summary>
        /// According to the format, decodes the raw bytes into plaintext SII
        /// text and reports the schema of the file (which attributes of which
        /// unit type are arrays).
        /// <para>
        /// The schema is only available for binary (BSII) payloads, which carry
        /// the structure definitions. For plaintext files an empty schema is
        /// returned and the parser falls back to its heuristics.
        /// </para>
        /// </summary>
        /// <exception cref="SiiFormatException">当格式不支持或解码失败时抛出。</exception>
        public static string Decode(byte[] raw, SiiFormat format, out SiiSchema schema)
        {
            schema = SiiSchema.Empty;

            switch (format)
            {
                case SiiFormat.Plaintext:
                    return StripBom(DecodePayload(raw, ref schema));

                case SiiFormat.Zlib:
                    return StripBom(DecodePayload(InflateBytes(raw), ref schema));

                case SiiFormat.Encrypted:
                    return StripBom(DecodePayload(DecodeEncryptedBytes(raw), ref schema));

                case SiiFormat.Binary:
                    return StripBom(DecodePayload(raw, ref schema));

                default:
                    throw new SiiFormatException(
                        "无法识别的 SII 格式。该存档可能采用了尚未支持的加密/二进制格式。");
            }
        }

        /// <summary>
        /// 把解密/解压后的载荷转换成 SII 文本：二进制 <c>BSII</c> 载荷走
        /// <see cref="SiiBsiiDecoder"/>，其余按 UTF-8 文本处理。
        /// </summary>
        private static string DecodePayload(byte[] payload, ref SiiSchema schema)
        {
            if (payload == null)
            {
                throw new SiiFormatException("SII 载荷为空。");
            }

            if (IsBsii(payload))
            {
                return SiiBsiiDecoder.Decode(payload, out schema);
            }

            return Encoding.UTF8.GetString(payload);
        }

        /// <summary>判断载荷是否为二进制 BSII。</summary>
        private static bool IsBsii(byte[] payload)
        {
            return payload != null &&
                   payload.Length >= SiiBsiiDecoder.Magic.Length &&
                   Encoding.ASCII.GetString(payload, 0, SiiBsiiDecoder.Magic.Length) == SiiBsiiDecoder.Magic;
        }

        private static bool StartsWithMarker(byte[] payload)
        {
            if (payload.Length >= HeaderMarker.Length &&
                Encoding.ASCII.GetString(payload, 0, HeaderMarker.Length) == HeaderMarker)
            {
                return true;
            }
            return IsBsii(payload);
        }

        /// <summary>
        /// 解密 <c>ScsC</c> 容器：AES-256-CBC 解密后 zlib 解压，返回原始载荷字节。
        /// </summary>
        private static byte[] DecodeEncryptedBytes(byte[] raw)
        {
            if (raw.Length < CiphertextOffset + IvLength)
            {
                throw new SiiFormatException("加密的 SII 文件过小，结构不完整。");
            }

            byte[] iv = new byte[IvLength];
            Array.Copy(raw, IvOffset, iv, 0, IvLength);

            int cipherLen = raw.Length - CiphertextOffset;
            byte[] cipher = new byte[cipherLen];
            Array.Copy(raw, CiphertextOffset, cipher, 0, cipherLen);

            byte[] decrypted = AesDecrypt(cipher, SiiKey, iv);
            byte[] payload = InflateBytes(decrypted);

            // [52..56) 记录了 zlib 解压后的明文长度，仅作一致性参考：
            // 游戏的 HMAC 密钥未公开，无法重新生成加密容器，因此只读取不校验。
            uint expectedLength = ReadUInt32LE(raw, PlaintextLengthOffset);
            if (expectedLength != 0 && expectedLength != (uint)payload.Length)
            {
                // 长度不一致时仍以实际解出的载荷为准，避免误判导致无法打开存档。
            }

            return payload;
        }

        private static byte[] AesDecrypt(byte[] data, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(data, 0, data.Length);
        }

        /// <summary>
        /// zlib 解压，返回原始字节（优先 ZLibStream，失败回退裸 deflate）。
        /// </summary>
        private static byte[] InflateBytes(byte[] raw)
        {
            // 优先尝试带 zlib 头的 ZLibStream。
            try
            {
                using var input = new MemoryStream(raw);
                using var zlib = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                zlib.CopyTo(output);
                return output.ToArray();
            }
            catch
            {
                // 回退到裸 deflate（无 zlib 头）。
            }

            try
            {
                using var input = new MemoryStream(raw);
                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                deflate.CopyTo(output);
                return output.ToArray();
            }
            catch (Exception ex)
            {
                throw new SiiFormatException("无法解压 SII 数据：既不是 zlib 也不是裸 deflate 流。", ex);
            }
        }

        private static string StripBom(string s)
        {
            if (!string.IsNullOrEmpty(s) && s[0] == '\uFEFF')
            {
                return s.Substring(1);
            }
            return s;
        }

        private static uint ReadUInt32LE(byte[] b, int off)
        {
            return unchecked((uint)(
                b[off] |
                (b[off + 1] << 8) |
                (b[off + 2] << 16) |
                (b[off + 3] << 24)));
        }

    }
}
