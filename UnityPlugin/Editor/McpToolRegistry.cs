#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor
{
    /// <summary>
    /// Manages tool enable/disable state per category.
    /// Persists state via EditorPrefs with prefix "MCP_CAT_".
    /// Supports presets for quick configuration.
    /// </summary>
    public static class McpToolRegistry
    {
        // ─────────────────────────────────────────
        //  Category definitions
        // ─────────────────────────────────────────
        public class ToolCategory
        {
            public string Id;
            public string DisplayName;
            public string[] Tools;
            public bool DefaultEnabled;
        }

        private const string PREFS_PREFIX = "MCP_CAT_";

        public static readonly ToolCategory[] Categories = new[]
        {
            new ToolCategory { Id = "scene", DisplayName = "Scene (4)", DefaultEnabled = true,
                Tools = new[] { "unity_scene_load","unity_scene_create","unity_scene_save","unity_scene_list" } },
            new ToolCategory { Id = "gameobject", DisplayName = "GameObject (7)", DefaultEnabled = true,
                Tools = new[] { "unity_object_create","unity_object_delete","unity_object_find","unity_object_inspect","unity_object_update","unity_object_duplicate","unity_object_find_by_path" } },
            new ToolCategory { Id = "component", DisplayName = "Component (3)", DefaultEnabled = true,
                Tools = new[] { "unity_component_add","unity_component_remove","unity_component_update" } },
            new ToolCategory { Id = "hierarchy", DisplayName = "Hierarchy (2)", DefaultEnabled = true,
                Tools = new[] { "unity_hierarchy_list","unity_hierarchy_reparent" } },
            new ToolCategory { Id = "editor", DisplayName = "Editor Control (5)", DefaultEnabled = true,
                Tools = new[] { "unity_play_control","unity_refresh_assets","unity_get_compilation_result","unity_execute_menu_item","unity_get_editor_state" } },
            new ToolCategory { Id = "material", DisplayName = "Material & Shader (5)", DefaultEnabled = true,
                Tools = new[] { "unity_material_create","unity_material_assign","unity_material_set_property","unity_material_get_properties","unity_material_set_texture" } },
            new ToolCategory { Id = "prefab", DisplayName = "Prefab (5)", DefaultEnabled = true,
                Tools = new[] { "unity_prefab_create","unity_prefab_instantiate","unity_prefab_apply_overrides","unity_prefab_revert","unity_prefab_unpack" } },
            new ToolCategory { Id = "asset", DisplayName = "Asset Management (7)", DefaultEnabled = true,
                Tools = new[] { "unity_asset_import","unity_asset_move","unity_asset_delete","unity_asset_find","unity_asset_get_dependencies","unity_asset_set_labels","unity_scriptable_object_create" } },
            new ToolCategory { Id = "import", DisplayName = "Import Settings (4)", DefaultEnabled = true,
                Tools = new[] { "unity_texture_import_settings","unity_model_import_settings","unity_audio_import_settings","unity_asset_postprocessor_add" } },
            new ToolCategory { Id = "physics", DisplayName = "Physics (4)", DefaultEnabled = true,
                Tools = new[] { "unity_physics_raycast","unity_physics_overlap","unity_physics_settings","unity_physics_set_collision_matrix" } },
            new ToolCategory { Id = "lighting", DisplayName = "Lighting (5)", DefaultEnabled = true,
                Tools = new[] { "unity_light_create","unity_light_bake","unity_reflection_probe_add","unity_light_probe_group","unity_environment_settings" } },
            new ToolCategory { Id = "navmesh", DisplayName = "Navigation (5)", DefaultEnabled = true,
                Tools = new[] { "unity_navmesh_bake","unity_navmesh_agent_setup","unity_navmesh_obstacle_add","unity_navmesh_set_area","unity_navmesh_find_path" } },
            new ToolCategory { Id = "lod", DisplayName = "LOD & Performance (6)", DefaultEnabled = true,
                Tools = new[] { "unity_lod_group_setup","unity_occlusion_bake","unity_static_flags_set","unity_gpu_instancing_enable","unity_profiler_capture","unity_memory_snapshot" } },
            new ToolCategory { Id = "animation", DisplayName = "Animation (7)", DefaultEnabled = true,
                Tools = new[] { "unity_animator_create_controller","unity_animator_add_state","unity_animator_add_transition","unity_animator_set_parameter","unity_animation_clip_create","unity_playable_graph_create","unity_playable_mixer_blend" } },
            new ToolCategory { Id = "audio", DisplayName = "Audio (6)", DefaultEnabled = true,
                Tools = new[] { "unity_audio_source_setup","unity_audio_play","unity_audio_mixer_create","unity_audio_mixer_set_param","unity_audio_mixer_snapshot","unity_audio_listener_setup" } },
            new ToolCategory { Id = "uitoolkit", DisplayName = "UI Toolkit (7)", DefaultEnabled = true,
                Tools = new[] { "unity_ui_query","unity_ui_create_element","unity_ui_set_style","unity_ui_bind_data","unity_ui_generate_uxml","unity_ui_generate_uss","unity_ui_register_callback" } },
            new ToolCategory { Id = "rendering", DisplayName = "Rendering & Camera (6)", DefaultEnabled = true,
                Tools = new[] { "unity_camera_setup","unity_post_processing_add","unity_render_settings","unity_screenshot_capture","unity_cinemachine_vcam_create","unity_cinemachine_set_body_aim" } },
            new ToolCategory { Id = "playersettings", DisplayName = "Player Settings (7)", DefaultEnabled = true,
                Tools = new[] { "unity_player_settings","unity_player_resolution","unity_time_settings","unity_color_space","unity_graphics_api","unity_scripting_backend","unity_script_execution_order" } },
            new ToolCategory { Id = "twod", DisplayName = "2D Systems (9)", DefaultEnabled = true,
                Tools = new[] { "unity_sprite_create","unity_sprite_atlas_create","unity_tilemap_create","unity_tilemap_set_tile","unity_tilemap_paint_area","unity_tilemap_clear","unity_2d_physics_setup","unity_sorting_layer_manage","unity_sprite_shape_create" } },
            new ToolCategory { Id = "spline", DisplayName = "Spline (5)", DefaultEnabled = true,
                Tools = new[] { "unity_spline_create","unity_spline_add_knot","unity_spline_extrude_mesh","unity_spline_animate","unity_spline_instantiate" } },
            new ToolCategory { Id = "terrain", DisplayName = "Terrain (6)", DefaultEnabled = true,
                Tools = new[] { "unity_terrain_create","unity_terrain_set_heightmap","unity_terrain_paint_texture","unity_terrain_place_trees","unity_terrain_place_details","unity_terrain_set_settings" } },
            new ToolCategory { Id = "vfx", DisplayName = "Particle & VFX (4)", DefaultEnabled = true,
                Tools = new[] { "unity_particle_create","unity_particle_set_module","unity_particle_play_stop","unity_vfx_graph_create" } },
            new ToolCategory { Id = "probuilder", DisplayName = "ProBuilder (6)", DefaultEnabled = false,
                Tools = new[] { "unity_probuilder_create_shape","unity_probuilder_extrude_face","unity_probuilder_set_material","unity_probuilder_merge","unity_probuilder_export_mesh","unity_probuilder_boolean" } },
            new ToolCategory { Id = "editorutils", DisplayName = "Editor Utilities (8)", DefaultEnabled = true,
                Tools = new[] { "unity_console_get_logs","unity_console_clear","unity_selection_set","unity_scene_view_focus","unity_scene_view_set_camera","unity_undo_perform","unity_editor_prefs","unity_run_tests" } },
            new ToolCategory { Id = "netcode", DisplayName = "Multiplayer (5)", DefaultEnabled = false,
                Tools = new[] { "unity_netcode_setup","unity_network_object_create","unity_network_variable_add","unity_network_rpc_define","unity_multiplayer_test" } },
            new ToolCategory { Id = "ecs", DisplayName = "ECS / DOTS (5)", DefaultEnabled = false,
                Tools = new[] { "unity_ecs_create_world","unity_ecs_create_entity","unity_ecs_add_system","unity_ecs_query","unity_ecs_subscene_create" } },
            new ToolCategory { Id = "sentis", DisplayName = "Sentis ML (4)", DefaultEnabled = false,
                Tools = new[] { "unity_sentis_load_model","unity_sentis_run_inference","unity_sentis_get_output","unity_sentis_set_backend" } },
            new ToolCategory { Id = "addressables", DisplayName = "Addressables (4)", DefaultEnabled = false,
                Tools = new[] { "unity_addressable_mark","unity_addressable_group_create","unity_addressable_build","unity_addressable_load_test" } },
            new ToolCategory { Id = "script", DisplayName = "Script Management (3)", DefaultEnabled = true,
                Tools = new[] { "unity_create_script","unity_read_script","unity_edit_script" } },
            new ToolCategory { Id = "build", DisplayName = "Build Pipeline (3)", DefaultEnabled = true,
                Tools = new[] { "unity_build_player","unity_build_settings","unity_build_scene_list" } },
            new ToolCategory { Id = "package", DisplayName = "Package Manager (4)", DefaultEnabled = true,
                Tools = new[] { "unity_package_list","unity_package_add","unity_package_remove","unity_package_search" } },
            new ToolCategory { Id = "devtools", DisplayName = "AI Auto-Fixers (3)", DefaultEnabled = true,
                Tools = new[] { "unity_dev_get_compile_errors","unity_dev_find_missing_references","unity_dev_find_asset_dependencies" } },
            new ToolCategory { Id = "uiexport", DisplayName = "UI Export & Vision (2)", DefaultEnabled = true,
                Tools = new[] { "unity_ui_dump_hierarchy","unity_shader_get_properties" } }
        };

        // Cached enabled state
        private static HashSet<string> _enabledTools;
        private static bool _initialized = false;

        // ─────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────

        public static bool IsToolEnabled(string toolName)
        {
            EnsureInitialized();
            return _enabledTools.Contains(toolName);
        }

        public static bool IsCategoryEnabled(string categoryId)
        {
            return EditorPrefs.GetBool(PREFS_PREFIX + categoryId, GetDefaultForCategory(categoryId));
        }

        public static void SetCategoryEnabled(string categoryId, bool enabled)
        {
            EditorPrefs.SetBool(PREFS_PREFIX + categoryId, enabled);
            RebuildCache();
        }

        public static void EnableAll()
        {
            foreach (var cat in Categories)
                EditorPrefs.SetBool(PREFS_PREFIX + cat.Id, true);
            RebuildCache();
        }

        public static void DisableAll()
        {
            foreach (var cat in Categories)
                EditorPrefs.SetBool(PREFS_PREFIX + cat.Id, false);
            RebuildCache();
        }

        public static int EnabledToolCount
        {
            get { EnsureInitialized(); return _enabledTools.Count; }
        }

        public static int TotalToolCount
        {
            get { return Categories.Sum(c => c.Tools.Length); }
        }

        // ─────────────────────────────────────────
        //  Presets
        // ─────────────────────────────────────────

        public static readonly Dictionary<string, string[]> Presets = new Dictionary<string, string[]>
        {
            { "Full", Categories.Select(c => c.Id).ToArray() },
            { "Core Only", new[] { "scene","gameobject","component","hierarchy","editor" } },
            { "3D Game", new[] { "scene","gameobject","component","hierarchy","editor","material","prefab","asset","import","physics","lighting","navmesh","lod","animation","audio","rendering","playersettings","vfx","editorutils" } },
            { "2D Game", new[] { "scene","gameobject","component","hierarchy","editor","material","prefab","asset","import","twod","animation","audio","uitoolkit","playersettings","editorutils" } },
            { "Multiplayer", new[] { "scene","gameobject","component","hierarchy","editor","material","prefab","asset","physics","lighting","navmesh","animation","audio","rendering","playersettings","netcode","editorutils" } },
        };

        public static void ApplyPreset(string presetName)
        {
            if (!Presets.TryGetValue(presetName, out var enabledCats)) return;
            var enabledSet = new HashSet<string>(enabledCats);
            foreach (var cat in Categories)
                EditorPrefs.SetBool(PREFS_PREFIX + cat.Id, enabledSet.Contains(cat.Id));
            RebuildCache();
        }

        // ─────────────────────────────────────────
        //  Internal
        // ─────────────────────────────────────────

        private static void EnsureInitialized()
        {
            if (!_initialized) RebuildCache();
        }

        private static void RebuildCache()
        {
            _enabledTools = new HashSet<string>();
            foreach (var cat in Categories)
            {
                if (IsCategoryEnabled(cat.Id))
                {
                    foreach (var tool in cat.Tools)
                        _enabledTools.Add(tool);
                }
            }
            _initialized = true;
        }

        private static bool GetDefaultForCategory(string categoryId)
        {
            var cat = Categories.FirstOrDefault(c => c.Id == categoryId);
            return cat?.DefaultEnabled ?? true;
        }
    }
}
#endif
