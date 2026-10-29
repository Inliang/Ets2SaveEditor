using System;
using System.Collections.Generic;
using Ets2SaveEditor.Core.GameData;

namespace Ets2SaveEditor.Core.GameData
{
    /// <summary>
    /// Built-in game data, backed by the authoritative catalogues.
    /// <para>
    /// 城市来自 <see cref="CityCatalogue"/>（从真实存档提取的 334 个城市，
    /// 中文名）；货物来自 <see cref="CargoCatalogue.SelectableTokens"/>（游戏实际
    /// 在用的全部货物，token 形如 <c>cargo.gummy_bears</c>）；货运公司来自
    /// <see cref="GameNames.Company"/>。
    /// </para>
    /// <para>
    /// 这里不再维护第二份手写清单：早期版本内置的 28 个英文城市和 10 个
    /// 虚构货物（<c>oil</c> / <c>milk</c> …）既不完整，货物 token 也不是
    /// 游戏认识的（游戏里是 <c>cargo.haulie</c> 这种形式），写进存档游戏
    /// 根本不认。
    /// </para>
    /// </summary>
    public class BuiltInGameData : IGameDataProvider
    {
        /// <summary>All known cities (Chinese names, from the city catalogue).</summary>
        public List<City> Cities() => CityCatalogue.Cities();

        /// <summary>
        /// All known cargo types (real game tokens, e.g. <c>cargo.gummy_bears</c>).
        /// </summary>
        public List<Cargo> Cargoes()
        {
            var list = new List<Cargo>();
            foreach (string token in CargoCatalogue.SelectableTokens)
            {
                list.Add(new Cargo(token, CargoCatalogue.DisplayName(token)));
            }

            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
            return list;
        }

        /// <summary>
        /// All known freight companies (e.g. <c>company.permanent.nbfc</c>).
        /// <para>
        /// 表里查不到中文名的条目（例如只随 DLC 提供的公司）会被跳过，
        /// 免得显示成 <c>@@cn_xxx@@</c> 这种本地化占位符。
        /// </para>
        /// </summary>
        public List<Company> Companies()
        {
            var list = new List<Company>();
            foreach (string token in GameNames.Company.Keys)
            {
                string name = GameNames.Lookup(GameNames.Company, token);
                if (string.IsNullOrEmpty(name)) continue;
                list.Add(new Company(token, name));
            }

            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
            return list;
        }
    }
}
