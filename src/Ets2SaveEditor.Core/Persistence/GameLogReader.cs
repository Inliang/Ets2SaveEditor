#nullable enable
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Ets2SaveEditor.Core.Persistence
{
    /// <summary>游戏日志里最后一次加载的存档槽。</summary>
    public sealed class LastLoadedSave
    {
        /// <summary>profiles 下的十六进制档案目录名。</summary>
        public string ProfileHex { get; set; } = string.Empty;

        /// <summary>save 下的槽位目录名（如 autosave / 1）。</summary>
        public string SlotName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 读取游戏日志，找出最后一条 <c>Loading save.</c> 记录指向的存档槽。
    /// <para>
    /// ETS2 1.50+ 会把已加载的存档放进 filesystem cache：日志里最后加载的槽，
    /// 就是游戏「继续游戏 / 缓存」实际使用的槽。工具据此智能默认选中它。
    /// </para>
    /// 纯函数：只依赖传入的 <c>logPath</c>，不读注册表、不碰其它文件。
    /// </summary>
    public static class GameLogReader
    {
        /// <summary>只读文件末尾这么多个字节，避免把整份巨型日志读进内存。</summary>
        private const int MaxTailBytes = 2 * 1024 * 1024;

        /// <summary>
        /// 日志形如：
        /// <c>00:01:23.456 : Loading save. path: &lt;MEMFILE&gt;&lt;UFS&gt;/home/profiles/4361726F6C696E65E4B8B6/save/autosave/game.sii</c>
        /// </summary>
        private static readonly Regex SavePathRegex = new Regex(
            @"/profiles/(?<hex>[^/\\]+)/save/(?<slot>[^/\\]+)/game\.sii",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// 返回日志中最后一条 <c>Loading save.</c> 行解析出的档案 / 槽位；
        /// 日志不存在、无匹配、格式异常或任何 IO 异常一律返回 null。
        /// </summary>
        public static LastLoadedSave? FindLastLoaded(string logPath)
        {
            try
            {
                if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath)) return null;

                string tail = ReadTail(logPath, MaxTailBytes);
                if (string.IsNullOrEmpty(tail)) return null;

                string[] lines = tail.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
                // 倒序遍历：取最后一条 Loading save. 记录。
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    string line = lines[i];
                    if (line == null ||
                        line.IndexOf("Loading save", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    // 优先从 "path:" 之后开始找，找不到就退回整行。
                    string pathPart = line;
                    int p = line.IndexOf("path:", StringComparison.OrdinalIgnoreCase);
                    if (p >= 0) pathPart = line.Substring(p + "path:".Length);

                    Match m = SavePathRegex.Match(pathPart);
                    if (m.Success)
                    {
                        return new LastLoadedSave
                        {
                            ProfileHex = m.Groups["hex"].Value,
                            SlotName = m.Groups["slot"].Value
                        };
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>只读文件末尾最多 <paramref name="maxBytes"/> 字节并解码为文本。</summary>
        private static string ReadTail(string path, int maxBytes)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            long length = stream.Length;
            long start = Math.Max(0, length - maxBytes);
            stream.Seek(start, SeekOrigin.Begin);

            int count = (int)(length - start);
            byte[] buffer = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(buffer, read, count - read);
                if (n <= 0) break;
                read += n;
            }

            string text = Encoding.UTF8.GetString(buffer, 0, read);
            // 从文件中间开始时，第一行是残片，丢掉它。
            if (start > 0)
            {
                int nl = text.IndexOf('\n');
                if (nl >= 0) text = text.Substring(nl + 1);
            }
            return text;
        }
    }
}
