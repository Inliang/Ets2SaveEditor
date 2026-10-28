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
        Plaintext,
        Zlib,
        Encrypted,   // "SrcC": AES-256-CBC + zlib（现代 ETS2/ATS 存档默认格式）
        Binary,      // "SIIB": 二进制 SII（P2 尚未支持）
        Unknown
    }

    /// <summary>
    /// Detects the SII container format and decodes it to plaintext SII text.
    /// Supports:
    ///   - 明文 SII（以 <c>SiiNunit</c> 开头）
    ///   - zlib 压缩 SII（裸 zlib，常见于部分旧文件）
    ///   - 加密 SII（<c>SrcC</c>：AES-256-CBC 解密后再 zlib 解压）
    /// 全程使用 .NET 托管 API，零额外依赖。
    /// </summary>
    public static class SiiFormatDetector
    {
        private const string HeaderMarker = "SiiNunit";

        // "SrcC" 作为小端 uint32：字节序 S r c C。
        private const uint SigSrcC = 0x43637253;

        // SCS Software 硬编码的 AES-256 密钥（与 TS-SE-Tool / sii-decrypt 一致）。
        private static readonly byte[] SiiKey =
        {
            0x2a, 0x5f, 0xcb, 0x17, 0x91, 0xd2, 0x2f, 0xb6,
            0x02, 0x45, 0xb3, 0xd8, 0x36, 0x9e, 0xd0, 0xb2,
            0xc2, 0x73, 0x71, 0x56, 0x3f, 0xbf, 0x1f, 0x3c,
            0x9e, 0xdf, 0x6b, 0x11, 0x82, 0x5a, 0x5d, 0x0a
        };

        // 加密容器布局：
        //   [0..4)   "SrcC" 签名
        //   [4..36)  保留/校验（解密时忽略）
        //   [36..52) 16 字节 IV（AES 初始向量）
        //   [52..56) 保留（解密时忽略）
        //   [56..)   密文（AES-256-CBC）
        private const int IvOffset = 36;
        private const int IvLength = 16;
        private const int CiphertextOffset = 56;

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
            if (sig == SigSrcC)
            {
                return SiiFormat.Encrypted;
            }

            // 明文：以 ASCII 的 SiiNunit 开头。
            if (raw.Length >= HeaderMarker.Length &&
                Encoding.ASCII.GetString(raw, 0, HeaderMarker.Length) == HeaderMarker)
            {
                return SiiFormat.Plaintext;
            }

            // 裸 zlib：解压后应以 SiiNunit 开头。
            try
            {
                string text = Inflate(raw);
                if (text != null && StripBom(text).StartsWith(HeaderMarker, StringComparison.Ordinal))
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
            switch (format)
            {
                case SiiFormat.Plaintext:
                    return StripBom(Encoding.UTF8.GetString(raw));

                case SiiFormat.Zlib:
                    return StripBom(Inflate(raw));

                case SiiFormat.Encrypted:
                    return StripBom(DecodeEncrypted(raw));

                case SiiFormat.Binary:
                    throw new SiiFormatException(
                        "暂不支持二进制 SII（SIIB）格式，该功能将在后续版本加入。");

                default:
                    throw new SiiFormatException(
                        "无法识别的 SII 格式。该存档可能采用了尚未支持的加密/二进制格式。");
            }
        }

        /// <summary>
        /// 解密 <c>SrcC</c> 容器：AES-256-CBC 解密后 zlib 解压。
        /// </summary>
        private static string DecodeEncrypted(byte[] raw)
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
            return Inflate(decrypted);
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
        /// zlib 解压（优先 ZLibStream，失败回退裸 deflate）。
        /// </summary>
        private static string Inflate(byte[] raw)
        {
            // 优先尝试带 zlib 头的 ZLibStream。
            try
            {
                using var input = new MemoryStream(raw);
                using var zlib = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                zlib.CopyTo(output);
                return Encoding.UTF8.GetString(output.ToArray());
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
                return Encoding.UTF8.GetString(output.ToArray());
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
