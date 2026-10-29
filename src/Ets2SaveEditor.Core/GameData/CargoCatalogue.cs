using System;
using System.Collections.Generic;

namespace Ets2SaveEditor.Core.GameData
{
    /// <summary>
    /// Catalogue of the cargo types the game knows about, together with the
    /// income rate used for the pay estimate.
    /// <para>
    /// The 350 tokens are the ones that occur in a real ETS2 save. The rate
    /// (euros per unit per kilometre) is derived from the
    /// <c>delivery_log_entry</c> records of that save: every record holds the
    /// cargo, the distance, the number of units and the money the player
    /// received, so <c>revenue ÷ (distance × units)</c> yields the rate the
    /// game paid. Those measured rates take precedence over the fallback
    /// table below.
    /// </para>
    /// <para>
    /// The game does not store the rate of an offer — it computes the income
    /// from cargo, distance and urgency when the offer is displayed, using
    /// the market price of the destination city. The estimate therefore
    /// reproduces the order of magnitude, not the exact amount.
    /// </para>
    /// </summary>
    public static class CargoCatalogue
    {
        /// <summary>
        /// Rates measured from the delivery records of a real save file
        /// (euros per unit per kilometre). These are the most accurate values
        /// available without the game data files.
        /// </summary>
        private static readonly Dictionary<string, double> MeasuredRates =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                { "cargo.car_balt1", 0.4839 },
                { "cargo.concr_cent", 4.3891 },
                { "cargo.concr_stair", 2.8278 },
                { "cargo.gummy_bears", 1.0082 },
                { "cargo.halloween", 0.6039 },
                { "cargo.halloween_1", 0.4366 },
                { "cargo.halloween_2a", 0.5384 },
                { "cargo.halloween_2b", 0.7767 },
                { "cargo.plast_film", 0.9417 },
                { "cargo.used_packag", 0.8512 },
                { "cargo.used_plast", 0.3242 },
                { "cargo.used_plast_c", 0.3710 },
                { "cargo.x_apaints", 0.3195 },
                { "cargo.x_craftsupp", 0.3238 },
                { "cargo.x_giftparc", 0.2653 },
                { "cargo.x_gifts_ch", 0.5252 },
                { "cargo.x_gifts_ma", 0.3883 },
                { "cargo.x_honey", 0.5241 },
                { "cargo.x_woodlam", 0.4625 },
            };

