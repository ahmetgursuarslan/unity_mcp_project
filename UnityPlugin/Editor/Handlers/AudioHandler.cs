#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles audio tools:
    /// unity_audio_source_setup, unity_audio_play, unity_audio_mixer_create,
    /// unity_audio_mixer_set_param, unity_audio_mixer_snapshot, unity_audio_listener_setup
    /// </summary>
    public static class AudioHandler
    {
        [Serializable] private class SourceParams { public int instanceId; public string clipPath; public float volume = 1; public float pitch = 1; public int loop; public int spatialBlend = -1; public float minDistance = -1; public float maxDistance = -1; }
        [Serializable] private class PlayParams { public int instanceId; public string action; }
        [Serializable] private class MixerCreateParams { public string name; public string savePath; }
        [Serializable] private class MixerSetParams { public string mixerPath; public string paramName; public float value; }
        [Serializable] private class SnapshotParams { public string mixerPath; public string snapshotName; public float transitionTime; }
        [Serializable] private class ListenerParams { public int instanceId; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_audio_source_setup": return HandleSourceSetup(paramsJson);
                    case "unity_audio_play": return HandlePlay(paramsJson);
                    case "unity_audio_mixer_create": return HandleMixerCreate(paramsJson);
                    case "unity_audio_mixer_set_param": return HandleMixerSet(paramsJson);
                    case "unity_audio_mixer_snapshot": return HandleSnapshot(paramsJson);
                    case "unity_audio_listener_setup": return HandleListener(paramsJson);
                    default: return $"{{\"error\":\"Unknown audio tool: {tool}\"}}";
                }
            });
        }

        private static string HandleSourceSetup(string paramsJson)
        {
            var p = JsonUtility.FromJson<SourceParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var source = go.GetComponent<AudioSource>();
            if (source == null) source = Undo.AddComponent<AudioSource>(go);

            Undo.RecordObject(source, "MCP AudioSource Setup");
            if (!string.IsNullOrEmpty(p.clipPath))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p.clipPath);
                if (clip != null) source.clip = clip;
            }
            source.volume = p.volume;
            source.pitch = p.pitch;
            source.loop = p.loop == 1;
            if (p.spatialBlend >= 0) source.spatialBlend = p.spatialBlend;
            if (p.minDistance > 0) source.minDistance = p.minDistance;
            if (p.maxDistance > 0) source.maxDistance = p.maxDistance;

            EditorUtility.SetDirty(source);
            return $"{{\"configured\":true,\"clip\":\"{source.clip?.name ?? "none"}\"}}";
        }

        private static string HandlePlay(string paramsJson)
        {
            var p = JsonUtility.FromJson<PlayParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var source = go.GetComponent<AudioSource>();
            if (source == null) return "{\"error\":\"No AudioSource on object\"}";

            switch ((p.action ?? "play").ToLower())
            {
                case "play": source.Play(); break;
                case "stop": source.Stop(); break;
                case "pause": source.Pause(); break;
            }
            return $"{{\"action\":\"{p.action}\"}}";
        }

        private static string HandleMixerCreate(string paramsJson)
        {
            var p = JsonUtility.FromJson<MixerCreateParams>(paramsJson);
            var path = p.savePath ?? $"Assets/Audio/{(p.name ?? "NewMixer")}.mixer";
            SecurityGuard.ValidatePath(path);

            // AudioMixer creation requires Unity internal creation
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            // Use menu item approach for mixer creation
            return "{\"info\":\"AudioMixer must be created via Assets > Create > Audio Mixer in editor. Use unity_execute_menu_item with 'Assets/Create/Audio Mixer'.\"}";
        }

        private static string HandleMixerSet(string paramsJson)
        {
            var p = JsonUtility.FromJson<MixerSetParams>(paramsJson);
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(p.mixerPath);
            if (mixer == null) return $"{{\"error\":\"AudioMixer not found at {p.mixerPath}\"}}";

            mixer.SetFloat(p.paramName, p.value);
            return $"{{\"set\":true,\"param\":\"{p.paramName}\",\"value\":{p.value}}}";
        }

        private static string HandleSnapshot(string paramsJson)
        {
            var p = JsonUtility.FromJson<SnapshotParams>(paramsJson);
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(p.mixerPath);
            if (mixer == null) return $"{{\"error\":\"AudioMixer not found at {p.mixerPath}\"}}";

            var snapshot = mixer.FindSnapshot(p.snapshotName);
            if (snapshot == null) return $"{{\"error\":\"Snapshot '{p.snapshotName}' not found\"}}";

            snapshot.TransitionTo(p.transitionTime > 0 ? p.transitionTime : 1f);
            return $"{{\"transitioned\":true,\"snapshot\":\"{p.snapshotName}\"}}";
        }

        private static string HandleListener(string paramsJson)
        {
            var p = JsonUtility.FromJson<ListenerParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var listener = go.GetComponent<AudioListener>();
            if (listener == null) listener = Undo.AddComponent<AudioListener>(go);

            return $"{{\"added\":true,\"instanceId\":{go.GetInstanceID()}}}";
        }
    }
}
#endif
