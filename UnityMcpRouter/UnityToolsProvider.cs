using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace UnityMcpRouter;

/// <summary>
/// Registers all Unity MCP tools and proxies calls through to the Unity WebSocket server.
/// Each tool method follows the pattern: validate params → forward to Unity → return result.
/// Total: ~130 tools across 24 categories.
/// </summary>
[McpServerToolType]
public class UnityToolsProvider
{
    private static async Task<string> ForwardToUnity(
        UnityWebSocketClient client, string toolName, object? parameters, CancellationToken ct)
    {
        var paramsJson = parameters != null
            ? JsonDocument.Parse(JsonSerializer.Serialize(parameters)).RootElement
            : (JsonElement?)null;
        var response = await client.SendCommandAsync(toolName, paramsJson, ct);
        if (response.TryGetProperty("isError", out var isErr) && isErr.GetBoolean())
        {
            var errorMsg = response.TryGetProperty("error", out var errProp)
                ? errProp.GetString() ?? "Unknown Unity error" : "Unknown Unity error";
            return $"[UNITY ERROR] {errorMsg}";
        }
        return response.TryGetProperty("result", out var result) ? result.GetRawText() : response.GetRawText();
    }

    // ═══════════════════════════════════════════════
    //  PHASE 1: SCENE MANAGEMENT
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_scene_load"), Description("Load a Unity scene by path")]
    public static async Task<string> SceneLoad(UnityWebSocketClient client, [Description("Scene path")] string scenePath, CancellationToken ct)
        => await ForwardToUnity(client, "unity_scene_load", new { scenePath }, ct);

