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
        /// The display names live in the game's own data files, not in the
        /// save file. They are extracted into <see cref="GameNames.Cargo"/>
        /// by <c>tools/extract_names.py</c>. When a token is not in that
        /// table the token itself is shown in a readable form instead.
        /// </para>
        /// </summary>
        public static string DisplayName(string cargoToken)
        {
            if (string.IsNullOrEmpty(cargoToken)) return string.Empty;

            string name = GameNames.Lookup(GameNames.Cargo, cargoToken);
            if (!string.IsNullOrEmpty(name)) return name;

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
            return name.Replace('_', ' ');
        }

        /// <summary>All cargo tokens of the catalogue.</summary>
        public static IEnumerable<string> Tokens => Rates.Keys;
    }
}
