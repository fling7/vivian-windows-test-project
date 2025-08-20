#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
        string basePath = "Packages/vivian-example-prototypes/Resources/Interactionsobjects";
        string groupPath = Path.Combine(basePath, _groupName);
        string prefabFolder = Path.Combine(groupPath, "Prefabs");
        string materialsFolder = Path.Combine(groupPath, "Materials");
        string texturesFolder = Path.Combine(groupPath, "Textures");
        string specFolder = Path.Combine(groupPath, "FunctionalSpecification");

        Directory.CreateDirectory(prefabFolder);
        Directory.CreateDirectory(materialsFolder);
        Directory.CreateDirectory(texturesFolder);
        Directory.CreateDirectory(specFolder);
        string specFile = Path.Combine(specFolder, "InteractionElements.json");

        var json = new StringBuilder();
        json.AppendLine("{");
        json.AppendLine("    \"Elements\": [");

        for (int i = 0; i < _selectedObjects.Count; i++)
        {
            var go = _selectedObjects[i];
            string type = _interactionTypes[_interactionSelection[go]];
            json.Append(BuildElementJson(go.name, type));
            if (i < _selectedObjects.Count - 1)
            {
                json.AppendLine(",");
            }
            else
            {
                json.AppendLine();
            }
        }

        json.AppendLine("    ]");
        json.AppendLine("}");
        File.WriteAllText(specFile, json.ToString());

        GameObject root = new GameObject(_groupName);
        foreach (var go in _selectedObjects)
        {
            GameObject copy = Instantiate(go);
            copy.transform.SetParent(root.transform, true);
        }

        string prefabPath = Path.Combine(prefabFolder, _groupName + ".prefab");
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        DestroyImmediate(root);

        AssetDatabase.Refresh();
        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        PrefabUtility.InstantiatePrefab(prefabAsset);
    }

    private string BuildElementJson(string name, string type)
    {
        switch (type)
        {
            case "ToggleButton":
                return
$"        {{\n            \"Type\": \"ToggleButton\",\n            \"Name\": \"{name}\",\n            \"InitialAttributeValues\": [\n                {{ \"Attribute\": \"VALUE\", \"Value\": \"false\" }}\n            ]\n        }}";
            case "Slider":
                return
$"        {{\n            \"Type\": \"Slider\",\n            \"Name\": \"{name}\",\n            \"MinPosition\": {{ \"x\": 0.0, \"y\": 0.0, \"z\": 0.0 }},\n            \"MaxPosition\": {{ \"x\": 0.0, \"y\": 0.0, \"z\": 0.0 }},\n            \"InitialAttributeValues\": [\n                {{ \"Attribute\": \"VALUE\", \"Value\": \"0.0\" }},\n                {{ \"Attribute\": \"FIXED\", \"Value\": \"false\" }}\n            ],\n            \"PositionResolution\": 0,\n            \"TransitionTimeInMs\": 0\n        }}";
            case "Rotatable":
                return
$"        {{\n            \"Type\": \"Rotatable\",\n            \"Name\": \"{name}\",\n            \"MinRotation\": 0.0,\n            \"MaxRotation\": 0.0,\n            \"RotationAxis\": {{\n                \"Origin\": {{ \"x\": 0.0, \"y\": 0.0, \"z\": 0.0 }},\n                \"Direction\": {{ \"x\": 0.0, \"y\": 0.0, \"z\": 1.0 }}\n            }},\n            \"InitialAttributeValues\": [\n                {{ \"Attribute\": \"VALUE\", \"Value\": \"0.0\" }},\n                {{ \"Attribute\": \"FIXED\", \"Value\": \"false\" }}\n            ],\n            \"PositionResolution\": 0,\n            \"AllowsForInfiniteRotation\": false,\n            \"TransitionTimeInMs\": 0\n        }}";
            case "TouchArea":
                return
$"        {{\n            \"Type\": \"TouchArea\",\n            \"Name\": \"{name}\",\n            \"Plane\": {{ \"x\": 0.0, \"y\": 0.0, \"z\": 1.0 }},\n            \"Resolution\": {{ \"x\": 0.0, \"y\": 0.0 }}\n        }}";
            case "Movable":
                return
$"        {{\n            \"Type\": \"Movable\",\n            \"Name\": \"{name}\",\n            \"InitialAttributeValues\": [\n                {{ \"Attribute\": \"POSITION\", \"Value\": \"(0.0,0.0,0.0)\" }},\n                {{ \"Attribute\": \"ROTATION\", \"Value\": \"(0.0,0.0,0.0)\" }}\n            ],\n            \"SnapPoses\": [],\n            \"TransitionTimeInMs\": 0\n        }}";
            default:
                return
$"        {{\n            \"Type\": \"Button\",\n            \"Name\": \"{name}\"\n        }}";
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
}
#endif

