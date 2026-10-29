using System;
using System.Collections.Generic;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii;

namespace Ets2SaveEditor.Core.Sii.Model
{
    /// <summary>
    /// Top-level SII document (the content between the outer braces).
    /// <para>
    /// 单元查找使用索引（id → 单元、类型 → 单元列表），因此按 id / 按类型
    /// 查询都是 O(1)：真实存档有 16000 多个单元，线性扫描会让每辆车、每辆
    /// 挂车的属性读取都付出上万次比较。
    /// </para>
    /// </summary>
    public class SiiDocument
    {
        /// <summary>Ordered list of units.</summary>
        private List<SiiUnit> _units = new List<SiiUnit>();

        /// <summary>按 id 建立的索引（id 不会随属性修改而改变）。</summary>
        private Dictionary<string, SiiUnit> _byId;

        /// <summary>按类型建立的索引，值保持文档中的原始顺序。</summary>
        private Dictionary<string, List<SiiUnit>> _byType;

        /// <summary>索引是否需要重建。</summary>
        private bool _indexDirty = true;

        /// <summary>建立索引时的单元数量，用来发现外部直接改动列表。</summary>
        private int _indexedCount = -1;

        /// <summary>车辆/挂车 id → 所属车库 id（懒加载）。</summary>
        private Dictionary<string, string> _garageOwner;

        /// <summary>建立车库归属索引时的单元数量。</summary>
        private int _garageOwnerCount = -1;

        /// <summary>Document header, normally "SiiNunit".</summary>
        public string Header { get; set; }

        /// <summary>
        /// Ordered list of units.
        /// <para>
        /// 列表本身仍然可以直接修改，但索引无法感知这种改动。请优先使用
        /// <see cref="AddUnit"/> / <see cref="RemoveUnit"/>，它们会同步维护索引；
        /// 若确实直接改了列表，请调用 <see cref="InvalidateIndex"/> 让索引重建。
        /// </para>
        /// </summary>
        public List<SiiUnit> Units
        {
            get { return _units; }
            set
            {
                _units = value ?? new List<SiiUnit>();
                InvalidateIndex();
            }
        }

        /// <summary>
        /// Schema of the document: it tells which attributes of which unit type
        /// are arrays. Needed to tell an empty array (<c>name: 0</c>) apart
        /// from an ordinary integer attribute.
        /// </summary>
        public SiiSchema Schema { get; set; }

        public SiiDocument()
        {
            Header = "SiiNunit";
            Units = new List<SiiUnit>();
        }

        /// <summary>
        /// Adds a unit and keeps the lookup index in sync.
        /// <para>
        /// 这是新增单元的推荐入口：直接往 <see cref="Units"/> 里加单元时，
        /// 索引只能靠「数量变了」来发现异常，无法保证正确。
        /// </para>
        /// </summary>
        /// <param name="unit">Unit to add; null is ignored.</param>
        public void AddUnit(SiiUnit unit)
        {
            if (unit == null) return;

            _units.Add(unit);

            // 索引还没建立时不必逐个登记：下次查询会整体重建。
            if (_indexDirty) return;

            IndexUnit(unit);
            _indexedCount = _units.Count;
        }

        /// <summary>
        /// Removes a unit and keeps the lookup index in sync.
        /// </summary>
        /// <param name="unit">Unit to remove.</param>
        /// <returns>True when the unit was found and removed.</returns>
        public bool RemoveUnit(SiiUnit unit)
        {
            if (unit == null) return false;

            bool removed = _units.Remove(unit);
            if (removed) InvalidateIndex();
            return removed;
        }

        /// <summary>
        /// Discards the lookup index; it is rebuilt on the next query.
        /// <para>
        /// 只有在直接改动了 <see cref="Units"/> 列表（而不是通过
        /// <see cref="AddUnit"/> / <see cref="RemoveUnit"/>）时才需要调用。
        /// </para>
        /// </summary>
        public void InvalidateIndex()
        {
            _indexDirty = true;
            _byId = null;
            _byType = null;
            _indexedCount = -1;
            _garageOwner = null;
            _garageOwnerCount = -1;
        }

        /// <summary>
        /// Rebuilds the lookup index from the current unit list.
        /// <para>
        /// 解析完存档后立刻调用一次，索引的开销就落在「加载存档」阶段，
        /// 而不是第一次渲染表格时。
        /// </para>
        /// </summary>
        public void RebuildIndex()
        {
            _byId = new Dictionary<string, SiiUnit>(StringComparer.Ordinal);
            _byType = new Dictionary<string, List<SiiUnit>>(StringComparer.Ordinal);

            foreach (var unit in _units)
            {
                IndexUnit(unit);
            }

            _indexedCount = _units.Count;
            _indexDirty = false;
        }

