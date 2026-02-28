#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles animation and timeline tools:
    /// unity_animator_create_controller, unity_animator_add_state, unity_animator_add_transition,
    /// unity_animator_set_parameter, unity_animation_clip_create, unity_playable_graph_create,
    /// unity_playable_mixer_blend
    /// </summary>
    public static class AnimationHandler
    {
        [Serializable] private class CreateCtrlParams { public string name; public string savePath; }
        [Serializable] private class AddStateParams { public string controllerPath; public string stateName; public string clipPath; public int layerIndex; }
        [Serializable] private class TransitionParams { public string controllerPath; public string fromState; public string toState; public string conditionParam; public string conditionMode; public float conditionValue; public float duration; public int hasExitTime = 1; }
        [Serializable] private class SetParamParams { public int instanceId; public string paramName; public string paramType; public string value; }
        [Serializable] private class ClipParams { public string name; public string savePath; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_animator_create_controller": return HandleCreateController(paramsJson);
                    case "unity_animator_add_state": return HandleAddState(paramsJson);
                    case "unity_animator_add_transition": return HandleAddTransition(paramsJson);
                    case "unity_animator_set_parameter": return HandleSetParam(paramsJson);
                    case "unity_animation_clip_create": return HandleCreateClip(paramsJson);
                    case "unity_playable_graph_create": return "{\"info\":\"PlayableGraph is runtime-only. Use Animator Controller for editor setup.\"}";
                    case "unity_playable_mixer_blend": return "{\"info\":\"PlayableMixer is runtime-only. Use Animator blend trees for editor setup.\"}";
                    default: return $"{{\"error\":\"Unknown animation tool: {tool}\"}}";
                }
            });
        }

        private static string HandleCreateController(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateCtrlParams>(paramsJson);
            var path = p.savePath ?? $"Assets/Animations/{(p.name ?? "NewController")}.controller";
            SecurityGuard.ValidatePath(path);

            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.name = p.name ?? "NewController";
            AssetDatabase.SaveAssets();
            return $"{{\"created\":true,\"path\":\"{path}\"}}";
        }

        private static string HandleAddState(string paramsJson)
        {
            var p = JsonUtility.FromJson<AddStateParams>(paramsJson);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(p.controllerPath);
            if (controller == null) return $"{{\"error\":\"Controller not found at {p.controllerPath}\"}}";

            var layer = controller.layers[Math.Max(0, p.layerIndex)];
            var state = layer.stateMachine.AddState(p.stateName ?? "NewState");

            if (!string.IsNullOrEmpty(p.clipPath))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p.clipPath);
                if (clip != null) state.motion = clip;
            }

            AssetDatabase.SaveAssets();
            return $"{{\"added\":true,\"stateName\":\"{state.name}\"}}";
        }

        private static string HandleAddTransition(string paramsJson)
        {
            var p = JsonUtility.FromJson<TransitionParams>(paramsJson);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(p.controllerPath);
            if (controller == null) return $"{{\"error\":\"Controller not found at {p.controllerPath}\"}}";

            var sm = controller.layers[0].stateMachine;
            AnimatorState fromState = null, toState = null;
            foreach (var s in sm.states)
            {
                if (s.state.name == p.fromState) fromState = s.state;
                if (s.state.name == p.toState) toState = s.state;
            }
            if (fromState == null) return $"{{\"error\":\"State '{p.fromState}' not found\"}}";
            if (toState == null) return $"{{\"error\":\"State '{p.toState}' not found\"}}";

            var transition = fromState.AddTransition(toState);
            transition.hasExitTime = p.hasExitTime == 1;
            if (p.duration > 0) transition.duration = p.duration;

            if (!string.IsNullOrEmpty(p.conditionParam))
            {
                var mode = AnimatorConditionMode.If;
                if (!string.IsNullOrEmpty(p.conditionMode))
                    Enum.TryParse(p.conditionMode, true, out mode);
                transition.AddCondition(mode, p.conditionValue, p.conditionParam);
            }

            AssetDatabase.SaveAssets();
            return $"{{\"added\":true,\"from\":\"{p.fromState}\",\"to\":\"{p.toState}\"}}";
        }

        private static string HandleSetParam(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetParamParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var animator = go.GetComponent<Animator>();
            if (animator == null) return "{\"error\":\"No Animator on object\"}";

            switch ((p.paramType ?? "").ToLower())
            {
                case "bool": animator.SetBool(p.paramName, p.value == "true" || p.value == "1"); break;
                case "int": animator.SetInteger(p.paramName, int.Parse(p.value)); break;
                case "float": animator.SetFloat(p.paramName, float.Parse(p.value, System.Globalization.CultureInfo.InvariantCulture)); break;
                case "trigger": animator.SetTrigger(p.paramName); break;
                default: return "{\"error\":\"paramType must be bool, int, float, or trigger\"}";
            }
            return $"{{\"set\":true,\"param\":\"{p.paramName}\"}}";
        }

        private static string HandleCreateClip(string paramsJson)
        {
            var p = JsonUtility.FromJson<ClipParams>(paramsJson);
            var path = p.savePath ?? $"Assets/Animations/{(p.name ?? "NewClip")}.anim";
            SecurityGuard.ValidatePath(path);

            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var clip = new AnimationClip();
            clip.name = p.name ?? "NewClip";
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            return $"{{\"created\":true,\"path\":\"{path}\"}}";
        }
    }
}
#endif
