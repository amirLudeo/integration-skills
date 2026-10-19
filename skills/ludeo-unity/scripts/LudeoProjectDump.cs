using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LudeoIntegration.EditorTools
{
    /// <summary>
    /// Read-only dump of scenes and prefabs to text, so an agent can read a project whose assets are binary.
    /// Every GameObject (hierarchy, active, tag, layer, prefab source) and every component's serialized fields,
    /// one "path = value" line per field. Opens scenes and prefab contents, never saves them.
    /// Headless, with no Editor holding the project:
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt; -executeMethod LudeoIntegration.EditorTools.LudeoProjectDump.Run
    ///     -ludeoDumpOut &lt;abs dir&gt; [-ludeoDumpAllScenes] [-ludeoDumpPrefabs] -logFile &lt;abs log&gt;
    /// </summary>
    public static class LudeoProjectDump
    {
        private const int MAX_LINES_PER_COMPONENT = 300;
        private const int MAX_ARRAY_ELEMENTS = 50;

        public static void Run()
        {
            string outDir = Arg("-ludeoDumpOut") ?? "ludeo-integration-plan/project-dump";
            Directory.CreateDirectory(outDir);

            List<string> scenes = HasArg("-ludeoDumpAllScenes")
                ? AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).ToList()
                : EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();

            int sceneCount = 0;
            foreach (string path in scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var sb = new StringBuilder();
                sb.AppendLine($"# scene {path}");
                foreach (GameObject root in scene.GetRootGameObjects())
                    DumpGameObject(root, 0, sb);
                File.WriteAllText(Path.Combine(outDir, "scene__" + Safe(path) + ".txt"), sb.ToString());
                sceneCount++;
            }

            int prefabCount = 0;
            if (HasArg("-ludeoDumpPrefabs"))
            {
                foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
                {
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($"# prefab {path}");
                        DumpGameObject(root, 0, sb);
                        File.WriteAllText(Path.Combine(outDir, "prefab__" + Safe(path) + ".txt"), sb.ToString());
                        prefabCount++;
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }

            Debug.Log($"[LudeoDump] done scenes={sceneCount} prefabs={prefabCount} out={Path.GetFullPath(outDir)}");
        }

        private static void DumpGameObject(GameObject go, int depth, StringBuilder sb)
        {
            string pad = new string(' ', depth * 2);
            string prefab = PrefabUtility.IsAnyPrefabInstanceRoot(go)
                ? " prefab=" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)
                : string.Empty;
            sb.AppendLine($"{pad}GameObject \"{go.name}\" active={go.activeSelf} tag={go.tag} layer={LayerMask.LayerToName(go.layer)}{prefab}");

            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null)
                {
                    sb.AppendLine($"{pad}  [Missing script]");
                    continue;
                }
                sb.AppendLine($"{pad}  [{component.GetType().FullName}]");
                DumpFields(component, pad + "    ", sb);
            }

            foreach (Transform child in go.transform)
                DumpGameObject(child.gameObject, depth + 1, sb);
        }

        private static void DumpFields(UnityEngine.Object target, string pad, StringBuilder sb)
        {
            var so = new SerializedObject(target);
            SerializedProperty it = so.GetIterator();
            int lines = 0;
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = true;
                if (it.propertyPath == "m_Script")
                    continue;
                if (it.isArray && it.propertyType != SerializedPropertyType.String && it.arraySize > MAX_ARRAY_ELEMENTS)
                {
                    sb.AppendLine($"{pad}{it.propertyPath} = <array of {it.arraySize}, not expanded>");
                    enter = false;
                    continue;
                }
                if (it.hasVisibleChildren && it.propertyType == SerializedPropertyType.Generic)
                    continue; // its children follow, each on its own line
                if (++lines > MAX_LINES_PER_COMPONENT)
                {
                    sb.AppendLine($"{pad}<truncated after {MAX_LINES_PER_COMPONENT} fields>");
                    return;
                }
                sb.AppendLine($"{pad}{it.propertyPath} = {Value(it)}");
                enter = false; // a printed value (vector, color, string…) is not expanded further
            }
        }

        private static string Value(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: return p.longValue.ToString();
                case SerializedPropertyType.Boolean: return p.boolValue.ToString();
                case SerializedPropertyType.Float: return p.doubleValue.ToString("R");
                case SerializedPropertyType.String: return "\"" + p.stringValue + "\"";
                case SerializedPropertyType.Enum:
                    return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumDisplayNames.Length
                        ? p.enumDisplayNames[p.enumValueIndex]
                        : p.intValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    UnityEngine.Object o = p.objectReferenceValue;
                    if (o == null) return "null";
                    string assetPath = AssetDatabase.GetAssetPath(o);
                    return string.IsNullOrEmpty(assetPath) ? $"{o.GetType().Name} \"{o.name}\" (in scene)" : $"{o.GetType().Name} {assetPath}";
                case SerializedPropertyType.Vector2: return p.vector2Value.ToString("R");
                case SerializedPropertyType.Vector3: return p.vector3Value.ToString("R");
                case SerializedPropertyType.Vector4: return p.vector4Value.ToString("R");
                case SerializedPropertyType.Quaternion: return p.quaternionValue.eulerAngles.ToString("R") + " (euler)";
                case SerializedPropertyType.Color: return p.colorValue.ToString();
                case SerializedPropertyType.Rect: return p.rectValue.ToString();
                case SerializedPropertyType.Bounds: return p.boundsValue.ToString();
                case SerializedPropertyType.LayerMask: return p.intValue.ToString();
                case SerializedPropertyType.ArraySize: return p.intValue.ToString();
                default: return "<" + p.propertyType + ">";
            }
        }

        private static string Safe(string path) => path.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');

        private static bool HasArg(string name) => Environment.GetCommandLineArgs().Contains(name);

        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
