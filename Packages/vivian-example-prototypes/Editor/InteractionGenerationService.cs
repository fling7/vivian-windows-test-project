#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

public static class InteractionGenerationService
{
    public static async Task GenerateAsync(List<GameObject> selectedObjects, Dictionary<GameObject, int> interactionSelection, string[] interactionTypes, string groupName, string description)
    {
        var prompt = BuildPrompt(selectedObjects, interactionSelection, interactionTypes, description);
        var (interactionJson, statesJson, transitionsJson) = await CallOpenAIAsync(prompt);
        CreateInteractionObjects(selectedObjects, groupName, interactionJson, statesJson, transitionsJson);
    }

    private static string BuildPrompt(List<GameObject> selectedObjects, Dictionary<GameObject, int> interactionSelection, string[] interactionTypes, string description)
    {
        var json = new StringBuilder();
        json.AppendLine("{\n    \"Elements\": [");
        for (int i = 0; i < selectedObjects.Count; i++)
        {
            var go = selectedObjects[i];
            string type = interactionTypes[interactionSelection[go]];
            json.Append(BuildElementJson(go.name, type));
            if (i < selectedObjects.Count - 1)
            {
                json.AppendLine(",");
            }
            else
            {
                json.AppendLine();
            }
        }
        json.AppendLine("    ]\n}");

        var sb = new StringBuilder();
        sb.AppendLine("You are an assistant that creates three JSON files for interaction specifications.");
        sb.AppendLine("InteractionElements.json describes interactive elements. States.json contains named states with attribute values. Transitions.json lists transitions between states.");
        sb.AppendLine("Base interaction elements:");
        sb.AppendLine(json.ToString());
        sb.AppendLine("User description:");
        sb.AppendLine(description);
        sb.AppendLine("Return a JSON object with properties 'InteractionElements', 'States', and 'Transitions' containing the respective JSON content.");
        return sb.ToString();
    }

    private static async Task<(string interaction, string states, string transitions)> CallOpenAIAsync(string prompt)
    {
        string apiKey = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new System.Exception("OPENAI_API_KEY not set");
        }

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var messages = new[]
        {
            new { role = "system", content = "You generate JSON specification files." },
            new { role = "user", content = prompt }
        };

        var body = new { model = "gpt-4o-mini", messages = messages };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseString);
        string message = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        using var specDoc = JsonDocument.Parse(message);
        string interaction = specDoc.RootElement.GetProperty("InteractionElements").GetRawText();
        string states = specDoc.RootElement.GetProperty("States").GetRawText();
        string transitions = specDoc.RootElement.GetProperty("Transitions").GetRawText();
        return (interaction, states, transitions);
    }

    private static void CreateInteractionObjects(List<GameObject> selectedObjects, string groupName, string interactionElementsJson, string statesJson, string transitionsJson)
    {
        string basePath = "Packages/vivian-example-prototypes/Resources";
        string groupPath = Path.Combine(basePath, groupName);
        string prefabFolder = Path.Combine(groupPath, "Prefabs");
        string materialsFolder = Path.Combine(groupPath, "Materials");
        string texturesFolder = Path.Combine(groupPath, "Textures");
        string specFolder = Path.Combine(groupPath, "FunctionalSpecification");

        Directory.CreateDirectory(prefabFolder);
        Directory.CreateDirectory(materialsFolder);
        Directory.CreateDirectory(texturesFolder);
        Directory.CreateDirectory(specFolder);

        File.WriteAllText(Path.Combine(specFolder, "InteractionElements.json"), interactionElementsJson);
        File.WriteAllText(Path.Combine(specFolder, "States.json"), statesJson);
        File.WriteAllText(Path.Combine(specFolder, "Transitions.json"), transitionsJson);

        GameObject root = new GameObject(groupName);
        foreach (var go in selectedObjects)
        {
            GameObject copy = Object.Instantiate(go);
            copy.transform.SetParent(root.transform, true);
        }

        string prefabPath = Path.Combine(prefabFolder, groupName + ".prefab");
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.Refresh();
        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        PrefabUtility.InstantiatePrefab(prefabAsset);
    }

    private static string BuildElementJson(string name, string type)
    {
        switch (type)
        {
            case "ToggleButton":
                return $"        {{\n            \"Type\": \"ToggleButton\",\n            \"Name\": \"{name}\",\n            \"InitialAttributeValues\": [\n                {{ \"Attribute\": \"VALUE\", \"Value\": \"false\" }}\n            ]\n        }}";
            case "Slider":
                return $"        {{\n            \"Type\": \"Slider\",\n            \"Name\": \"{name}\",\n            \"MinPosition\": {{ \"x\": 0.0, \"y\": 0.0, \"z\": 0.0 }},\n            \"MaxPosition\": {{ \"x\": 0.0, \"y\": 0.0, \"z\": 0.0 }},\n            \"InitialAttributeValues\": [\n                {{ \"Attribute\": \"VALUE\", \"Value\": \"0.0\" }},\n                {{ \"Attribute\": \"FIXED\", \"Value\": \"false\" }}\n            ],\n            \"PositionResolution\": 0,\n            \"TransitionTimeInMs\": 0\n        }}";
            case "Rotatable":
                return $"        {{\n            \"Type\": \"Rotatable\",\n            \"Name\": \"{name}\",\n            \"MinRotation\": 0.0,\n            \"MaxRotation\": 0.0,\n            \"RotationAxis\": {{\n                \"Origin\": {{ \"x\": 0.0, \"y\": 0.0, \"z\": 0.0 }},\n                \"Direction\": {{ \"x\": 0.0, \"y\": 0.0, \"z\": 1.0 }}\n            }},\n            \"InitialAttributeValues\": [\n                {{ \"Attribute\": \"VALUE\", \"Value\": \"0.0\" }},\n                {{ \"Attribute\": \"FIXED\", \"Value\": \"false\" }}\n            ],\n            \"PositionResolution\": 0,\n            \"AllowsForInfiniteRotation\": false,\n            \"TransitionTimeInMs\": 0\n        }}";
            case "TouchArea":
                return $"        {{\n            \"Type\": \"TouchArea\",\n            \"Name\": \"{name}\",\n            \"Plane\": {{ \"x\": 0.0, \"y\": 0.0, \"z\": 1.0 }},\n            \"Resolution\": {{ \"x\": 0.0, \"y\": 0.0 }}\n        }}";
            case "Movable":
                return $"        {{\n            \"Type\": \"Movable\",\n            \"Name\": \"{name}\",\n            \"InitialAttributeValues\": [\n                {{ \"Attribute\": \"POSITION\", \"Value\": \"(0.0,0.0,0.0)\" }},\n                {{ \"Attribute\": \"ROTATION\", \"Value\": \"(0.0,0.0,0.0)\" }}\n            ],\n            \"SnapPoses\": [],\n            \"TransitionTimeInMs\": 0\n        }}";
            default:
                return $"        {{\n            \"Type\": \"Button\",\n            \"Name\": \"{name}\"\n        }}";
        }
    }
}
#endif
