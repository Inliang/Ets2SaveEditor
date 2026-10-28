using System;
using System.Collections.Generic;

namespace Ets2SaveEditor.Core.Sii
{
    /// <summary>
    /// Built-in schema of the modern (flat) ETS2/ATS save format.
    /// <para>
    /// Binary saves carry their own structure definitions, so their schema is
    /// extracted from the file itself and is always authoritative. Plaintext
    /// saves have no such information, so this table is used as a fallback —
    /// it lists the attributes that the modern format stores as arrays.
    /// </para>
    /// <para>
    /// Without this knowledge an empty array (<c>name: 0</c> with no indexed
    /// lines) cannot be told apart from an ordinary integer attribute, which
    /// would make the tool write an array with one element back and corrupt
    /// the save file.
    /// </para>
    /// </summary>
    public static class BuiltInSiiSchema
    {
        private static readonly Dictionary<string, string[]> Fields =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "bank", new[] { "loans" } },
            { "bus_job_log", new[] { "entries" } },
            { "bus_stop", new[] { "lines_offer" } },
            { "car_job_generator", new[] { "car_offers" } },
            { "car_job_log", new[] { "entries" } },
            { "company", new[] { "cargo_offer_seeds", "delivered_pos", "job_offer" } },
            { "delivery_log", new[] { "entries" } },
            { "delivery_log_entry", new[] { "params" } },
            { "economy", new[] { "achieved_feats", "bus_stops", "companies", "discovered_cutscene_items", "discovered_cutscene_items_states", "discovered_items", "discovered_roads", "driver_pool", "drivers_offer", "garage_ignore_list", "garages", "relations", "screen_access_list", "screen_visit_list", "stored_gps_ahead", "stored_gps_ahead_waypoints", "stored_gps_avoid_waypoints", "stored_gps_behind", "stored_gps_behind_waypoints", "stored_gps_waypoints_history", "stored_map_actions", "stored_online_gps_ahead", "stored_online_gps_ahead_waypoints", "stored_online_gps_avoid_waypoints", "stored_online_gps_behind", "stored_online_gps_behind_waypoints", "stored_online_gps_waypoints_history", "total_distances_by_mode", "total_game_times_by_mode", "total_real_times_by_mode", "tourist_atlas_log_flags", "tourist_atlas_log_units", "transported_cargo_types", "unlocked_dealers", "unlocked_recruitments", "user_colors", "visited_cities", "visited_cities_count" } },
            { "economy_event_queue", new[] { "data" } },
            { "ferry_log", new[] { "entries" } },
            { "game_progress", new[] { "owned_trucks" } },
            { "garage", new[] { "drivers", "trailers", "vehicles" } },
            { "job_offer_data", new[] { "trailer_place" } },
            { "mail_ctrl", new[] { "inbox", "pending_mails", "pmail_timers" } },
            { "mail_def", new[] { "param_keys", "param_values" } },
            { "map_action", new[] { "id_params", "num_params", "str_params", "target_tags" } },
            { "oversize_offer_ctrl", new[] { "route_offers" } },
            { "oversize_route_offers", new[] { "offers" } },
            { "player", new[] { "buses", "cars", "dismissed_drivers", "driver_flags", "driver_quit_warned", "driver_readiness_timer", "driver_undrivable_truck_timers", "drivers", "my_slave_trailer_placements", "my_vehicles", "slave_trailer_placements", "trailer_defs", "trailer_utilization_logs", "trailers", "truck_profit_logs", "trucks" } },
            { "player_vehicles", new[] { "stored_trailer_placements" } },
            { "police_ctrl", new[] { "offence_counter", "offence_timer", "offence_valid" } },
            { "police_offence_log", new[] { "detailed_history_entries", "offence_total_counts", "offence_total_fines" } },
            { "profit_log", new[] { "stats_data" } },
            { "registry", new[] { "data", "index", "keys", "valid" } },
            { "trailer", new[] { "accessories", "wheels_wear", "wheels_wear_unfixable" } },
            { "trailer_def", new[] { "country_validity", "mass_ratio" } },
            { "trailer_utilization_log", new[] { "entries" } },
            { "transport_data", new[] { "count_per_adr", "count_per_dock", "docks" } },
            { "used_vehicle_assortment", new[] { "trucks" } },
            { "vehicle", new[] { "accessories", "user_mirror_rot", "wheels_wear", "wheels_wear_unfixable" } },
            { "vehicle_addon_accessory", new[] { "slot_hookup", "slot_name" } },
            { "vehicle_drv_plate_accessory", new[] { "slot_hookup", "slot_name" } },
        };

        /// <summary>
        /// Returns the built-in schema of the modern save format.
        /// </summary>
        public static SiiSchema Instance { get; } = Build();

        private static SiiSchema Build()
        {
            var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var kv in Fields)
            {
                map[kv.Key] = new HashSet<string>(kv.Value, StringComparer.Ordinal);
            }
            return new SiiSchema(map);
        }
    }
}