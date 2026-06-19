using UnityEditor;
using UnityEngine;

// Editor convenience: create the live-tunable FormationBalance asset in Resources
// so formation spacing/speeds can be edited in the inspector (including during
// play) instead of recompiling. Without the asset the runtime uses baked defaults.
public static class FormationBalanceAssetTool
{
    private const string Directory = "Assets/Resources";
    private const string AssetPath = Directory + "/FormationBalance.asset";

    [MenuItem("Conquer Others/Create Formation Balance Asset")]
    public static void CreateFormationBalanceAsset()
    {
        FormationBalanceData existing = AssetDatabase.LoadAssetAtPath<FormationBalanceData>(AssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log($"FormationBalance asset already exists at {AssetPath}.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(Directory))
            AssetDatabase.CreateFolder("Assets", "Resources");

        FormationBalanceData data = ScriptableObject.CreateInstance<FormationBalanceData>();
        AssetDatabase.CreateAsset(data, AssetPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = data;
        EditorGUIUtility.PingObject(data);
        Debug.Log($"Created FormationBalance asset at {AssetPath}. Edit it to tune formations live.");
    }
}
