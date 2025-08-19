#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;

public class GenerateInteractionsWindow : EditorWindow
{
    [MenuItem("Assets/generateinteractions")]
    public static void ShowWindow()
    {
        GetWindow<GenerateInteractionsWindow>(true, "generate interactions");
    }

    private Vector2 _scrollPos;
    private readonly Dictionary<GameObject, bool> _selection = new Dictionary<GameObject, bool>();
    private string _groupName = string.Empty;

    private void OnGUI()
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

        if (GUILayout.Button("Create Interaction Objects"))
        {
            CreateInteractionObjects();
        }
    }

    private void CreateInteractionObjects()
    {
        var selected = new List<GameObject>();
        foreach (var kv in _selection)
        {
            if (kv.Value)
            {
                selected.Add(kv.Key);
            }
        }

        if (selected.Count == 0 || string.IsNullOrEmpty(_groupName))
        {
            Debug.LogWarning("No objects selected or group name empty.");
            return;
        }

        string basePath = "Assets/Interactionsobjects";
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
        if (!File.Exists(specFile))
        {
            File.WriteAllText(specFile, "{}");
        }

        GameObject root = new GameObject(_groupName);
        foreach (var go in selected)
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
