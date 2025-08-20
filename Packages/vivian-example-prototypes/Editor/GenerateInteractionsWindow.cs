#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        DefineInteractionElements,
        DescribeInteractions
    }

    private Step _currentStep = Step.SelectObjects;
    private Vector2 _scrollPos;
    private readonly Dictionary<GameObject, bool> _selection = new Dictionary<GameObject, bool>();
    private readonly Dictionary<GameObject, int> _interactionSelection = new Dictionary<GameObject, int>();
    private readonly string[] _interactionTypes = { "Button", "ToggleButton", "Slider", "Rotatable", "TouchArea", "Movable" };
    private readonly List<GameObject> _selectedObjects = new List<GameObject>();
    private string _groupName = string.Empty;
    private string _description = string.Empty;

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
        else if (_currentStep == Step.DescribeInteractions)
        {
            DrawDescriptionStep();
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

        if (GUILayout.Button("Next"))
        {
            _currentStep = Step.DescribeInteractions;
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

    private void DrawDescriptionStep()
    {
        EditorGUILayout.LabelField("Describe Interactions", EditorStyles.boldLabel);
        _description = EditorGUILayout.TextArea(_description, GUILayout.Height(100));

        if (GUILayout.Button("Generate"))
        {
            GenerateFromDescription();
        }
    }

    private async void GenerateFromDescription()
    {
        try
        {
            await InteractionGenerationService.GenerateAsync(_selectedObjects, _interactionSelection, _interactionTypes, _groupName, _description);
            _currentStep = Step.SelectObjects;
        }
        catch (System.Exception ex)
        {
            Debug.LogError(ex.Message);
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
