using UnityEditor;
using UnityEngine;

// Editor convenience: create the live-tunable CombatBalance asset in Resources
// so combat numbers can be edited in the inspector (including during play)
// instead of recompiling. Without the asset the runtime uses baked defaults.
public static class CombatBalanceAssetTool
{
    private const string Directory = "Assets/Resources";
    private const string AssetPath = Directory + "/CombatBalance.asset";

    [MenuItem("Conquer Others/Create Combat Balance Asset")]
    public static void CreateCombatBalanceAsset()
    {
        CombatBalanceData existing = AssetDatabase.LoadAssetAtPath<CombatBalanceData>(AssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log($"CombatBalance asset already exists at {AssetPath}.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(Directory))
            AssetDatabase.CreateFolder("Assets", "Resources");

        CombatBalanceData data = ScriptableObject.CreateInstance<CombatBalanceData>();
        AssetDatabase.CreateAsset(data, AssetPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = data;
        EditorGUIUtility.PingObject(data);
        Debug.Log($"Created CombatBalance asset at {AssetPath}. Edit it to tune combat live.");
    }
}
