#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class GenerateInteractionsWindow : EditorWindow
{
    [MenuItem("Assets/generateinteractions")]
    public static void ShowWindow()
    {
        GetWindow<GenerateInteractionsWindow>(true, "generate interactions");
    }

    private enum Step
    {
        SelectObjects,
        DefineInteractionElements
    }

    private Step _currentStep = Step.SelectObjects;
    private Vector2 _scrollPos;
    private readonly Dictionary<GameObject, bool> _selection = new Dictionary<GameObject, bool>();
    private readonly Dictionary<GameObject, int> _interactionSelection = new Dictionary<GameObject, int>();
    private readonly string[] _interactionTypes = { "Button", "ToggleButton", "Slider", "Rotatable", "TouchArea", "Movable" };
    private readonly List<GameObject> _selectedObjects = new List<GameObject>();
    private string _groupName = string.Empty;
    private string _interactionDescription = string.Empty;

    private void OnGUI()
    {
        if (_currentStep == Step.SelectObjects)
        {
            DrawSelectionStep();
        }
        else if (_currentStep == Step.DefineInteractionElements)
        {
            DrawInteractionElementsStep();
        }
    }

    private void DrawSelectionStep()
    {
        EditorGUILayout.LabelField("GameObjects in Active Scene", EditorStyles.boldLabel);

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorGUILayout.LabelField("No active scene loaded.");
            return;
        }

        var allObjects = new List<GameObject>();
        foreach (var root in scene.GetRootGameObjects())
        {
            CollectChildren(root, allObjects);
        }

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Select", GUILayout.Width(50));
        EditorGUILayout.LabelField("GameObject");
        EditorGUILayout.LabelField("Active", GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        foreach (var go in allObjects)
        {
            EditorGUILayout.BeginHorizontal();

            bool isSelected = _selection.ContainsKey(go) && _selection[go];
            bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(50));
            if (newSelected != isSelected)
            {
                _selection[go] = newSelected;
            }

            EditorGUILayout.ObjectField(go, typeof(GameObject), true);
            EditorGUILayout.Toggle(go.activeInHierarchy, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        _groupName = EditorGUILayout.TextField("Group Name", _groupName);

        if (GUILayout.Button("Next"))
        {
            PrepareInteractionDefinition();
        }
    }

    private void DrawInteractionElementsStep()
    {
        EditorGUILayout.LabelField("Define Interaction Elements", EditorStyles.boldLabel);
        foreach (var go in _selectedObjects)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(go.name, GUILayout.Width(200));
            int current = _interactionSelection.ContainsKey(go) ? _interactionSelection[go] : 0;
            int selected = EditorGUILayout.Popup(current, _interactionTypes);
            _interactionSelection[go] = selected;
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Interaction Description", EditorStyles.boldLabel);
        _interactionDescription = EditorGUILayout.TextArea(_interactionDescription, GUILayout.MinHeight(60));

        if (GUILayout.Button("Create Interaction Objects"))
        {
            CreateInteractionObjects();
            _currentStep = Step.SelectObjects;
        }
    }

    private void PrepareInteractionDefinition()
    {
        _selectedObjects.Clear();
        foreach (var kv in _selection)
        {
            if (kv.Value)
            {
                _selectedObjects.Add(kv.Key);
            }
        }

        if (_selectedObjects.Count == 0 || string.IsNullOrEmpty(_groupName))
        {
            Debug.LogWarning("No objects selected or group name empty.");
            return;
        }

        _interactionSelection.Clear();
        foreach (var go in _selectedObjects)
        {
            _interactionSelection[go] = 0;
        }

        _currentStep = Step.DefineInteractionElements;
    }

    private void CreateInteractionObjects()
{
    string basePath = "Packages/vivian-example-prototypes/Resources";
    string groupPath = Path.Combine(basePath, _groupName);

    // 1) Nur den Group-Ordner anlegen (keine Unterordner)
    Directory.CreateDirectory(groupPath);
    string groupPathUnity = groupPath.Replace("\\", "/");

    // 2) Alte Prefabs in diesem Group-Ordner aufräumen (verhindert "zwei Prefabs" durch Altbestand)
    //    Hinweis: lässt "scene prefab.prefab" notfalls stehen – wir überschreiben es gleich ohnehin.
    string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { groupPathUnity });
    foreach (var guid in guids)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (!string.IsNullOrEmpty(path) && path.StartsWith(groupPathUnity))
        {
            AssetDatabase.DeleteAsset(path);
        }
    }

    // 3) Nur Top-Level-Objekte übernehmen (kein Parent+Child doppelt)
    var topLevel = GetTopLevelOnly(_selectedObjects);

    // 4) Sammel-Root für das Prefab bauen
    GameObject root = new GameObject("sceneprefab");

    // 5) Ausgewählte Top-Level-Objekte klonen und unter Root hängen (Namen beibehalten)
    foreach (var go in topLevel)
    {
        if (go == null) continue;
        var clone = Instantiate(go);
        clone.name = go.name;                 // "(Clone)" entfernen
        clone.transform.SetParent(root.transform, true); // true: Welttransform beibehalten
    }

    // 6) Ein einziges Sammel-Prefab speichern (ohne Unterordner)
    string prefabPath = Path.Combine(groupPath, "sceneprefab.prefab").Replace("\\", "/");

    var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    if (existing != null)
    {
        AssetDatabase.DeleteAsset(prefabPath);
    }

    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
    DestroyImmediate(root);

    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();

    // 7) Python-Generator starten (nutzt weiterhin _groupName, _interactionDescription, _selectedObjects)
    RunPythonGenerator();

    // 8) Originale aus der Szene löschen
    foreach (var go in topLevel)
    {
        if (go != null)
        {
            DestroyImmediate(go);
        }
    }

    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();

    Debug.Log($"Created single prefab: {prefabPath} and removed originals from scene.");
}




    private void RunPythonGenerator()
    {
        string scriptPath = Path.Combine("C:\\Users\\burklo\\Downloads\\specgen_no_cli\\unityconnector.py");
        if (!File.Exists(scriptPath))
        {
            Debug.LogError($"Script not found at {scriptPath}");
            return;
        }
        string python = "python";
        string escapedDesc = _interactionDescription.Replace("\"", "\\\"");
        // ⇩⇩⇩ HIER: group name als erstes CLI-Argument
        var args = new List<string> { $"\"{scriptPath}\"", $"\"{_groupName}\"", $"\"{escapedDesc}\"" };
        foreach (var go in _selectedObjects)
        {
            string type = _interactionTypes[_interactionSelection[go]];
            args.Add($"\"{go.name}\"");
            args.Add($"\"{type}\"");
        }
        var psi = new ProcessStartInfo
        {
            FileName = python,
            Arguments = string.Join(" ", args),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        try
        {
            using (var proc = Process.Start(psi))
            {
                proc.WaitForExit();
                string output = proc.StandardOutput.ReadToEnd();
                if (!string.IsNullOrEmpty(output))
                {
                    Debug.Log(output);
                }
                if (proc.ExitCode != 0)
                {
                    Debug.LogError(proc.StandardError.ReadToEnd());
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to run python script: {e.Message}");
        }
    }


    private void CollectChildren(GameObject go, List<GameObject> list)
    {
        list.Add(go);
        for (int i = 0; i < go.transform.childCount; i++)
        {
            CollectChildren(go.transform.GetChild(i).gameObject, list);
        }
    }
    // Liefert nur Top-Level-Objekte aus einer Auswahl (kein Objekt, dessen Vorfahre ebenfalls ausgewählt ist)
    private static List<GameObject> GetTopLevelOnly(List<GameObject> selected)
    {
        var result = new List<GameObject>();
        var selectedSet = new HashSet<Transform>();
        foreach (var go in selected)
        {
            if (go != null) selectedSet.Add(go.transform);
        }

        foreach (var go in selected)
        {
            if (go == null) continue;
            bool hasSelectedAncestor = false;
            var t = go.transform.parent;
            while (t != null)
            {
                if (selectedSet.Contains(t)) { hasSelectedAncestor = true; break; }
                t = t.parent;
            }
            if (!hasSelectedAncestor) result.Add(go);
        }
        return result;
    }

}
#endif

