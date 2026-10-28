using System;
using System.Collections.Generic;
using System.Text;
using Ets2SaveEditor.Core.Persistence;

namespace Ets2SaveEditor.ViewModels
{
    /// <summary>
    /// 一个游戏档案（profile）及其包含的所有存档槽。
    /// 用于在右侧面板中按「档案 → 存档槽」两级选择存档。
    /// </summary>
    public class SaveProfileItem
    {
        /// <summary>"ETS2" 或 "ATS"。</summary>
        public string Game { get; set; } = string.Empty;

        /// <summary>档案目录名（通常是 SteamID 或十六进制编码的档案名）。</summary>
        public string ProfileId { get; set; } = string.Empty;

        /// <summary>是否来自 Steam。</summary>
        public bool IsSteam { get; set; }

        /// <summary>该档案下的存档槽。</summary>
        public List<SaveSlot> Slots { get; set; } = new List<SaveSlot>();

        /// <summary>
        /// 可读的档案名。ETS2 本地档案目录名是十六进制编码的 UTF-8 档案名，
        /// 例如 <c>4361726F6C696E65E4B8B6</c> 实际是「Caroline软运」。
        /// 无法解码时退回原始目录名。
        /// </summary>
        public string FriendlyName
        {
            get
            {
                string? decoded = TryDecodeHex(ProfileId);
                return decoded ?? ProfileId;
            }
        }

        /// <summary>下拉框中显示的文本。</summary>
        public string Display => FriendlyName + (IsSteam ? " · Steam" : string.Empty);

        /// <summary>下拉框收起时显示的文本（自定义模板下按 ToString 呈现）。</summary>
        public override string ToString() => Display;

        /// <summary>尝试把十六进制目录名解码为 UTF-8 文本，失败返回 null。</summary>
        private static string? TryDecodeHex(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 8 || id.Length % 2 != 0) return null;
            foreach (char c in id)
            {
                if (!Uri.IsHexDigit(c)) return null;
            }
            try
            {
                var bytes = new byte[id.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = Convert.ToByte(id.Substring(i * 2, 2), 16);
                }
                string text = Encoding.UTF8.GetString(bytes);
                if (string.IsNullOrWhiteSpace(text)) return null;
                foreach (char c in text)
                {
                    // 控制字符或替换字符说明这不是有效的 UTF-8 文本。
                    if (char.IsControl(c) || c == '\uFFFD') return null;
                }
                return text;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>存档槽内的一个 .sii 文件（列表显示短名，加载用完整路径）。</summary>
    public class SlotFileItem
    {
        /// <summary>文件名（如 game.sii）。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>完整路径。</summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>列表中显示的文本。</summary>
        public override string ToString() => Name;
    }
}
