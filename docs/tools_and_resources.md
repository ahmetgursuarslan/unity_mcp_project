# Unity MCP Tools and Resources

The Unity MCP ecosystem contains two primary ways for the AI to interact with the Unity Editor: **Tools** (active commands) and **Resources** (passive data feeds).

## Resources (Live Context)
Resources allow the AI to silently read essential project context without explicitly calling a tool and waiting for a JSON response. The Router exposes the following URIs:

| Resource URI | Description | MimeType |
| --- | --- | --- |
| `unity://console/errors` | The last 50 compilation or runtime errors from the Unity Console. Automatically updates when the Editor fails to compile. | `text/plain` |
| `unity://scene/hierarchy` | An HTML-like string representation of the active Scene hierarchy, primarily focusing on `Canvas` and standard UI GameObjects. | `text/html` |
| `unity://project/info` | Essential version info, project name, and a list of all scenes configured in the Build Settings. | `application/json` |

---

## Tool Categories
There are currently over 145 discrete tools spanning 31 categories. All tools begin with the `unity_` prefix to prevent collisions with other MCP servers.

### 🤖 Auto-Fixers
*These tools enable agentic autonomy by allowing the AI to debug its own codebase.*
- `unity_dev_get_compile_errors`: Retrieves the active list of script compilation errors, including file paths and line numbers.
- `unity_dev_find_missing_references`: Scans the current scene or the entire project to find broken references (Missing Monobehaviours, Missing Prefab links).
- `unity_dev_find_asset_dependencies`: Useful before deleting or moving assets; shows all other assets that depend on a given file.

### 👁️ UI & Visual Context
*These tools allow the AI to "see" the unity layout.*
- `unity_ui_dump_hierarchy`: Outputs a clean, lightweight JSON/HTML tree representing the visual layout, rect transforms, and layout groups of the UI.
- `unity_shader_get_properties`: Dumps all exposed public properties (Colors, Floats, Textures) from an active Material or Shader Graph.

### 🏗️ Scene & Hierarchy Management
- `unity_scene_load`, `unity_scene_create`, `unity_scene_save`, `unity_scene_list`
- `unity_object_create`, `unity_object_delete`, `unity_object_find`
- `unity_hierarchy_list`, `unity_hierarchy_reparent`

### 🔧 Components & Materials
- `unity_component_add`, `unity_component_remove`, `unity_component_update`
- `unity_material_create`, `unity_material_assign`, `unity_material_set_color`, `unity_material_set_float`

### 📦 Prefabs & Assets
- `unity_prefab_create`, `unity_prefab_instantiate`, `unity_prefab_apply`, `unity_prefab_unpack`
- `unity_asset_import`, `unity_asset_move`, `unity_asset_delete`, `unity_asset_find`

### ⚙️ Editor Control & Build
- `unity_editor_play`, `unity_editor_stop`, `unity_editor_pause`, `unity_editor_refresh`, `unity_editor_compile`
- `unity_build_player`, `unity_build_settings_get`, `unity_build_settings_set`
- `unity_package_list`, `unity_package_add`, `unity_package_remove`

*(For a full list of tools across Physics, Navigation, Audio, UI Toolkit, Lighting, Rendering, Terrain, and ProBuilder, refer to the source mappings in `UnityToolsProvider.cs`.)*