    [McpServerTool(Name = "unity_scene_create"), Description("Create a new empty scene")]
    public static async Task<string> SceneCreate(UnityWebSocketClient client, [Description("Scene name")] string sceneName, [Description("Save path")] string? savePath = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_scene_create", new { sceneName, savePath }, ct);

    [McpServerTool(Name = "unity_scene_save"), Description("Save the active scene")]
    public static async Task<string> SceneSave(UnityWebSocketClient client, CancellationToken ct)
        => await ForwardToUnity(client, "unity_scene_save", null, ct);

    [McpServerTool(Name = "unity_scene_list"), Description("List all scenes in Build Settings")]
    public static async Task<string> SceneList(UnityWebSocketClient client, CancellationToken ct)
        => await ForwardToUnity(client, "unity_scene_list", null, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 1: GAMEOBJECT MANAGEMENT
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_object_create"), Description("Create a GameObject, optionally as a primitive")]
    public static async Task<string> ObjectCreate(UnityWebSocketClient client, [Description("Name")] string name, [Description("Primitive: Cube,Sphere,etc")] string? primitiveType = null, [Description("Parent instanceId")] int? parentId = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_object_create", new { name, primitiveType, parentId }, ct);

    [McpServerTool(Name = "unity_object_delete"), Description("Delete a GameObject by instanceId")]
    public static async Task<string> ObjectDelete(UnityWebSocketClient client, [Description("Instance ID")] int instanceId, CancellationToken ct)
        => await ForwardToUnity(client, "unity_object_delete", new { instanceId }, ct);

    [McpServerTool(Name = "unity_object_find"), Description("Find GameObjects by name, tag, or component type")]
    public static async Task<string> ObjectFind(UnityWebSocketClient client, [Description("Name")] string? name = null, [Description("Tag")] string? tag = null, [Description("Component type")] string? componentType = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_object_find", new { name, tag, componentType }, ct);

    [McpServerTool(Name = "unity_object_inspect"), Description("Inspect a GameObject's transform, components, properties")]
    public static async Task<string> ObjectInspect(UnityWebSocketClient client, [Description("Instance ID")] int instanceId, CancellationToken ct)
        => await ForwardToUnity(client, "unity_object_inspect", new { instanceId }, ct);

    [McpServerTool(Name = "unity_object_update"), Description("Update a GameObject's transform, name, tag, layer, active state")]
    public static async Task<string> ObjectUpdate(UnityWebSocketClient client, [Description("Instance ID")] int instanceId, string? name = null, string? tag = null, int? layer = null, bool? isActive = null, float[]? position = null, float[]? rotation = null, float[]? scale = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_object_update", new { instanceId, name, tag, layer, isActive, position, rotation, scale }, ct);

    [McpServerTool(Name = "unity_object_duplicate"), Description("Duplicate a GameObject")]
    public static async Task<string> ObjectDuplicate(UnityWebSocketClient client, [Description("Source instance ID")] int instanceId, [Description("New name")] string? newName = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_object_duplicate", new { instanceId, newName }, ct);

    [McpServerTool(Name = "unity_object_find_by_path"), Description("Find a GameObject by hierarchy path (e.g. 'Player/Camera/Main')")]
    public static async Task<string> ObjectFindByPath(UnityWebSocketClient client, [Description("Hierarchy path")] string path, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_object_find_by_path", new { path }, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 1: COMPONENT MANAGEMENT
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_component_add"), Description("Add a component to a GameObject")]
    public static async Task<string> ComponentAdd(UnityWebSocketClient client, [Description("Instance ID")] int instanceId, [Description("Component type")] string componentType, CancellationToken ct)
        => await ForwardToUnity(client, "unity_component_add", new { instanceId, componentType }, ct);

    [McpServerTool(Name = "unity_component_remove"), Description("Remove a component from a GameObject")]
    public static async Task<string> ComponentRemove(UnityWebSocketClient client, [Description("Instance ID")] int instanceId, [Description("Component type")] string componentType, CancellationToken ct)
        => await ForwardToUnity(client, "unity_component_remove", new { instanceId, componentType }, ct);

    [McpServerTool(Name = "unity_component_update"), Description("Update component fields via Reflection")]
    public static async Task<string> ComponentUpdate(UnityWebSocketClient client, [Description("Instance ID")] int instanceId, [Description("Component type")] string componentType, [Description("JSON fields")] string fieldsJson, CancellationToken ct)
        => await ForwardToUnity(client, "unity_component_update", new { instanceId, componentType, fields = JsonDocument.Parse(fieldsJson).RootElement }, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 1: HIERARCHY
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_hierarchy_list"), Description("Get scene hierarchy as JSON tree")]
    public static async Task<string> HierarchyList(UnityWebSocketClient client, CancellationToken ct)
        => await ForwardToUnity(client, "unity_hierarchy_list", null, ct);

    [McpServerTool(Name = "unity_hierarchy_reparent"), Description("Set parent of a GameObject")]
    public static async Task<string> HierarchyReparent(UnityWebSocketClient client, [Description("Child ID")] int childId, [Description("Parent ID (0=root)")] int? parentId = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_hierarchy_reparent", new { childId, parentId }, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 1: EDITOR CONTROL
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_play_control"), Description("Control play mode: play, pause, stop")]
    public static async Task<string> PlayControl(UnityWebSocketClient client, [Description("State: play/pause/stop")] string state, CancellationToken ct)
        => await ForwardToUnity(client, "unity_play_control", new { state }, ct);

    [McpServerTool(Name = "unity_refresh_assets"), Description("Refresh AssetDatabase")]
    public static async Task<string> RefreshAssets(UnityWebSocketClient client, CancellationToken ct)
        => await ForwardToUnity(client, "unity_refresh_assets", null, ct);

    [McpServerTool(Name = "unity_get_compilation_result"), Description("Get compilation errors/warnings")]
    public static async Task<string> GetCompilationResult(UnityWebSocketClient client, CancellationToken ct)
        => await ForwardToUnity(client, "unity_get_compilation_result", null, ct);

    [McpServerTool(Name = "unity_execute_menu_item"), Description("Execute an editor menu command")]
    public static async Task<string> ExecuteMenuItem(UnityWebSocketClient client, [Description("Menu path")] string menuPath, CancellationToken ct)
        => await ForwardToUnity(client, "unity_execute_menu_item", new { menuPath }, ct);

    [McpServerTool(Name = "unity_get_editor_state"), Description("Get editor state: play mode, scene, selection")]
    public static async Task<string> GetEditorState(UnityWebSocketClient client, CancellationToken ct)
        => await ForwardToUnity(client, "unity_get_editor_state", null, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 2: MATERIAL & SHADER
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_material_create"), Description("Create a new material with a shader")]
    public static async Task<string> MaterialCreate(UnityWebSocketClient client, [Description("Material name")] string name, [Description("Shader name")] string? shaderName = null, [Description("Save path")] string? savePath = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_material_create", new { name, shaderName, savePath }, ct);

    [McpServerTool(Name = "unity_material_assign"), Description("Assign a material to a GameObject's renderer")]
    public static async Task<string> MaterialAssign(UnityWebSocketClient client, [Description("GameObject instanceId")] int instanceId, [Description("Material asset path")] string materialPath, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_material_assign", new { instanceId, materialPath }, ct);

    [McpServerTool(Name = "unity_material_set_property"), Description("Set a material property (color, float, vector, keyword)")]
    public static async Task<string> MaterialSetProperty(UnityWebSocketClient client, [Description("Material path")] string materialPath, [Description("Property name")] string propertyName, [Description("Type: color/float/int/vector/keyword_enable/keyword_disable")] string propertyType, [Description("Value")] string value, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_material_set_property", new { materialPath, propertyName, propertyType, value }, ct);

    [McpServerTool(Name = "unity_material_get_properties"), Description("List all shader properties of a material")]
    public static async Task<string> MaterialGetProperties(UnityWebSocketClient client, [Description("Material path")] string materialPath, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_material_get_properties", new { materialPath }, ct);

    [McpServerTool(Name = "unity_material_set_texture"), Description("Set a texture on a material property")]
    public static async Task<string> MaterialSetTexture(UnityWebSocketClient client, [Description("Material path")] string materialPath, [Description("Property name")] string propertyName, [Description("Texture path")] string texturePath, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_material_set_texture", new { materialPath, propertyName, texturePath }, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 2: PREFAB
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_prefab_create"), Description("Create a prefab from a scene GameObject")]
    public static async Task<string> PrefabCreate(UnityWebSocketClient client, [Description("GameObject instanceId")] int instanceId, [Description("Save path")] string? savePath = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_prefab_create", new { instanceId, savePath }, ct);

    [McpServerTool(Name = "unity_prefab_instantiate"), Description("Instantiate a prefab into the scene")]
    public static async Task<string> PrefabInstantiate(UnityWebSocketClient client, [Description("Prefab asset path")] string prefabPath, float[]? position = null, float[]? rotation = null, int? parentId = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_prefab_instantiate", new { prefabPath, position, rotation, parentId }, ct);

    [McpServerTool(Name = "unity_prefab_apply_overrides"), Description("Apply prefab overrides")]
    public static async Task<string> PrefabApply(UnityWebSocketClient client, [Description("Instance ID")] int instanceId, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_prefab_apply_overrides", new { instanceId }, ct);

    [McpServerTool(Name = "unity_prefab_revert"), Description("Revert prefab instance to original")]
    public static async Task<string> PrefabRevert(UnityWebSocketClient client, [Description("Instance ID")] int instanceId, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_prefab_revert", new { instanceId }, ct);

    [McpServerTool(Name = "unity_prefab_unpack"), Description("Unpack a prefab instance")]
    public static async Task<string> PrefabUnpack(UnityWebSocketClient client, [Description("Instance ID")] int instanceId, [Description("Mode: root/completely")] string? mode = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_prefab_unpack", new { instanceId, mode }, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 2: ASSET MANAGEMENT
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_asset_import"), Description("Import an external file into Assets")]
    public static async Task<string> AssetImport(UnityWebSocketClient client, [Description("Source file path")] string sourcePath, [Description("Destination path")] string destinationPath, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_asset_import", new { sourcePath, destinationPath }, ct);

    [McpServerTool(Name = "unity_asset_move"), Description("Move/rename an asset")]
    public static async Task<string> AssetMove(UnityWebSocketClient client, [Description("Old path")] string oldPath, [Description("New path")] string newPath, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_asset_move", new { oldPath, newPath }, ct);

    [McpServerTool(Name = "unity_asset_delete"), Description("Delete an asset")]
    public static async Task<string> AssetDelete(UnityWebSocketClient client, [Description("Asset path")] string assetPath, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_asset_delete", new { assetPath }, ct);

    [McpServerTool(Name = "unity_asset_find"), Description("Find assets by filter and type")]
    public static async Task<string> AssetFind(UnityWebSocketClient client, [Description("Search filter")] string? filter = null, [Description("Type filter")] string? type = null, [Description("Search folder")] string? searchFolder = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_asset_find", new { filter, type, searchFolder }, ct);

    [McpServerTool(Name = "unity_asset_get_dependencies"), Description("Get all dependencies of an asset")]
    public static async Task<string> AssetGetDeps(UnityWebSocketClient client, [Description("Asset path")] string assetPath, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_asset_get_dependencies", new { assetPath }, ct);

    [McpServerTool(Name = "unity_asset_set_labels"), Description("Set labels on an asset")]
    public static async Task<string> AssetSetLabels(UnityWebSocketClient client, [Description("Asset path")] string assetPath, [Description("Labels array")] string[] labels, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_asset_set_labels", new { assetPath, labels }, ct);

    [McpServerTool(Name = "unity_scriptable_object_create"), Description("Create a ScriptableObject asset")]
    public static async Task<string> SOCreate(UnityWebSocketClient client, [Description("SO type name")] string typeName, [Description("Save path")] string savePath, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_scriptable_object_create", new { typeName, savePath }, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 2: IMPORT SETTINGS
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_texture_import_settings"), Description("Configure texture import: size, compression, mipmaps")]
    public static async Task<string> TexImport(UnityWebSocketClient client, [Description("Texture path")] string assetPath, int? maxSize = null, string? compression = null, string? filterMode = null, int? generateMipMaps = null, string? textureType = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_texture_import_settings", new { assetPath, maxSize, compression, filterMode, generateMipMaps, textureType }, ct);

    [McpServerTool(Name = "unity_model_import_settings"), Description("Configure model import: scale, normals, animation")]
    public static async Task<string> ModelImport(UnityWebSocketClient client, [Description("Model path")] string assetPath, float? scaleFactor = null, int? importNormals = null, int? importAnimation = null, string? animationType = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_model_import_settings", new { assetPath, scaleFactor, importNormals, importAnimation, animationType }, ct);

    [McpServerTool(Name = "unity_audio_import_settings"), Description("Configure audio import: load type, compression, quality")]
    public static async Task<string> AudioImport(UnityWebSocketClient client, [Description("Audio path")] string assetPath, string? loadType = null, string? compressionFormat = null, float? quality = null, int? forceToMono = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_audio_import_settings", new { assetPath, loadType, compressionFormat, quality, forceToMono }, ct);

    [McpServerTool(Name = "unity_asset_postprocessor_add"), Description("Add custom asset post-processor rule")]
    public static async Task<string> PostProcessor(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_asset_postprocessor_add", null, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 3: PHYSICS
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_physics_raycast"), Description("Cast a ray and return hit info")]
    public static async Task<string> Raycast(UnityWebSocketClient client, [Description("Origin [x,y,z]")] float[] origin, [Description("Direction [x,y,z]")] float[] direction, float? maxDistance = null, int? layerMask = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_physics_raycast", new { origin, direction, maxDistance, layerMask }, ct);

    [McpServerTool(Name = "unity_physics_overlap"), Description("Find colliders in a sphere")]
    public static async Task<string> Overlap(UnityWebSocketClient client, [Description("Center [x,y,z]")] float[] center, [Description("Radius")] float radius, int? layerMask = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_physics_overlap", new { center, radius, layerMask }, ct);

    [McpServerTool(Name = "unity_physics_settings"), Description("Configure physics: gravity, solver iterations")]
    public static async Task<string> PhysicsSettings(UnityWebSocketClient client, float[]? gravity = null, int? solverIterations = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_physics_settings", new { gravity, solverIterations }, ct);

    [McpServerTool(Name = "unity_physics_set_collision_matrix"), Description("Set layer collision rules")]
    public static async Task<string> CollisionMatrix(UnityWebSocketClient client, int layer1, int layer2, int? collide = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_physics_set_collision_matrix", new { layer1, layer2, collide }, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 3: LIGHTING
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_light_create"), Description("Create a light (Directional, Point, Spot, Area)")]
    public static async Task<string> LightCreate(UnityWebSocketClient client, [Description("Type")] string? type = null, float[]? color = null, float? intensity = null, string? name = null, float[]? position = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_light_create", new { type, color, intensity, name, position }, ct);

    [McpServerTool(Name = "unity_light_bake"), Description("Start lightmap baking")]
    public static async Task<string> LightBake(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_light_bake", null, ct);

    [McpServerTool(Name = "unity_reflection_probe_add"), Description("Add a reflection probe")]
    public static async Task<string> ReflProbe(UnityWebSocketClient client, float[]? position = null, float[]? size = null, string? mode = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_reflection_probe_add", new { position, size, mode }, ct);

    [McpServerTool(Name = "unity_light_probe_group"), Description("Add a light probe group")]
    public static async Task<string> LightProbe(UnityWebSocketClient client, float[]? position = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_light_probe_group", new { position }, ct);

    [McpServerTool(Name = "unity_environment_settings"), Description("Set skybox, ambient light, fog")]
    public static async Task<string> EnvSettings(UnityWebSocketClient client, string? skyboxPath = null, float[]? ambientColor = null, string? ambientMode = null, int? fog = null, float[]? fogColor = null, float? fogDensity = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_environment_settings", new { skyboxPath, ambientColor, ambientMode, fog, fogColor, fogDensity }, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 3: NAVMESH
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_navmesh_bake"), Description("Bake the NavMesh")]
    public static async Task<string> NavBake(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_navmesh_bake", null, ct);

    [McpServerTool(Name = "unity_navmesh_agent_setup"), Description("Add/configure NavMeshAgent")]
    public static async Task<string> NavAgent(UnityWebSocketClient client, int instanceId, float? speed = null, float? radius = null, float? height = null, float? stoppingDistance = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_navmesh_agent_setup", new { instanceId, speed, radius, height, stoppingDistance }, ct);

    [McpServerTool(Name = "unity_navmesh_obstacle_add"), Description("Add NavMeshObstacle")]
    public static async Task<string> NavObstacle(UnityWebSocketClient client, int instanceId, int? carve = null, float[]? size = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_navmesh_obstacle_add", new { instanceId, carve, size }, ct);

    [McpServerTool(Name = "unity_navmesh_set_area"), Description("List/set NavMesh area costs")]
    public static async Task<string> NavArea(UnityWebSocketClient client, int? areaIndex = null, float? cost = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_navmesh_set_area", new { areaIndex, cost }, ct);

    [McpServerTool(Name = "unity_navmesh_find_path"), Description("Calculate path between two points")]
    public static async Task<string> NavPath(UnityWebSocketClient client, float[] start, float[] end, int? areaMask = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_navmesh_find_path", new { start, end, areaMask }, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 3: LOD & PERFORMANCE
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_lod_group_setup"), Description("Configure LOD Group")]
    public static async Task<string> LODSetup(UnityWebSocketClient client, int instanceId, float[]? thresholds = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_lod_group_setup", new { instanceId, thresholds }, ct);

    [McpServerTool(Name = "unity_occlusion_bake"), Description("Bake occlusion culling")]
    public static async Task<string> OcclusionBake(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_occlusion_bake", null, ct);

    [McpServerTool(Name = "unity_static_flags_set"), Description("Set static editor flags on a GameObject")]
    public static async Task<string> StaticFlags(UnityWebSocketClient client, int instanceId, [Description("Comma-separated flags")] string? flags = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_static_flags_set", new { instanceId, flags }, ct);

    [McpServerTool(Name = "unity_gpu_instancing_enable"), Description("Toggle GPU instancing on a material")]
    public static async Task<string> GPUInstancing(UnityWebSocketClient client, string materialPath, int? enable = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_gpu_instancing_enable", new { materialPath, enable }, ct);

    [McpServerTool(Name = "unity_profiler_capture"), Description("Capture profiler memory stats")]
    public static async Task<string> ProfilerCapture(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_profiler_capture", null, ct);

    [McpServerTool(Name = "unity_memory_snapshot"), Description("Get memory allocation snapshot")]
    public static async Task<string> MemSnapshot(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_memory_snapshot", null, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 4: ANIMATION
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_animator_create_controller"), Description("Create an AnimatorController asset")]
    public static async Task<string> AnimCtrlCreate(UnityWebSocketClient client, string? name = null, string? savePath = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_animator_create_controller", new { name, savePath }, ct);

    [McpServerTool(Name = "unity_animator_add_state"), Description("Add a state to an AnimatorController")]
    public static async Task<string> AnimAddState(UnityWebSocketClient client, string controllerPath, string stateName, string? clipPath = null, int? layerIndex = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_animator_add_state", new { controllerPath, stateName, clipPath, layerIndex }, ct);

    [McpServerTool(Name = "unity_animator_add_transition"), Description("Add a transition between animator states")]
    public static async Task<string> AnimTransition(UnityWebSocketClient client, string controllerPath, string fromState, string toState, string? conditionParam = null, string? conditionMode = null, float? conditionValue = null, float? duration = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_animator_add_transition", new { controllerPath, fromState, toState, conditionParam, conditionMode, conditionValue, duration }, ct);

    [McpServerTool(Name = "unity_animator_set_parameter"), Description("Set an Animator parameter")]
    public static async Task<string> AnimSetParam(UnityWebSocketClient client, int instanceId, string paramName, string paramType, string value, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_animator_set_parameter", new { instanceId, paramName, paramType, value }, ct);

    [McpServerTool(Name = "unity_animation_clip_create"), Description("Create an AnimationClip asset")]
    public static async Task<string> ClipCreate(UnityWebSocketClient client, string? name = null, string? savePath = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_animation_clip_create", new { name, savePath }, ct);

    [McpServerTool(Name = "unity_playable_graph_create"), Description("Info about PlayableGraph (runtime-only)")]
    public static async Task<string> PlayableGraph(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_playable_graph_create", null, ct);

    [McpServerTool(Name = "unity_playable_mixer_blend"), Description("Info about PlayableMixer (runtime-only)")]
    public static async Task<string> PlayableMixer(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_playable_mixer_blend", null, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 4: AUDIO
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_audio_source_setup"), Description("Add/configure AudioSource")]
    public static async Task<string> AudioSource(UnityWebSocketClient client, int instanceId, string? clipPath = null, float? volume = null, float? pitch = null, int? loop = null, int? spatialBlend = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_audio_source_setup", new { instanceId, clipPath, volume, pitch, loop, spatialBlend }, ct);

    [McpServerTool(Name = "unity_audio_play"), Description("Play/stop/pause AudioSource")]
    public static async Task<string> AudioPlay(UnityWebSocketClient client, int instanceId, [Description("Action: play/stop/pause")] string? action = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_audio_play", new { instanceId, action }, ct);

    [McpServerTool(Name = "unity_audio_mixer_create"), Description("Create an AudioMixer")]
    public static async Task<string> MixerCreate(UnityWebSocketClient client, string? name = null, string? savePath = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_audio_mixer_create", new { name, savePath }, ct);

    [McpServerTool(Name = "unity_audio_mixer_set_param"), Description("Set an AudioMixer parameter")]
    public static async Task<string> MixerSet(UnityWebSocketClient client, string mixerPath, string paramName, float value, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_audio_mixer_set_param", new { mixerPath, paramName, value }, ct);

    [McpServerTool(Name = "unity_audio_mixer_snapshot"), Description("Transition to an AudioMixer snapshot")]
    public static async Task<string> MixerSnapshot(UnityWebSocketClient client, string mixerPath, string snapshotName, float? transitionTime = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_audio_mixer_snapshot", new { mixerPath, snapshotName, transitionTime }, ct);

    [McpServerTool(Name = "unity_audio_listener_setup"), Description("Add AudioListener to a GameObject")]
    public static async Task<string> AudioListener(UnityWebSocketClient client, int instanceId, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_audio_listener_setup", new { instanceId }, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 5: UI TOOLKIT
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_ui_query"), Description("Query UI elements")]
    public static async Task<string> UIQuery(UnityWebSocketClient client, string selector, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_ui_query", new { selector }, ct);

    [McpServerTool(Name = "unity_ui_generate_uxml"), Description("Generate a UXML file")]
    public static async Task<string> UIGenUxml(UnityWebSocketClient client, string savePath, string? content = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_ui_generate_uxml", new { savePath, content }, ct);

    [McpServerTool(Name = "unity_ui_generate_uss"), Description("Generate a USS stylesheet")]
    public static async Task<string> UIGenUss(UnityWebSocketClient client, string savePath, string? content = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_ui_generate_uss", new { savePath, content }, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 5: RENDERING & CAMERA
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_camera_setup"), Description("Create/configure a camera")]
    public static async Task<string> CameraSetup(UnityWebSocketClient client, int? instanceId = null, float? fov = null, float? nearClip = null, float? farClip = null, string? clearFlags = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_camera_setup", new { instanceId, fov, nearClip, farClip, clearFlags }, ct);

    [McpServerTool(Name = "unity_post_processing_add"), Description("Add URP Volume for post-processing")]
    public static async Task<string> PostProc(UnityWebSocketClient client, int? instanceId = null, string? profilePath = null, int? isGlobal = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_post_processing_add", new { instanceId, profilePath, isGlobal }, ct);

    [McpServerTool(Name = "unity_render_settings"), Description("Get/set quality level and render settings")]
    public static async Task<string> RenderSettings(UnityWebSocketClient client, string? qualityLevel = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_render_settings", new { qualityLevel }, ct);

    [McpServerTool(Name = "unity_screenshot_capture"), Description("Capture a screenshot")]
    public static async Task<string> Screenshot(UnityWebSocketClient client, string? savePath = null, int? superSize = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_screenshot_capture", new { savePath, superSize }, ct);

    [McpServerTool(Name = "unity_cinemachine_vcam_create"), Description("Create a Cinemachine virtual camera")]
    public static async Task<string> CinemachineCreate(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_cinemachine_vcam_create", null, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 5: PLAYER & GLOBAL SETTINGS
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_player_settings"), Description("Set company/product name, bundle ID")]
    public static async Task<string> PlayerSettings(UnityWebSocketClient client, string? companyName = null, string? productName = null, string? bundleId = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_player_settings", new { companyName, productName, bundleId }, ct);

    [McpServerTool(Name = "unity_player_resolution"), Description("Set default resolution and fullscreen mode")]
    public static async Task<string> PlayerRes(UnityWebSocketClient client, int? width = null, int? height = null, int? fullscreen = null, int? vSync = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_player_resolution", new { width, height, fullscreen, vSync }, ct);

    [McpServerTool(Name = "unity_time_settings"), Description("Set fixedTimestep, maxTimestep, timeScale")]
    public static async Task<string> TimeSettings(UnityWebSocketClient client, float? fixedTimestep = null, float? maxTimestep = null, float? timeScale = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_time_settings", new { fixedTimestep, maxTimestep, timeScale }, ct);

    [McpServerTool(Name = "unity_color_space"), Description("Set Linear or Gamma color space")]
    public static async Task<string> ColorSpace(UnityWebSocketClient client, string? colorSpace = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_color_space", new { colorSpace }, ct);

    [McpServerTool(Name = "unity_graphics_api"), Description("Get current graphics APIs")]
    public static async Task<string> GraphicsApi(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_graphics_api", null, ct);

    [McpServerTool(Name = "unity_scripting_backend"), Description("Set Mono or IL2CPP scripting backend")]
    public static async Task<string> ScriptBackend(UnityWebSocketClient client, string? backend = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_scripting_backend", new { backend }, ct);

    [McpServerTool(Name = "unity_script_execution_order"), Description("Set MonoBehaviour execution order")]
    public static async Task<string> ExecOrder(UnityWebSocketClient client, string scriptName, int order, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_script_execution_order", new { scriptName, order }, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 6: 2D SYSTEMS
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_sprite_create"), Description("Configure texture as sprite")]
    public static async Task<string> SpriteCreate(UnityWebSocketClient client, string texturePath, int? pixelsPerUnit = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_sprite_create", new { texturePath, pixelsPerUnit }, ct);

    [McpServerTool(Name = "unity_tilemap_create"), Description("Create a Tilemap with Grid")]
    public static async Task<string> TilemapCreate(UnityWebSocketClient client, string? name = null, string? gridType = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_tilemap_create", new { name, gridType }, ct);

    [McpServerTool(Name = "unity_tilemap_set_tile"), Description("Place a tile at coordinates")]
    public static async Task<string> TileSet(UnityWebSocketClient client, int tilemapId, int x, int y, string tilePath, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_tilemap_set_tile", new { tilemapId, x, y, tilePath }, ct);

    [McpServerTool(Name = "unity_tilemap_paint_area"), Description("Paint an area with tiles")]
    public static async Task<string> TilePaint(UnityWebSocketClient client, int tilemapId, int x1, int y1, int x2, int y2, string tilePath, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_tilemap_paint_area", new { tilemapId, x1, y1, x2, y2, tilePath }, ct);

    [McpServerTool(Name = "unity_tilemap_clear"), Description("Clear all tiles")]
    public static async Task<string> TileClear(UnityWebSocketClient client, int tilemapId, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_tilemap_clear", new { tilemapId }, ct);

    [McpServerTool(Name = "unity_2d_physics_setup"), Description("Add Rigidbody2D and 2D colliders")]
    public static async Task<string> Physics2D(UnityWebSocketClient client, int instanceId, string? bodyType = null, float? mass = null, float? gravityScale = null, string? colliderType = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_2d_physics_setup", new { instanceId, bodyType, mass, gravityScale, colliderType }, ct);

    [McpServerTool(Name = "unity_sorting_layer_manage"), Description("List sorting layers")]
    public static async Task<string> SortLayers(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_sorting_layer_manage", null, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 6: SPLINE & TERRAIN
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_spline_create"), Description("Create a SplineContainer")]
    public static async Task<string> SplineCreate(UnityWebSocketClient client, string? name = null, float[]? position = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_spline_create", new { name, position }, ct);

    [McpServerTool(Name = "unity_spline_extrude_mesh"), Description("Add SplineExtrude component")]
    public static async Task<string> SplineExtrude(UnityWebSocketClient client, int instanceId, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_spline_extrude_mesh", new { instanceId }, ct);

    [McpServerTool(Name = "unity_spline_animate"), Description("Add SplineAnimate component")]
    public static async Task<string> SplineAnimate(UnityWebSocketClient client, int targetId, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_spline_animate", new { targetId }, ct);

    [McpServerTool(Name = "unity_terrain_create"), Description("Create a terrain")]
    public static async Task<string> TerrainCreate(UnityWebSocketClient client, string? name = null, float? width = null, float? height = null, float? length = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_terrain_create", new { name, width, height, length }, ct);

    [McpServerTool(Name = "unity_terrain_place_trees"), Description("Place trees on terrain")]
    public static async Task<string> TerrainTrees(UnityWebSocketClient client, int instanceId, string prefabPath, float[] position, float? widthScale = null, float? heightScale = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_terrain_place_trees", new { instanceId, prefabPath, position, widthScale, heightScale }, ct);

    [McpServerTool(Name = "unity_terrain_set_settings"), Description("Configure terrain render settings")]
    public static async Task<string> TerrainSettings(UnityWebSocketClient client, int instanceId, float? detailDistance = null, float? treeDistance = null, int? pixelError = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_terrain_set_settings", new { instanceId, detailDistance, treeDistance, pixelError }, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 7: VFX + PROBUILDER + EDITOR UTILS
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_particle_create"), Description("Create a ParticleSystem with optional preset (fire, smoke, sparks)")]
    public static async Task<string> ParticleCreate(UnityWebSocketClient client, string? name = null, string? preset = null, float[]? position = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_particle_create", new { name, preset, position }, ct);

    [McpServerTool(Name = "unity_particle_play_stop"), Description("Play/stop/pause a ParticleSystem")]
    public static async Task<string> ParticlePlay(UnityWebSocketClient client, int instanceId, string? action = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_particle_play_stop", new { instanceId, action }, ct);

    [McpServerTool(Name = "unity_vfx_graph_create"), Description("Create a VFX Graph object")]
    public static async Task<string> VFXCreate(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_vfx_graph_create", null, ct);

    [McpServerTool(Name = "unity_probuilder_create_shape"), Description("Create a ProBuilder shape")]
    public static async Task<string> PBCreate(UnityWebSocketClient client, string? shape = null, string? name = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_probuilder_create_shape", new { shape, name }, ct);

    [McpServerTool(Name = "unity_probuilder_export_mesh"), Description("Export ProBuilder mesh as asset")]
    public static async Task<string> PBExport(UnityWebSocketClient client, int instanceId, string? savePath = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_probuilder_export_mesh", new { instanceId, savePath }, ct);

    [McpServerTool(Name = "unity_console_clear"), Description("Clear the Unity console")]
    public static async Task<string> ConsoleClear(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_console_clear", null, ct);

    [McpServerTool(Name = "unity_selection_set"), Description("Set editor selection")]
    public static async Task<string> SelectionSet(UnityWebSocketClient client, int[] instanceIds, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_selection_set", new { instanceIds }, ct);

    [McpServerTool(Name = "unity_scene_view_focus"), Description("Focus Scene View on a GameObject")]
    public static async Task<string> SVFocus(UnityWebSocketClient client, int instanceId, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_scene_view_focus", new { instanceId }, ct);

    [McpServerTool(Name = "unity_scene_view_set_camera"), Description("Set Scene View camera position/rotation")]
    public static async Task<string> SVCamera(UnityWebSocketClient client, float[]? position = null, float[]? rotation = null, float? size = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_scene_view_set_camera", new { position, rotation, size }, ct);

    [McpServerTool(Name = "unity_undo_perform"), Description("Perform undo or redo")]
    public static async Task<string> UndoPerform(UnityWebSocketClient client, [Description("Action: undo/redo")] string? action = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_undo_perform", new { action }, ct);

    [McpServerTool(Name = "unity_editor_prefs"), Description("Get/set EditorPrefs")]
    public static async Task<string> EditorPrefs(UnityWebSocketClient client, string key, string? value = null, string? type = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_editor_prefs", new { key, value, type }, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 8: NETCODE + ECS + SENTIS + ADDRESSABLES
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_netcode_setup"), Description("Setup NetworkManager for Netcode")]
    public static async Task<string> NetSetup(UnityWebSocketClient client, int? tickRate = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_netcode_setup", new { tickRate }, ct);

    [McpServerTool(Name = "unity_network_object_create"), Description("Add NetworkObject component")]
    public static async Task<string> NetObj(UnityWebSocketClient client, int instanceId, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_network_object_create", new { instanceId }, ct);

    [McpServerTool(Name = "unity_ecs_subscene_create"), Description("Create an ECS SubScene")]
    public static async Task<string> ECSSubScene(UnityWebSocketClient client, string? name = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_ecs_subscene_create", new { name }, ct);

    [McpServerTool(Name = "unity_sentis_load_model"), Description("Load a Sentis ML model")]
    public static async Task<string> SentisLoad(UnityWebSocketClient client, string modelPath, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_sentis_load_model", new { modelPath }, ct);

    [McpServerTool(Name = "unity_addressable_mark"), Description("Mark asset as Addressable")]
    public static async Task<string> AddrMark(UnityWebSocketClient client, string assetPath, string? address = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_addressable_mark", new { assetPath, address }, ct);

    [McpServerTool(Name = "unity_addressable_build"), Description("Build Addressables content")]
    public static async Task<string> AddrBuild(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_addressable_build", null, ct);

    // ═══════════════════════════════════════════════
    //  SCRIPT MANAGEMENT
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_create_script"), Description("Create a C# script with template (MonoBehaviour, ScriptableObject, Editor, Interface, Static, Enum)")]
    public static async Task<string> CreateScript(UnityWebSocketClient client, [Description("Class name")] string scriptName, [Description("Type: MonoBehaviour/ScriptableObject/Editor/Interface/Static/Enum")] string? scriptType = null, [Description("Namespace")] string? namespaceName = null, [Description("Save path")] string? savePath = null, [Description("Full script content (overrides template)")] string? content = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_create_script", new { scriptName, scriptType, namespaceName, savePath, content }, ct);

    [McpServerTool(Name = "unity_read_script"), Description("Read a C# script file contents")]
    public static async Task<string> ReadScript(UnityWebSocketClient client, [Description("Script file path")] string scriptPath, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_read_script", new { scriptPath }, ct);

    [McpServerTool(Name = "unity_edit_script"), Description("Overwrite a C# script with new content")]
    public static async Task<string> EditScript(UnityWebSocketClient client, [Description("Script path")] string savePath, [Description("New file content")] string content, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_edit_script", new { savePath, content }, ct);

    // ═══════════════════════════════════════════════
    //  BUILD PIPELINE
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_build_player"), Description("Build the player for a target platform (windows, mac, linux, android, ios, webgl)")]
    public static async Task<string> BuildPlayer(UnityWebSocketClient client, [Description("Target: windows/mac/linux/android/ios/webgl")] string? target = null, [Description("Output path")] string? path = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_build_player", new { target, path }, ct);

    [McpServerTool(Name = "unity_build_settings"), Description("Get/set build settings (development mode)")]
    public static async Task<string> BuildSettings(UnityWebSocketClient client, [Description("Set development mode: true/false")] string? development = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_build_settings", new { development }, ct);

    [McpServerTool(Name = "unity_build_scene_list"), Description("List scenes in Build Settings")]
    public static async Task<string> BuildSceneList(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_build_scene_list", null, ct);

    // ═══════════════════════════════════════════════
    //  PACKAGE MANAGER
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_package_list"), Description("List all installed UPM packages")]
    public static async Task<string> PackageList(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_package_list", null, ct);

    [McpServerTool(Name = "unity_package_add"), Description("Install a UPM package by ID (e.g. com.unity.cinemachine)")]
    public static async Task<string> PackageAdd(UnityWebSocketClient client, [Description("Package ID")] string packageId, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_package_add", new { packageId }, ct);

    [McpServerTool(Name = "unity_package_remove"), Description("Remove an installed UPM package")]
    public static async Task<string> PackageRemove(UnityWebSocketClient client, [Description("Package ID")] string packageId, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_package_remove", new { packageId }, ct);

    [McpServerTool(Name = "unity_package_search"), Description("Search UPM registry for packages")]
    public static async Task<string> PackageSearch(UnityWebSocketClient client, [Description("Search query")] string query, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_package_search", new { query }, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 2: AI AUTONOMY (DEV TOOLS)
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_dev_get_compile_errors"), Description("Get current compilation errors from Unity Console with exact file/line numbers")]
    public static async Task<string> DevGetCompileErrors(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_dev_get_compile_errors", null, ct);

    [McpServerTool(Name = "unity_dev_find_missing_references"), Description("Scan the active scene for missing scripts (null components) or empty object references")]
    public static async Task<string> DevFindMissingReferences(UnityWebSocketClient client, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_dev_find_missing_references", null, ct);

    [McpServerTool(Name = "unity_dev_find_asset_dependencies"), Description("Find what depends on an asset, and everywhere that asset is used in the project")]
    public static async Task<string> DevFindAssetDependencies(UnityWebSocketClient client, [Description("Asset path (e.g. Assets/Scripts/Player.cs)")] string assetPath, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_dev_find_asset_dependencies", new { assetPath }, ct);

    // ═══════════════════════════════════════════════
    //  PHASE 2: UI EXPORT & VISUAL SYSTEMS
    // ═══════════════════════════════════════════════
    [McpServerTool(Name = "unity_ui_dump_hierarchy"), Description("Export a Canvas or UI Toolkit tree into a semantic HTML-like string for AI layout reasoning")]
    public static async Task<string> UIDumpHierarchy(UnityWebSocketClient client, [Description("Canvas/Panel root instanceId (0 for all)")] int rootInstanceId = 0, [Description("Max depth to scan")] int depth = 5, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_ui_dump_hierarchy", new { rootInstanceId, depth }, ct);

    [McpServerTool(Name = "unity_shader_get_properties"), Description("Analyze a Material or Shader to get all public colors, floats, and textures")]
    public static async Task<string> ShaderGetProperties(UnityWebSocketClient client, [Description("Material instanceId (0 if using path)")] int materialInstanceId = 0, [Description("Asset path (empty if using ID)")] string? materialPath = null, CancellationToken ct = default)
        => await ForwardToUnity(client, "unity_shader_get_properties", new { materialInstanceId, materialPath }, ct);
}