        /// <summary>Returns the unit with the given id, or null.</summary>
        public SiiUnit GetUnitById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            EnsureIndex();

            SiiUnit unit;
            return _byId.TryGetValue(id, out unit) ? unit : null;
        }

        /// <summary>Returns all units of the given type.</summary>
        public List<SiiUnit> GetUnitsByType(string type)
        {
            var result = new List<SiiUnit>();
            if (string.IsNullOrEmpty(type)) return result;

            EnsureIndex();

            List<SiiUnit> units;
            if (_byType.TryGetValue(type, out units)) result.AddRange(units);
            return result;
        }

        /// <summary>Returns the first unit of the given type, or null.</summary>
        public SiiUnit GetFirstByType(string type)
        {
            if (string.IsNullOrEmpty(type)) return null;

            EnsureIndex();

            List<SiiUnit> units;
            if (_byType.TryGetValue(type, out units) && units.Count > 0) return units[0];
            return null;
        }

        /// <summary>
        /// Returns the id of the garage the given vehicle or trailer unit is
        /// parked in, or an empty string when it is not in any garage.
        /// <para>
        /// 存档里会保留玩家卖掉的车/挂车对应的单元，只有还被某个车库的
        /// <c>vehicles</c> / <c>trailers</c> 数组引用的才算还停在车库里。
        /// </para>
        /// </summary>
        /// <param name="unitId">Id of the vehicle or trailer unit.</param>
        /// <returns>Id of the owning garage, or an empty string.</returns>
        public string FindGarageOwner(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return string.Empty;

            EnsureIndex();
            EnsureGarageOwnerIndex();

            string garageId;
            return _garageOwner.TryGetValue(unitId, out garageId) ? garageId : string.Empty;
        }

        /// <summary>
        /// Makes sure the lookup index exists and matches the current unit list.
        /// </summary>
        private void EnsureIndex()
        {
            // 数量对不上说明列表被外部改过（增删了单元），必须重建。
            if (!_indexDirty && _indexedCount == _units.Count) return;
            RebuildIndex();
        }

        /// <summary>
        /// Registers a single unit in the id / type indexes.
        /// <para>
        /// id 重复时保留先出现的那一个，与线性扫描的行为一致。
        /// </para>
        /// </summary>
        private void IndexUnit(SiiUnit unit)
        {
            if (unit == null) return;

            string id = unit.Id;
            if (!string.IsNullOrEmpty(id) && !_byId.ContainsKey(id))
            {
                _byId.Add(id, unit);
            }

            string type = unit.Type;
            if (string.IsNullOrEmpty(type)) return;

            List<SiiUnit> units;
            if (!_byType.TryGetValue(type, out units))
            {
                units = new List<SiiUnit>();
                _byType.Add(type, units);
            }
            units.Add(unit);
        }

        /// <summary>
        /// Builds the reverse index that maps a vehicle/trailer id to the id of
        /// the garage it is parked in.
        /// </summary>
        private void EnsureGarageOwnerIndex()
        {
            if (_garageOwner != null && _garageOwnerCount == _indexedCount) return;

            var map = new Dictionary<string, string>(StringComparer.Ordinal);

            List<SiiUnit> garages;
            if (_byType.TryGetValue(Constants.GarageType, out garages))
            {
                foreach (var garage in garages)
                {
                    if (garage == null) continue;

                    CollectGarageEntries(map, garage, "vehicles");
                    CollectGarageEntries(map, garage, "trailers");
                }
            }

            _garageOwner = map;
            _garageOwnerCount = _indexedCount;
        }

        /// <summary>
        /// Adds the units referenced by the given array of a garage to the
        /// reverse index. Unused slots hold the literal id "null" and are
        /// skipped, as are ids that are already known (the first garage in
        /// document order wins, just like the linear search did).
        /// </summary>
        private static void CollectGarageEntries(Dictionary<string, string> map, SiiUnit garage, string arrayName)
        {
            foreach (var item in garage.GetArrayItems(arrayName))
            {
                // AsReferenceId 会跳过数组头（整数）和 "null" 空位占位符。
                string id = item.AsReferenceId();
                if (string.IsNullOrEmpty(id)) continue;
                if (map.ContainsKey(id)) continue;
                map.Add(id, garage.Id);
            }
        }
    }
}
