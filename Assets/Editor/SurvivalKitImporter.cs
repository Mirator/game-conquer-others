using UnityEditor;
using UnityEngine;

// Assigns the curated Kenney Survival Kit FBXs to the PresentationCatalog's camp
// fields. Run from the menu or headless via
//   -batchmode -quit -executeMethod SurvivalKitImporter.Wire
// so the catalog references are set without hand-editing the .asset YAML.
public static class SurvivalKitImporter
{
    private const string Dir = "Assets/ThirdParty/Kenney/SurvivalKit/";
    private const string CatalogPath = "Assets/Resources/PresentationCatalog.asset";

    [MenuItem("Conquer Others/Wire Survival Kit Props")]
    public static void Wire()
    {
        PresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<PresentationCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"PresentationCatalog not found at {CatalogPath}");
            return;
        }

        catalog.campfire = Load("campfire-pit");
        catalog.tent = Load("structure-canvas");
        catalog.bedroll = Load("bedroll");
        catalog.campFence = Load("fence-fortified");

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"Wired Survival Kit props — campfire:{catalog.campfire != null} tent:{catalog.tent != null} " +
            $"bedroll:{catalog.bedroll != null} campFence:{catalog.campFence != null}");
    }

    private static GameObject Load(string modelName)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}{modelName}.fbx");
        if (model == null)
            Debug.LogWarning($"Survival Kit model missing: {Dir}{modelName}.fbx");
        return model;
    }
}
