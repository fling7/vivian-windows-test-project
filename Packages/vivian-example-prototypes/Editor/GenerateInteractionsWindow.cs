#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class GenerateInteractionsWindow : EditorWindow
{
    [MenuItem("Assets/generateinteractions")]
    public static void ShowWindow()
    {
        GetWindow<GenerateInteractionsWindow>(true, "generate interactions");
    }

    private void OnGUI()
    {
        // no content yet
    }
}
#endif
