#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Antigravity.MCP.Editor
{
    /// <summary>
    /// Routes incoming JSON commands to the appropriate handler class.
    /// Parses the "tool" field and delegates execution.
    /// </summary>
    public static class CommandDispatcher
    {
        [Serializable]
        private class IncomingCommand
        {
            public string id;
            public string tool;
            public string parameters; // raw JSON string
        }

        private static readonly Dictionary<string, Func<string, string, Task<string>>> _handlers 
            = new Dictionary<string, Func<string, string, Task<string>>>();

        static CommandDispatcher()
        {
            // === Phase 1: Core (19 tools) ===
            // Scene tools
            Register("unity_scene_load", Handlers.SceneHandler.Handle);
            Register("unity_scene_create", Handlers.SceneHandler.Handle);
            Register("unity_scene_save", Handlers.SceneHandler.Handle);
            Register("unity_scene_list", Handlers.SceneHandler.Handle);

            // GameObject tools
            Register("unity_object_create", Handlers.GameObjectHandler.Handle);
            Register("unity_object_delete", Handlers.GameObjectHandler.Handle);
            Register("unity_object_find", Handlers.GameObjectHandler.Handle);
            Register("unity_object_inspect", Handlers.GameObjectHandler.Handle);
            Register("unity_object_update", Handlers.GameObjectHandler.Handle);
            Register("unity_object_duplicate", Handlers.GameObjectHandler.Handle);
            Register("unity_object_find_by_path", Handlers.GameObjectHandler.Handle);

            // Component tools
            Register("unity_component_add", Handlers.ComponentHandler.Handle);
            Register("unity_component_remove", Handlers.ComponentHandler.Handle);
            Register("unity_component_update", Handlers.ComponentHandler.Handle);

            // Hierarchy tools
            Register("unity_hierarchy_list", Handlers.HierarchyHandler.Handle);
            Register("unity_hierarchy_reparent", Handlers.HierarchyHandler.Handle);

            // Editor control tools
            Register("unity_play_control", Handlers.EditorControlHandler.Handle);
            Register("unity_refresh_assets", Handlers.EditorControlHandler.Handle);
            Register("unity_get_compilation_result", Handlers.EditorControlHandler.Handle);
            Register("unity_execute_menu_item", Handlers.EditorControlHandler.Handle);
            Register("unity_get_editor_state", Handlers.EditorControlHandler.Handle);

            // === Phase 2: Material + Prefab + Asset + Import (21 tools) ===
            Register("unity_material_create", Handlers.MaterialHandler.Handle);
            Register("unity_material_assign", Handlers.MaterialHandler.Handle);
            Register("unity_material_set_property", Handlers.MaterialHandler.Handle);
            Register("unity_material_get_properties", Handlers.MaterialHandler.Handle);
            Register("unity_material_set_texture", Handlers.MaterialHandler.Handle);

            Register("unity_prefab_create", Handlers.PrefabHandler.Handle);
            Register("unity_prefab_instantiate", Handlers.PrefabHandler.Handle);
            Register("unity_prefab_apply_overrides", Handlers.PrefabHandler.Handle);
            Register("unity_prefab_revert", Handlers.PrefabHandler.Handle);
            Register("unity_prefab_unpack", Handlers.PrefabHandler.Handle);

            Register("unity_asset_import", Handlers.AssetHandler.Handle);
            Register("unity_asset_move", Handlers.AssetHandler.Handle);
            Register("unity_asset_delete", Handlers.AssetHandler.Handle);
            Register("unity_asset_find", Handlers.AssetHandler.Handle);
            Register("unity_asset_get_dependencies", Handlers.AssetHandler.Handle);
            Register("unity_asset_set_labels", Handlers.AssetHandler.Handle);
            Register("unity_scriptable_object_create", Handlers.AssetHandler.Handle);

            Register("unity_texture_import_settings", Handlers.ImportSettingsHandler.Handle);
            Register("unity_model_import_settings", Handlers.ImportSettingsHandler.Handle);
            Register("unity_audio_import_settings", Handlers.ImportSettingsHandler.Handle);
            Register("unity_asset_postprocessor_add", Handlers.ImportSettingsHandler.Handle);

            // === Phase 3: Physics + Lighting + NavMesh + LOD (20 tools) ===
            Register("unity_physics_raycast", Handlers.PhysicsHandler.Handle);
            Register("unity_physics_overlap", Handlers.PhysicsHandler.Handle);
            Register("unity_physics_settings", Handlers.PhysicsHandler.Handle);
            Register("unity_physics_set_collision_matrix", Handlers.PhysicsHandler.Handle);

            Register("unity_light_create", Handlers.LightingHandler.Handle);
            Register("unity_light_bake", Handlers.LightingHandler.Handle);
            Register("unity_reflection_probe_add", Handlers.LightingHandler.Handle);
            Register("unity_light_probe_group", Handlers.LightingHandler.Handle);
            Register("unity_environment_settings", Handlers.LightingHandler.Handle);

            Register("unity_navmesh_bake", Handlers.NavMeshHandler.Handle);
            Register("unity_navmesh_agent_setup", Handlers.NavMeshHandler.Handle);
            Register("unity_navmesh_obstacle_add", Handlers.NavMeshHandler.Handle);
            Register("unity_navmesh_set_area", Handlers.NavMeshHandler.Handle);
            Register("unity_navmesh_find_path", Handlers.NavMeshHandler.Handle);

            Register("unity_lod_group_setup", Handlers.LODPerformanceHandler.Handle);
            Register("unity_occlusion_bake", Handlers.LODPerformanceHandler.Handle);
            Register("unity_static_flags_set", Handlers.LODPerformanceHandler.Handle);
            Register("unity_gpu_instancing_enable", Handlers.LODPerformanceHandler.Handle);
            Register("unity_profiler_capture", Handlers.LODPerformanceHandler.Handle);
            Register("unity_memory_snapshot", Handlers.LODPerformanceHandler.Handle);

            // === Phase 4: Animation + Audio (16 tools) ===
            Register("unity_animator_create_controller", Handlers.AnimationHandler.Handle);
            Register("unity_animator_add_state", Handlers.AnimationHandler.Handle);
            Register("unity_animator_add_transition", Handlers.AnimationHandler.Handle);
            Register("unity_animator_set_parameter", Handlers.AnimationHandler.Handle);
            Register("unity_animation_clip_create", Handlers.AnimationHandler.Handle);
            Register("unity_playable_graph_create", Handlers.AnimationHandler.Handle);
            Register("unity_playable_mixer_blend", Handlers.AnimationHandler.Handle);

            Register("unity_audio_source_setup", Handlers.AudioHandler.Handle);
            Register("unity_audio_play", Handlers.AudioHandler.Handle);
            Register("unity_audio_mixer_create", Handlers.AudioHandler.Handle);
            Register("unity_audio_mixer_set_param", Handlers.AudioHandler.Handle);
            Register("unity_audio_mixer_snapshot", Handlers.AudioHandler.Handle);
            Register("unity_audio_listener_setup", Handlers.AudioHandler.Handle);

            // === Phase 5: UI + Rendering + Settings (20 tools) ===
            Register("unity_ui_query", Handlers.UIToolkitHandler.Handle);
            Register("unity_ui_create_element", Handlers.UIToolkitHandler.Handle);
            Register("unity_ui_set_style", Handlers.UIToolkitHandler.Handle);
            Register("unity_ui_bind_data", Handlers.UIToolkitHandler.Handle);
            Register("unity_ui_generate_uxml", Handlers.UIToolkitHandler.Handle);
            Register("unity_ui_generate_uss", Handlers.UIToolkitHandler.Handle);
            Register("unity_ui_register_callback", Handlers.UIToolkitHandler.Handle);

            Register("unity_camera_setup", Handlers.RenderingHandler.Handle);
            Register("unity_post_processing_add", Handlers.RenderingHandler.Handle);
            Register("unity_render_settings", Handlers.RenderingHandler.Handle);
            Register("unity_screenshot_capture", Handlers.RenderingHandler.Handle);
            Register("unity_cinemachine_vcam_create", Handlers.RenderingHandler.Handle);
            Register("unity_cinemachine_set_body_aim", Handlers.RenderingHandler.Handle);

            Register("unity_player_settings", Handlers.PlayerSettingsHandler.Handle);
            Register("unity_player_resolution", Handlers.PlayerSettingsHandler.Handle);
            Register("unity_time_settings", Handlers.PlayerSettingsHandler.Handle);
            Register("unity_color_space", Handlers.PlayerSettingsHandler.Handle);
            Register("unity_graphics_api", Handlers.PlayerSettingsHandler.Handle);
            Register("unity_scripting_backend", Handlers.PlayerSettingsHandler.Handle);
            Register("unity_script_execution_order", Handlers.PlayerSettingsHandler.Handle);

            // === Phase 6: 2D + Spline + Terrain (20 tools) ===
            Register("unity_sprite_create", Handlers.TwoDHandler.Handle);
            Register("unity_sprite_atlas_create", Handlers.TwoDHandler.Handle);
            Register("unity_tilemap_create", Handlers.TwoDHandler.Handle);
            Register("unity_tilemap_set_tile", Handlers.TwoDHandler.Handle);
            Register("unity_tilemap_paint_area", Handlers.TwoDHandler.Handle);
            Register("unity_tilemap_clear", Handlers.TwoDHandler.Handle);
            Register("unity_2d_physics_setup", Handlers.TwoDHandler.Handle);
            Register("unity_sorting_layer_manage", Handlers.TwoDHandler.Handle);
            Register("unity_sprite_shape_create", Handlers.TwoDHandler.Handle);

            Register("unity_spline_create", Handlers.SplineHandler.Handle);
            Register("unity_spline_add_knot", Handlers.SplineHandler.Handle);
            Register("unity_spline_extrude_mesh", Handlers.SplineHandler.Handle);
            Register("unity_spline_animate", Handlers.SplineHandler.Handle);
            Register("unity_spline_instantiate", Handlers.SplineHandler.Handle);

            Register("unity_terrain_create", Handlers.TerrainHandler.Handle);
            Register("unity_terrain_set_heightmap", Handlers.TerrainHandler.Handle);
            Register("unity_terrain_paint_texture", Handlers.TerrainHandler.Handle);
            Register("unity_terrain_place_trees", Handlers.TerrainHandler.Handle);
            Register("unity_terrain_place_details", Handlers.TerrainHandler.Handle);
            Register("unity_terrain_set_settings", Handlers.TerrainHandler.Handle);

            // === Phase 7: VFX + ProBuilder + Editor Utils (18 tools) ===
            Register("unity_particle_create", Handlers.ParticleVFXHandler.Handle);
            Register("unity_particle_set_module", Handlers.ParticleVFXHandler.Handle);
            Register("unity_particle_play_stop", Handlers.ParticleVFXHandler.Handle);
            Register("unity_vfx_graph_create", Handlers.ParticleVFXHandler.Handle);

            Register("unity_probuilder_create_shape", Handlers.ProBuilderHandler.Handle);
            Register("unity_probuilder_extrude_face", Handlers.ProBuilderHandler.Handle);
            Register("unity_probuilder_set_material", Handlers.ProBuilderHandler.Handle);
            Register("unity_probuilder_merge", Handlers.ProBuilderHandler.Handle);
            Register("unity_probuilder_export_mesh", Handlers.ProBuilderHandler.Handle);
            Register("unity_probuilder_boolean", Handlers.ProBuilderHandler.Handle);

            Register("unity_console_get_logs", Handlers.EditorUtilityHandler.Handle);
            Register("unity_console_clear", Handlers.EditorUtilityHandler.Handle);
            Register("unity_selection_set", Handlers.EditorUtilityHandler.Handle);
            Register("unity_scene_view_focus", Handlers.EditorUtilityHandler.Handle);
            Register("unity_scene_view_set_camera", Handlers.EditorUtilityHandler.Handle);
            Register("unity_undo_perform", Handlers.EditorUtilityHandler.Handle);
            Register("unity_editor_prefs", Handlers.EditorUtilityHandler.Handle);
            Register("unity_run_tests", Handlers.EditorUtilityHandler.Handle);

            // === Phase 8: Multiplayer + ECS + Sentis + Addressables (18 tools) ===
            Register("unity_netcode_setup", Handlers.NetcodeHandler.Handle);
            Register("unity_network_object_create", Handlers.NetcodeHandler.Handle);
            Register("unity_network_variable_add", Handlers.NetcodeHandler.Handle);
            Register("unity_network_rpc_define", Handlers.NetcodeHandler.Handle);
            Register("unity_multiplayer_test", Handlers.NetcodeHandler.Handle);

            Register("unity_ecs_create_world", Handlers.ECSHandler.Handle);
            Register("unity_ecs_create_entity", Handlers.ECSHandler.Handle);
            Register("unity_ecs_add_system", Handlers.ECSHandler.Handle);
            Register("unity_ecs_query", Handlers.ECSHandler.Handle);
            Register("unity_ecs_subscene_create", Handlers.ECSHandler.Handle);

            Register("unity_sentis_load_model", Handlers.SentisHandler.Handle);
            Register("unity_sentis_run_inference", Handlers.SentisHandler.Handle);
            Register("unity_sentis_get_output", Handlers.SentisHandler.Handle);
            Register("unity_sentis_set_backend", Handlers.SentisHandler.Handle);

            Register("unity_addressable_mark", Handlers.AddressablesHandler.Handle);
            Register("unity_addressable_group_create", Handlers.AddressablesHandler.Handle);
            Register("unity_addressable_build", Handlers.AddressablesHandler.Handle);
            Register("unity_addressable_load_test", Handlers.AddressablesHandler.Handle);

            // === Additional: Script + Build + Package (10 tools) ===
            Register("unity_create_script", Handlers.ScriptHandler.Handle);
            Register("unity_read_script", Handlers.ScriptHandler.Handle);
            Register("unity_edit_script", Handlers.ScriptHandler.Handle);

            Register("unity_build_player", Handlers.BuildHandler.Handle);
            Register("unity_build_settings", Handlers.BuildHandler.Handle);
            Register("unity_build_scene_list", Handlers.BuildHandler.Handle);

            Register("unity_package_remove", Handlers.PackageHandler.Handle);
            Register("unity_package_search", Handlers.PackageHandler.Handle);

            // === Phase 2: AI Autonomy Tools ===
            Register("unity_dev_get_compile_errors", Handlers.DeveloperToolsHandler.Handle);
            Register("unity_dev_find_missing_references", Handlers.DeveloperToolsHandler.Handle);
            Register("unity_dev_find_asset_dependencies", Handlers.DeveloperToolsHandler.Handle);

            // === Phase 2: UI Export & Visual Systems ===
            Register("unity_ui_dump_hierarchy", Handlers.UIExtractorHandler.Handle);
            Register("unity_shader_get_properties", Handlers.ShaderAnalyzerHandler.Handle);
        }

        private static void Register(string toolName, Func<string, string, Task<string>> handler)
        {
            _handlers[toolName] = handler;
        }

        /// <summary>
        /// Dispatches a raw JSON message to the correct handler.
        /// Returns a JSON response string with the result or error.
        /// </summary>
        public static async Task<string> DispatchAsync(string rawJson)
        {
            string id = "0";
            try
            {
                // Parse just the envelope
                var parsed = JsonUtility.FromJson<IncomingCommand>(rawJson);
                
                // Extract id and tool; parameters are the raw JSON
                id = parsed.id ?? "0";
                var tool = parsed.tool;

                if (string.IsNullOrEmpty(tool))
                {
                    return CreateErrorResponse(id, "Missing 'tool' field in command");
                }

                // Find parameters in raw JSON (JsonUtility can't handle nested raw JSON)
                var parametersJson = ExtractParametersJson(rawJson);

                if (!_handlers.TryGetValue(tool, out var handler))
                {
                    return CreateErrorResponse(id, $"Unknown tool: {tool}");
                }

                // Check if tool is enabled in the registry
                if (!McpToolRegistry.IsToolEnabled(tool))
                {
                    return CreateErrorResponse(id, 
                        $"Tool '{tool}' is disabled. Enable it from Window > Antigravity > MCP Control Panel.");
                }

                // Rate limiting
                if (!SecurityGuard.CheckRateLimit())
                {
                    return CreateErrorResponse(id, "Rate limit exceeded. Try again later.");
                }

                // Read-only mode check
                if (!SecurityGuard.IsAllowedInReadOnlyMode(tool))
                {
                    return CreateErrorResponse(id, 
                        $"Tool '{tool}' is blocked — MCP is in Read-Only mode.");
                }

                var result = await handler(tool, parametersJson);

                // Audit logging
                SecurityGuard.LogCommand(tool, result);

                return CreateSuccessResponse(id, result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP] Dispatch error: {ex}");
                return CreateErrorResponse(id, ex.Message);
            }
        }

        /// <summary>
        /// Extracts the "parameters" field from raw JSON as a raw string.
        /// Uses simple parsing since JsonUtility doesn't support raw JSON extraction.
        /// </summary>
        private static string ExtractParametersJson(string json)
        {
            // Find "parameters" key and extract its value
            var key = "\"parameters\"";
            var idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx == -1) return "{}";

            idx += key.Length;
            // Skip whitespace and colon
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':' || json[idx] == '\t'))
                idx++;

            if (idx >= json.Length) return "{}";

            // Find the start of the value
            if (json[idx] == '{')
            {
                // Find matching closing brace
                int depth = 0;
                int start = idx;
                bool inString = false;
                for (int i = idx; i < json.Length; i++)
                {
                    char c = json[i];
                    if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                        inString = !inString;
                    if (!inString)
                    {
                        if (c == '{') depth++;
                        else if (c == '}') { depth--; if (depth == 0) return json.Substring(start, i - start + 1); }
                    }
                }
            }

            return "{}";
        }

        public static string CreateSuccessResponse(string id, string resultJson)
        {
            // Build JSON manually to avoid JsonUtility limitations with raw JSON embedding
            return $"{{\"id\":\"{EscapeJson(id)}\",\"result\":{resultJson},\"isError\":false}}";
        }

        public static string CreateErrorResponse(string id, string errorMessage)
        {
            return $"{{\"id\":\"{EscapeJson(id)}\",\"error\":\"{EscapeJson(errorMessage)}\",\"isError\":true}}";
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }
    }
}
#endif