        private static readonly Dictionary<string, double> Rates =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                { "cargo.acetylene", 0.85 },
                { "cargo.acid", 0.85 },
                { "cargo.air_mails", 0.85 },
                { "cargo.aircft_tires", 0.85 },
                { "cargo.aircond", 0.85 },
                { "cargo.almond", 0.50 },
                { "cargo.alu_ingot", 0.85 },
                { "cargo.alu_profile", 0.85 },
                { "cargo.ammonia", 0.85 },
                { "cargo.ammunition", 0.85 },
                { "cargo.apples", 0.50 },
                { "cargo.apples_c", 0.50 },
                { "cargo.arsenic", 0.85 },
                { "cargo.asph_miller", 0.85 },
                { "cargo.atl_cod_flt", 0.85 },
                { "cargo.auger_wag", 0.50 },
                { "cargo.backfl_prev", 0.50 },
                { "cargo.barley", 0.50 },
                { "cargo.basil", 0.50 },
                { "cargo.battery", 0.50 },
                { "cargo.beans", 0.50 },
                { "cargo.beef_meat", 0.50 },
                { "cargo.beverages", 0.85 },
                { "cargo.beverages_c", 0.85 },
                { "cargo.beverages_t", 0.85 },
                { "cargo.big_bag_seed", 0.50 },
                { "cargo.boric_acid", 0.50 },
                { "cargo.bottle_water", 0.50 },
                { "cargo.brake_fluid", 0.50 },
                { "cargo.brake_pads", 0.50 },
                { "cargo.bricks", 0.50 },
                { "cargo.cable_reel", 0.50 },
                { "cargo.can_sardines", 0.50 },
                { "cargo.canned_beans", 0.50 },
                { "cargo.canned_beef", 0.50 },
                { "cargo.canned_pork", 0.50 },
                { "cargo.canned_tuna", 0.50 },
                { "cargo.car_balt1", 0.50 },
                { "cargo.car_balt2", 0.50 },
                { "cargo.car_d", 0.50 },
                { "cargo.car_f", 0.50 },
                { "cargo.car_gr", 0.50 },
                { "cargo.car_ibe", 0.50 },
                { "cargo.car_it", 0.50 },
                { "cargo.caravans", 0.50 },
                { "cargo.carb_water", 0.85 },
                { "cargo.carbn_pwdr_c", 0.50 },
                { "cargo.carrots", 0.50 },
                { "cargo.carrots_c", 0.50 },
                { "cargo.cars_fr", 0.50 },
                { "cargo.cauliflower", 0.50 },
                { "cargo.caviar", 0.50 },
                { "cargo.cement", 0.50 },
                { "cargo.cheese", 0.50 },
                { "cargo.chem_granule", 0.85 },
                { "cargo.chem_sorb_c", 0.85 },
                { "cargo.chem_sorbent", 0.85 },
                { "cargo.chemicals", 0.85 },
                { "cargo.chewing_gums", 0.50 },
                { "cargo.chicken_meat", 0.50 },
                { "cargo.chimney_syst", 0.50 },
                { "cargo.chlorine", 0.85 },
                { "cargo.chlorine_t", 0.85 },
                { "cargo.chocolate", 0.85 },
                { "cargo.clothes", 0.50 },
                { "cargo.clothes_c", 0.50 },
                { "cargo.coal", 0.50 },
                { "cargo.coconut_milk", 0.50 },
                { "cargo.coconut_oil", 0.50 },
                { "cargo.comp_process", 0.50 },
                { "cargo.conc_juice_t", 0.50 },
                { "cargo.concen_juice", 0.50 },
                { "cargo.concr_beams", 0.50 },
                { "cargo.concr_beams2", 0.50 },
                { "cargo.concr_cent", 0.50 },
                { "cargo.concr_stair", 0.50 },
                { "cargo.cont_trees", 0.50 },
                { "cargo.contamin", 0.50 },
                { "cargo.copp_rf_gutt", 0.50 },
                { "cargo.corks", 0.50 },
                { "cargo.cott_cheese", 0.50 },
                { "cargo.cott_harvest", 0.50 },
                { "cargo.crawler", 0.50 },
                { "cargo.cut_flowers", 0.50 },
                { "cargo.cyanide", 0.50 },
                { "cargo.czl_es300", 0.50 },
                { "cargo.czl_muv75", 0.50 },
                { "cargo.daf_tr", 0.50 },
                { "cargo.desinfection", 0.50 },
                { "cargo.diesel", 0.50 },
                { "cargo.diesel_gen", 0.50 },
                { "cargo.digger1000", 0.50 },
                { "cargo.digger500", 0.50 },
                { "cargo.disc_harrows", 0.50 },
                { "cargo.dozer", 0.50 },
                { "cargo.driller", 0.50 },
                { "cargo.dryers", 0.50 },
                { "cargo.drymilk", 0.50 },
                { "cargo.dynamite", 0.85 },
                { "cargo.elect_wiring", 0.85 },
                { "cargo.electronics", 0.85 },
                { "cargo.emp_wine_bar", 0.85 },
                { "cargo.emp_wine_bot", 0.85 },
                { "cargo.empty_barr", 0.27 },
                { "cargo.empty_palet", 0.27 },
                { "cargo.empty_spool", 0.27 },
                { "cargo.emptytank", 0.27 },
                { "cargo.ethane", 0.50 },
                { "cargo.excav_soil", 0.50 },
                { "cargo.excavator", 0.50 },
                { "cargo.exhausts_c", 0.85 },
                { "cargo.fert_spread", 0.50 },
                { "cargo.fertilizer", 0.85 },
                { "cargo.fireworks", 0.85 },
                { "cargo.fish_chips", 0.50 },
                { "cargo.floorpanels", 0.50 },
                { "cargo.flour", 0.85 },
                { "cargo.fluorine", 0.85 },
                { "cargo.food_oil_t", 0.50 },
                { "cargo.forage_harv", 0.50 },
                { "cargo.forklifts", 0.50 },
                { "cargo.fresh_fish", 0.50 },
                { "cargo.froz_octopi", 0.50 },
                { "cargo.frozen_hake", 0.50 },
                { "cargo.frsh_herbs", 0.50 },
                { "cargo.fuel_oil", 0.50 },
                { "cargo.fuel_tanks", 0.50 },
                { "cargo.fueltanker", 0.50 },
                { "cargo.furniture", 0.50 },
                { "cargo.garlic", 0.50 },
                { "cargo.glass", 0.85 },
                { "cargo.glass_packed", 0.85 },
                { "cargo.gnocchi", 0.50 },
                { "cargo.goat_cheese", 0.50 },
                { "cargo.granite_cube", 0.50 },
                { "cargo.grapes", 0.50 },
                { "cargo.graph_grease", 0.50 },
                { "cargo.grass_rolls", 0.50 },
                { "cargo.gravel", 0.50 },
                { "cargo.guard_rails", 0.50 },
                { "cargo.gummy_bears", 0.50 },
                { "cargo.halloween_1", 0.50 },
                { "cargo.harvest_bins", 0.50 },
                { "cargo.haulie", 0.50 },
                { "cargo.haulie2", 0.50 },
                { "cargo.hchemicals", 0.85 },
                { "cargo.helicopter", 0.50 },
                { "cargo.hi_volt_cabl", 0.85 },
                { "cargo.hipresstank", 0.50 },
                { "cargo.hmetal", 0.85 },
                { "cargo.honey", 0.50 },
                { "cargo.horse_tr", 0.50 },
                { "cargo.hvac", 0.50 },
                { "cargo.hw25c1", 0.50 },
                { "cargo.hw25c2", 0.50 },
                { "cargo.hw25c3", 0.50 },
                { "cargo.hydrochlor", 0.85 },
                { "cargo.hydrogen", 0.85 },
                { "cargo.ibc_cont", 0.50 },
                { "cargo.icecream", 0.50 },
                { "cargo.iced_coffee", 0.50 },
                { "cargo.iron_pipes", 0.50 },
                { "cargo.kerosene", 0.85 },
                { "cargo.ketchup", 0.50 },
                { "cargo.lamb_stom", 0.50 },
                { "cargo.large_cont", 0.50 },
                { "cargo.largetubes", 0.50 },
                { "cargo.lavender", 0.50 },
                { "cargo.limonades", 0.50 },
                { "cargo.live_cattle", 0.50 },
                { "cargo.live_pigs", 0.50 },
                { "cargo.liver_paste", 0.50 },
                { "cargo.locomotive", 0.50 },
                { "cargo.logs", 0.50 },
                { "cargo.lpg", 0.85 },
                { "cargo.lpg_t", 0.85 },
                { "cargo.lumber", 0.50 },
                { "cargo.lumber_p", 0.50 },
                { "cargo.lux_yacht", 0.50 },
                { "cargo.magnesium", 0.50 },
                { "cargo.maple_syrup", 0.50 },
                { "cargo.marb_blck", 0.50 },
                { "cargo.marb_blck2", 0.50 },
                { "cargo.marb_slab", 0.50 },
                { "cargo.mason_jars", 0.50 },
                { "cargo.mat_handler", 0.50 },
                { "cargo.med_equip", 0.85 },
                { "cargo.med_vaccine", 0.85 },
                { "cargo.mercuric", 0.85 },
                { "cargo.metal_beams", 0.50 },
                { "cargo.metal_cans", 0.50 },
                { "cargo.metal_center", 0.85 },
                { "cargo.metal_pipes", 0.50 },
                { "cargo.milk", 0.50 },
                { "cargo.milk_t", 0.50 },
                { "cargo.mob_crusher", 0.50 },
                { "cargo.mob_screener", 0.50 },
                { "cargo.mob_stacker", 0.50 },
                { "cargo.mobile_compr", 0.85 },
                { "cargo.mobile_crane", 0.50 },
                { "cargo.mondeos", 0.50 },
                { "cargo.moto_tires", 0.50 },
                { "cargo.motor_oil", 0.85 },
                { "cargo.motor_oil_c", 0.85 },
                { "cargo.motorcycles", 0.50 },
                { "cargo.mozzarela", 0.50 },
                { "cargo.mtl_coil", 0.50 },
                { "cargo.natur_rubber", 0.85 },
                { "cargo.neon", 0.50 },
                { "cargo.nitrocel", 0.85 },
                { "cargo.nitrogen", 0.85 },
                { "cargo.nonalco_beer", 0.85 },
                { "cargo.nuts", 0.50 },
                { "cargo.nylon_cord", 0.50 },
                { "cargo.oil", 0.85 },
                { "cargo.oil_filt_c", 0.85 },
                { "cargo.oil_filters", 0.85 },
                { "cargo.olive_oil", 0.85 },
                { "cargo.olive_oil_t", 0.85 },
                { "cargo.olive_tree", 0.50 },
                { "cargo.olives", 0.50 },
                { "cargo.onion", 0.50 },
                { "cargo.oranges", 0.50 },
                { "cargo.ore", 0.50 },
                { "cargo.outdr_flr_tl", 0.50 },
                { "cargo.overweight", 0.50 },
                { "cargo.packag_food", 0.50 },
                { "cargo.paper", 0.50 },
                { "cargo.pasta", 0.50 },
                { "cargo.pears", 0.50 },
                { "cargo.peas", 0.50 },
                { "cargo.perfor_frks", 0.50 },
                { "cargo.pesticide", 0.85 },
                { "cargo.pesto", 0.50 },
                { "cargo.pet_food", 0.50 },
                { "cargo.pet_food_c", 0.50 },
                { "cargo.petrol", 0.50 },
                { "cargo.phosphor", 0.50 },
                { "cargo.pickup_gr", 0.50 },
                { "cargo.plant_substr", 0.50 },
                { "cargo.planter", 0.50 },
                { "cargo.plast_film", 0.50 },
                { "cargo.plast_film_c", 0.50 },
                { "cargo.plast_pipes", 0.50 },
                { "cargo.plastic_gra", 0.50 },
                { "cargo.plumb_suppl", 0.50 },
                { "cargo.plums", 0.50 },
                { "cargo.pnut_butter", 0.50 },
                { "cargo.polyst_box", 0.50 },
                { "cargo.pork_meat", 0.50 },
                { "cargo.post_packag", 0.50 },
                { "cargo.pot_flowers", 0.50 },
                { "cargo.potahydro", 0.85 },
                { "cargo.potassium", 0.50 },
                { "cargo.potatoes", 0.50 },
                { "cargo.precast_strs", 0.50 },
                { "cargo.press_sl_val", 0.50 },
                { "cargo.propane", 0.85 },
                { "cargo.prosciutto", 0.50 },
                { "cargo.protec_cloth", 0.50 },
                { "cargo.pumps", 0.50 },
                { "cargo.radiators", 0.85 },
                { "cargo.re_bars", 0.85 },
                { "cargo.refl_posts", 0.85 },
                { "cargo.rice", 0.50 },
                { "cargo.rice_c", 0.50 },
                { "cargo.roller", 0.50 },
                { "cargo.roof_tiles", 0.50 },
                { "cargo.roofing_felt", 0.50 },
                { "cargo.rooflights", 0.50 },
                { "cargo.rye", 0.50 },
                { "cargo.salm_fillet", 0.50 },
                { "cargo.salt_spice_c", 0.85 },
                { "cargo.salt_spices", 0.85 },
                { "cargo.sand", 0.50 },
                { "cargo.sandwch_pnls", 0.50 },
                { "cargo.sausages", 0.50 },
                { "cargo.sawpanels", 0.50 },
                { "cargo.scaffoldings", 0.50 },
                { "cargo.scooters", 0.50 },
                { "cargo.scrap_metals", 0.27 },
                { "cargo.seal_bearing", 0.50 },
                { "cargo.sheep_wool", 0.50 },
                { "cargo.shock_absorb", 0.85 },
                { "cargo.silica", 0.85 },
                { "cargo.smokd_eel", 0.50 },
                { "cargo.smokd_sprats", 0.50 },
                { "cargo.sodchlor", 0.85 },
                { "cargo.sodhydro", 0.85 },
                { "cargo.sodium", 0.85 },
                { "cargo.soy_milk", 0.50 },
                { "cargo.soy_milk_t", 0.50 },
                { "cargo.spher_valves", 0.50 },
                { "cargo.sprayer", 0.50 },
                { "cargo.sq_tub", 0.50 },
                { "cargo.square_baler", 0.50 },
                { "cargo.steel_cord", 0.85 },
                { "cargo.stone_dust", 0.50 },
                { "cargo.stone_wool", 0.50 },
                { "cargo.stones", 0.50 },
                { "cargo.straw_bales", 0.50 },
                { "cargo.sugar", 0.50 },
                { "cargo.sugar_b", 0.50 },
                { "cargo.sulfur", 0.50 },
                { "cargo.sulfuric", 0.85 },
                { "cargo.sulfuric_t", 0.85 },
                { "cargo.tableware", 0.50 },
                { "cargo.ter_forklift", 0.50 },
                { "cargo.tmp_00", 0.50 },
                { "cargo.tomatoes", 0.50 },
                { "cargo.toys", 0.50 },
                { "cargo.tracks", 0.50 },
                { "cargo.tractor", 0.50 },
                { "cargo.tractor_au", 0.50 },
                { "cargo.tractor_c", 0.50 },
                { "cargo.tractors", 0.50 },
                { "cargo.train_part", 0.50 },
                { "cargo.train_part2", 0.50 },
                { "cargo.transformat", 0.50 },
                { "cargo.transmis", 0.85 },
                { "cargo.truck_batt", 0.85 },
                { "cargo.truck_batt_c", 0.85 },
                { "cargo.truck_rims", 0.50 },
                { "cargo.truck_rims_c", 0.50 },
                { "cargo.truck_tyres", 0.85 },
                { "cargo.tyres", 0.85 },
                { "cargo.used_battery", 0.50 },
                { "cargo.used_packag", 0.50 },
                { "cargo.used_plast", 0.50 },
                { "cargo.used_plast_c", 0.50 },
                { "cargo.vans_fd", 0.50 },
                { "cargo.vans_id", 0.50 },
                { "cargo.vans_vt", 0.50 },
                { "cargo.vent_tube", 0.50 },
                { "cargo.vinegar", 0.50 },
                { "cargo.vinegar_c", 0.50 },
                { "cargo.volvo_cars", 0.50 },
                { "cargo.volvo_tr", 0.50 },
                { "cargo.wallpanels", 0.50 },
                { "cargo.watermelons", 0.50 },
                { "cargo.watertank", 0.50 },
                { "cargo.wheat", 0.50 },
                { "cargo.windml_tube", 0.50 },
                { "cargo.wood_bark", 0.50 },
                { "cargo.wooden_beams", 0.50 },
                { "cargo.wrk_cloth", 0.50 },
                { "cargo.wshavings", 0.50 },
                { "cargo.yacht", 0.50 },
                { "cargo.yogurt", 0.50 },
                { "cargo.young_seed", 0.50 },
        };

        /// <summary>Number of cargo types in the catalogue.</summary>
        public static int Count => Rates.Count;

        /// <summary>
        /// 可选货物清单：游戏实际在用的全部货物（431 项）。
        /// <para>
        /// 三个来源合并：游戏数据文件（def.scs + 全部 dlc_*.scs）提取的显示名表
        /// （<see cref="GameNames.Cargo"/>）、费率表，以及从真实存档实测出来的
        /// 费率表。
        /// </para>
        /// <para>
        /// <b>为什么不能只用 <see cref="GameNames.Cargo"/>：</b>有一部分游戏实际
        /// 在用的货物**没有 cargo_data 定义**（可能是游戏内置，或定义在本工具
        /// 没覆盖到的位置），例如通用货运 <c>cargo.haulie</c> —— 游戏自己的
        /// 任务里引用了它 183 次，只能从存档实测表里拿到。只用定义表会漏掉
        /// 33 种游戏在用的货物，玩家在货运市场里就选不到。
        /// </para>
        /// <para>
        /// ⚠️ <b>一条容易踩的推理错误</b>：<b>在游戏数据文件里搜不到某个 token
        /// 的定义，只能说明「定义不在那儿」，推不出「游戏不用它」</b>。本工具、
        /// 测试和产品都曾据此把 <c>cargo.haulie</c> 当成过时 token —— 而它恰恰是
        /// 游戏引用最多的通用货运货物。判断一个 token 能不能用，要看**游戏自己
        /// 的任务引用了它多少次**，而不是看数据文件里有没有它的定义。
        /// </para>
        /// <para>
        /// <b>为什么排除 <c>cargo_obsole.*</c>：</b>那是游戏自己标注的**旧名**
        /// （游戏的改名映射表 <c>cargo_name_mapping</c> 里 73 对映射无一涉及
        /// obsolete；存档里游戏生成的任务也从不引用它们，只有历史遗留的少数
        /// 几种被引用过）。放进可选清单只会让用户选到游戏不会派发的货。
        /// </para>
        /// </summary>
        public static IEnumerable<string> SelectableTokens => AllTokenList.Value;

        /// <summary>
        /// 货物 token 全集（懒加载）。
        /// </summary>
        private static readonly Lazy<List<string>> AllTokenList =
            new Lazy<List<string>>(BuildAllTokens);

        /// <summary>合并三个来源，得到可选货物清单。</summary>
        private static List<string> BuildAllTokens()
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string token in GameNames.Cargo.Keys)
            {
                // 游戏自身从不引用的旧名（cargo_obsole.*），见 SelectableTokens。
                if (token.StartsWith("cargo_obsole.", StringComparison.OrdinalIgnoreCase)) continue;
                if (seen.Add(token)) result.Add(token);
            }

            foreach (string token in Rates.Keys)
            {
                if (seen.Add(token)) result.Add(token);
            }

            foreach (string token in MeasuredRates.Keys)
            {
                if (seen.Add(token)) result.Add(token);
            }

            return result;
        }

        /// <summary>
        /// Returns the income rate of the given cargo token
        /// (euros per unit per kilometre).
        /// <para>
        /// Measured rates (taken from the delivery records of a real save)
        /// win over the fallback table; unknown cargoes use the median rate
        /// observed in the same save.
        /// </para>
        /// </summary>
        public static double RateOf(string cargoToken)
        {
            double rate;
            if (string.IsNullOrEmpty(cargoToken)) return DefaultRate;

            if (MeasuredRates.TryGetValue(cargoToken, out rate)) return rate;
            if (Rates.TryGetValue(cargoToken, out rate)) return rate;

            return DefaultRate;
        }

        /// <summary>
        /// Rate used for cargoes that are not in the catalogue: the median
        /// rate measured in a real save (0.48 €/unit/km).
        /// </summary>
        public const double DefaultRate = 0.48;

        /// <summary>
        /// Returns a human-readable name for a cargo token.
        /// <para>
        /// 四级查表：
        /// <list type="number">
        /// <item><see cref="GameNames.Cargo"/> —— 权威表（def.scs + 全部 dlc_*.scs 的
        /// cargo_data 合并，489 项）。</item>
        /// <item><see cref="GameNames.LocalizedCargo"/> —— 本地化兜底（locale.scs 的
        /// cn_* 键，425 项），覆盖 cargo_data 没收录的 token。</item>
        /// <item>游戏改名的旧 token：<c>cargo.x_apaints</c> 查不到时，
        /// 试它的过时变体 <c>cargo_obsole.x_apaints</c>（同一个货物）。</item>
        /// <item>最后回退到 token 本身的可读形式（<c>cargo.haulie</c> → "Haulie"）。</item>
        /// </list>
        /// </para>
        /// <para>
        /// 英文本地化里「散装 / 箱装 / 罐装」是同一个名字（Apples / Apples_c、
        /// Milk / Milk_t …），所以**不同 token 会撞名**。这里给撞名的 token
        /// 加上 token 后缀（<c>Apples (cargo.apples_c)</c>），保证两个不同的
        /// 货物永远不会显示成同一个名字。
        /// </para>
        /// </summary>
        public static string DisplayName(string cargoToken)
        {
            if (string.IsNullOrEmpty(cargoToken)) return string.Empty;

            var map = DisplayNameMap.Value;
            string name;
            if (map.TryGetValue(cargoToken, out name)) return name;

            // 目录里没有的 token（老存档残留）：算一个可读名字；
            // 若与目录里的某个名字撞了，补上 token 以保证两两可区分。
            string fallback = BaseName(cargoToken);
            if (TakenNames.Value.Contains(fallback)) return fallback + " (" + cargoToken + ")";
            return fallback;
        }

        /// <summary>
        /// 显示名表：token → 最终显示名（撞名的已加 token 后缀）。懒加载。
        /// </summary>
        private static readonly Lazy<Dictionary<string, string>> DisplayNameMap =
            new Lazy<Dictionary<string, string>>(BuildDisplayNames);

        /// <summary>token → 基础名（未消歧）。懒加载。</summary>
        private static readonly Lazy<Dictionary<string, string>> BaseNames =
            new Lazy<Dictionary<string, string>>(BuildBaseNames);

        /// <summary>目录里所有用过的「基础名」（未加后缀），用于查重。</summary>
        private static readonly Lazy<HashSet<string>> TakenNames =
            new Lazy<HashSet<string>>(BuildTakenNames);

        /// <summary>
        /// 计算所有已知 token 的显示名：先算基础名，同名的整组都加上 token 后缀。
        /// <para>
        /// 只对**游戏真实定义过的货物**（<see cref="GameNames.Cargo"/>）分组：
        /// 本地化兜底表里的键是按 <c>cn_&lt;尾段&gt;</c> 机械转换来的，里面
        /// 混着「同一件东西的不同缩写」（cn_aircft_tires 与 cn_aircraft_tires），
        /// 拿它们参与分组会把几乎所有名字都判成撞名。
        /// </para>
        /// </summary>
        private static Dictionary<string, string> BuildDisplayNames()
        {
            var baseNames = BaseNames.Value;
            var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in baseNames)
            {
                List<string> group;
                if (!groups.TryGetValue(pair.Value, out group))
                {
                    group = new List<string>();
                    groups[pair.Value] = group;
                }
                group.Add(pair.Key);
            }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in baseNames)
            {
                List<string> group = groups[pair.Value];
                result[pair.Key] = group.Count > 1
                    ? pair.Value + " (" + pair.Key + ")"
                    : pair.Value;
            }
            return result;
        }

        /// <summary>计算每个已知 token 的「基础名」（未消歧）。</summary>
        private static Dictionary<string, string> BuildBaseNames()
        {
            var baseNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string token in GameNames.Cargo.Keys)
            {
                string baseName = BaseName(token);
                if (string.IsNullOrEmpty(baseName)) continue;
                baseNames[token] = baseName;
            }
            return baseNames;
        }

        /// <summary>
        /// 所有基础名（未加后缀），用于判断某个名字是否已被占用。
        /// <para>
        /// **正向收集**（而不是从最终名里反向剥离 " ("）：万一将来某个官方名本身
        /// 带括号（例如 "Cargo (Special)"），反向剥离会把它误伤成 "Cargo"。
        /// </para>
        /// </summary>
        private static HashSet<string> BuildTakenNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in BaseNames.Value)
            {
                names.Add(pair.Value);
            }
            return names;
        }

        /// <summary>
        /// 计算 token 的「基础名」：四级查表，不做撞名处理。
        /// </summary>
        private static string BaseName(string cargoToken)
        {
            if (string.IsNullOrEmpty(cargoToken)) return string.Empty;

            string name = GameNames.Lookup(GameNames.Cargo, cargoToken);
            if (!string.IsNullOrEmpty(name)) return name;

            // 本地化兜底：cargo_data 没收录的 token。
            name = GameNames.Lookup(GameNames.LocalizedCargo, cargoToken);
            if (!string.IsNullOrEmpty(name)) return name;

            // 游戏改名的旧 token：cargo.x_xxx 的过时变体是 cargo_obsole.x_xxx。
            // （已知例外：存档里的 cargo.x_apaints 对应的旧名是单数的
            //  cargo_obsole.x_apaint，差一个字母，这条规则匹配不到，会退回
            //  token 形式 "X Apaints"。它在存档里是历史遗留，游戏任务从不
            //  引用，可读性也够，不值得为它引入模糊匹配。）
            if (cargoToken.StartsWith("cargo.", StringComparison.OrdinalIgnoreCase))
            {
                string obsolete = "cargo_obsole." + cargoToken.Substring("cargo.".Length);
                name = GameNames.Lookup(GameNames.Cargo, obsolete);
                if (!string.IsNullOrEmpty(name)) return name;
            }

            // 回退：把 token 变成可读形式。先去掉命名空间前缀
            // （cargo. / company. 等），再把下划线换成空格。
            name = cargoToken;
            foreach (string prefix in new[] { "cargo.", "company.", "trailer.", "brand." })
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(prefix.Length);
                    break;
                }
            }
            int dot = name.IndexOf('.');
            if (dot >= 0 && dot < name.Length - 1) name = name.Substring(dot + 1);
            name = name.Replace('_', ' ');

            // 每个词首字母大写：老 token（cargo.haulie / cargo.tractor_au）虽然
            // 查不到正式名称，也要显示成 "Haulie" / "Tractor Au"。
            var words = name.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length == 0) continue;
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            }
            return string.Join(" ", words);
        }
    }
}
