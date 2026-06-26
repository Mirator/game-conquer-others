using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class PresentationAssetBuilder
{
    private const string ResourcesPath = "Assets/Resources";
    private const string PresentationPath = ResourcesPath + "/Presentation";
    private const string AnimationPath = PresentationPath + "/Animations";
    private const string CatalogPath = ResourcesPath + "/PresentationCatalog.asset";
    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    private static readonly AnimationSourceDefinition[] AnimationSources =
    {
        new("Assets/ThirdParty/Quaternius/Animations/UAL1_Standard.fbx",
            "AssetDownloads/Quaternius/Universal Animation Library[Standard].zip",
            "Universal Animation Library[Standard]/Unity/UAL1_Standard.fbx"),
        new("Assets/ThirdParty/Quaternius/Animations2/UAL2_Standard.fbx",
            "AssetDownloads/Quaternius/Universal Animation Library 2[Standard].zip",
            "Universal Animation Library 2[Standard]/Unity/UAL2_Standard.fbx")
    };

    private static readonly AnimationClipSpec[] FighterAnimationSpecs =
    {
        new("Idle", "Sword_Idle", "Armature|Sword_Idle", "Armature|Idle_Loop"),
        new("Walk", "Walk", "Armature|Walk_Loop"),
        new("FormationWalk", "FormationWalk", "Armature|Walk_Formal_Loop"),
        new("Jog", "Jog", "Armature|Jog_Fwd_Loop"),
        // Attacks are animated procedurally (FighterView.ApplyAttackSwing); the body
        // only needs locomotion/idle/hit/death/block clips. A single generic "Attack"
        // clip is kept for the primitive fallback rig and validation.
        new("Attack", "Sword_Attack", "Armature|Sword_Regular_A", "Armature|Sword_Attack"),
        new("Block", "Sword_Block", "Armature|Sword_Block"),
        new("Hit", "Hit", "Armature|Hit_Knockback", "Armature|Hit_Chest"),
        new("Death", "Death", "Armature|Death01")
    };

    static PresentationAssetBuilder()
    {
        EditorApplication.delayCall += EnsureCatalog;
    }

    [MenuItem("Conquer Others/Presentation/Rebuild Catalog")]
    public static void EnsureCatalog()
    {
        Directory.CreateDirectory(ResourcesPath);
        PresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<PresentationCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.courtyard = EnsureTheme("Courtyard", ArenaType.Courtyard, new Color(1f, 0.72f, 0.5f),
            new Color(0.26f, 0.28f, 0.3f), new Color(0.48f, 0.55f, 0.58f), 0.009f);
        catalog.forest = EnsureTheme("Forest", ArenaType.Forest, new Color(0.8f, 0.9f, 0.65f),
            new Color(0.16f, 0.25f, 0.17f), new Color(0.28f, 0.4f, 0.3f), 0.013f);
        catalog.marsh = EnsureTheme("Marsh", ArenaType.Marsh, new Color(0.68f, 0.8f, 0.82f),
            new Color(0.25f, 0.32f, 0.34f), new Color(0.4f, 0.52f, 0.52f), 0.021f);
        catalog.highlands = EnsureTheme("Highlands", ArenaType.Highlands, new Color(1f, 0.78f, 0.58f),
            new Color(0.26f, 0.29f, 0.31f), new Color(0.5f, 0.55f, 0.58f), 0.011f);
        catalog.swings = LoadClips("knifeSlice");
        catalog.impacts = LoadClips("chop");
        catalog.blocks = LoadClips("metal");
        catalog.footsteps = LoadClips("footstep");
        catalog.ui = LoadClips("click");
        catalog.panelBorder = LoadSprite("Assets/ThirdParty/Kenney/FantasyUI/panel-border-024.png");
        catalog.buttonBorder = LoadSprite("Assets/ThirdParty/Kenney/FantasyUI/panel-border-010.png");
        EnsureHumanoidImporters();
        RuntimeAnimatorController controller = EnsureFighterController();
        catalog.captainPrefab = EnsureFighterPrefab("Captain", "Assets/ThirdParty/Quaternius/Characters/Male_Ranger.fbx",
            FighterVariant.Captain, controller);
        catalog.militiaPrefab = EnsureFighterPrefab("Militia", "Assets/ThirdParty/Quaternius/Characters/Male_Peasant.fbx",
            FighterVariant.Militia, controller);
        catalog.veteranPrefab = EnsureFighterPrefab("Veteran", "Assets/ThirdParty/Quaternius/Characters/Male_Ranger.fbx",
            FighterVariant.Veteran, controller);
        catalog.guardPrefab = EnsureFighterPrefab("Guard", "Assets/ThirdParty/Quaternius/Characters/Male_Ranger.fbx",
            FighterVariant.Guard, controller);
        catalog.enemyPrefab = EnsureFighterPrefab("Enemy", "Assets/ThirdParty/Quaternius/Characters/Male_Peasant.fbx",
            FighterVariant.Enemy, controller);
        catalog.swordPrefab = LoadModel("Assets/ThirdParty/Quaternius/FantasyProps/Sword_Bronze.fbx");
        catalog.shieldPrefab = LoadModel("Assets/ThirdParty/Quaternius/FantasyProps/Shield_Wooden.fbx");
        catalog.villageWall = LoadModel("Assets/ThirdParty/Quaternius/MedievalVillage/Wall_UnevenBrick_Straight.fbx");
        catalog.villageArch = LoadModel("Assets/ThirdParty/Quaternius/MedievalVillage/Wall_Arch.fbx");
        catalog.villageWagon = LoadModel("Assets/ThirdParty/Quaternius/MedievalVillage/Prop_Wagon.fbx");
        catalog.villageCrate = LoadModel("Assets/ThirdParty/Quaternius/MedievalVillage/Prop_Crate.fbx");
        catalog.villageFence = LoadModel("Assets/ThirdParty/Quaternius/MedievalVillage/Prop_WoodenFence_Single.fbx");
        catalog.villageTowerRoof = LoadModel("Assets/ThirdParty/Quaternius/MedievalVillage/Roof_Tower_RoundTiles.fbx");
        catalog.banner = LoadModel("Assets/ThirdParty/Quaternius/FantasyProps/Banner_1.fbx");
        catalog.barrel = LoadModel("Assets/ThirdParty/Quaternius/FantasyProps/Barrel.fbx");
        catalog.propCrate = LoadModel("Assets/ThirdParty/Quaternius/FantasyProps/Crate_Wooden.fbx");
        catalog.weaponStand = LoadModel("Assets/ThirdParty/Quaternius/FantasyProps/WeaponStand.fbx");
        const string nature = "Assets/ThirdParty/Quaternius/Nature/";
        EnsureNatureTextures(nature);
        catalog.commonTree = LoadModel(nature + "CommonTree_1.fbx");
        catalog.pineTree = LoadModel(nature + "Pine_2.fbx");
        catalog.deadTree = LoadModel(nature + "DeadTree_2.fbx");
        catalog.rock = LoadModel(nature + "Rock_Medium_1.fbx");
        catalog.bush = LoadModel(nature + "Bush_Common.fbx");
        catalog.treeVariants = LoadModels(nature, "CommonTree_1", "CommonTree_2", "CommonTree_3", "CommonTree_4", "CommonTree_5");
        catalog.pineVariants = LoadModels(nature, "Pine_1", "Pine_2", "Pine_3");
        catalog.deadTreeVariants = LoadModels(nature, "DeadTree_1", "DeadTree_2", "DeadTree_4");
        catalog.rockVariants = LoadModels(nature, "Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3");
        // Solid-ish detail only — the flat plane-based grass/fern models read as
        // cardboard if their alpha texture doesn't bind, so they are kept out of the
        // general carpet (open ground uses the procedural instanced grass instead).
        catalog.groundClutter = LoadModels(nature, "Flower_3_Group", "Flower_4_Group", "Mushroom_Common",
            "Pebble_Round_1", "Pebble_Round_3", "Bush_Common_Flowers");
        catalog.barrenClutter = LoadModels(nature, "Pebble_Round_1", "Pebble_Round_3", "Mushroom_Common");
        catalog.tallGrass = LoadModels(nature, "Grass_Common_Tall", "Grass_Wispy_Tall", "Fern_1");

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Conquer Others/Presentation/Validate Catalog")]
    public static void ValidateCatalog()
    {
        PresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<PresentationCatalog>(CatalogPath);
        if (catalog == null || catalog.courtyard == null || catalog.forest == null || catalog.marsh == null
            || catalog.highlands == null || catalog.swings.Length == 0 || catalog.blocks.Length == 0
            || catalog.footsteps.Length == 0 || catalog.ui.Length == 0)
            throw new System.InvalidOperationException("Presentation catalog is incomplete.");
        Debug.Log("Presentation catalog validation passed.");
    }

    private static ArenaThemeDefinition EnsureTheme(string name, ArenaType arena, Color sun, Color ambient,
        Color fog, float density)
    {
        string path = $"{ResourcesPath}/{name}Theme.asset";
        ArenaThemeDefinition theme = AssetDatabase.LoadAssetAtPath<ArenaThemeDefinition>(path);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<ArenaThemeDefinition>();
            AssetDatabase.CreateAsset(theme, path);
        }
        theme.arena = arena;
        theme.sunlight = sun;
        theme.ambient = ambient;
        theme.fog = fog;
        theme.fogDensity = density;
        EditorUtility.SetDirty(theme);
        return theme;
    }

    private static AudioClip[] LoadClips(string contains)
    {
        List<AudioClip> clips = new();
        foreach (string guid in AssetDatabase.FindAssets($"t:AudioClip {contains}", new[] { "Assets/ThirdParty/Kenney" }))
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip != null)
                clips.Add(clip);
        }
        return clips.ToArray();
    }

    private static Sprite LoadSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && (importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Single))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static GameObject LoadModel(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

    private static GameObject[] LoadModels(string folder, params string[] names)
    {
        List<GameObject> models = new();
        foreach (string name in names)
        {
            GameObject model = LoadModel(folder + name + ".fbx");
            if (model != null)
                models.Add(model);
        }
        return models.ToArray();
    }

    // Quaternius nature ships base/normal textures in the kit's Textures folder.
    // We extract them beside the FBX and let the importer (material-description +
    // embedded materials, recursive texture search) bind them. Normal maps must be
    // flagged so they read as normals rather than colour. This is what makes trees
    // and rocks render textured instead of flat-shaded.
    private static void EnsureNatureTextures(string folder)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("_Normal"))
                continue;
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not ModelImporter mi)
                continue;
            bool changed = false;
            if (mi.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
            {
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                changed = true;
            }
            if (mi.materialLocation != ModelImporterMaterialLocation.InPrefab)
            {
                mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
                changed = true;
            }
            if (changed)
                mi.SaveAndReimport();
        }
    }

    private static GameObject EnsureFighterPrefab(string name, string modelPath, FighterVariant variant,
        RuntimeAnimatorController controller)
    {
        const string prefabRoot = "Assets/Resources/Presentation";
        Directory.CreateDirectory(prefabRoot);
        string prefabPath = $"{prefabRoot}/{name}.prefab";
        GameObject model = LoadModel(modelPath);
        if (model == null)
            return null;

        GameObject root = new(name);
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
        visual.name = "Authored Model";
        visual.transform.SetParent(root.transform, false);
        FighterView view = root.AddComponent<FighterView>();
        view.animator = visual.GetComponentInChildren<Animator>();
        if (view.animator != null)
        {
            view.animator.runtimeAnimatorController = controller;
            view.animator.applyRootMotion = false;
            view.anchors.rightHand = view.animator.GetBoneTransform(HumanBodyBones.RightHand);
            view.anchors.leftHand = view.animator.GetBoneTransform(HumanBodyBones.LeftHand);
            view.anchors.projectile = view.anchors.rightHand;
        }
        view.anchors.hit = visual.transform;
        view.anchors.footsteps = root.transform;
        List<Renderer> tintRenderers = new();
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
            if (renderer.name.Contains("Body") || renderer.name.Contains("Belt") || renderer.name.Contains("Pauldron"))
                tintRenderers.Add(renderer);
        view.teamTintRenderers = tintRenderers.ToArray();
        // The head is generated at runtime; never bake one into the prefab asset.
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child != null && child.name == "Generated Head")
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    private static void EnsureHumanoidImporters()
    {
        foreach (string path in new[]
        {
            "Assets/ThirdParty/Quaternius/Characters/Male_Peasant.fbx",
            "Assets/ThirdParty/Quaternius/Characters/Male_Ranger.fbx"
        })
            EnsureHumanoidImporter(path);
    }

    private static RuntimeAnimatorController EnsureFighterController()
    {
        bool createdTemporarySources = EnsureTemporaryAnimationSourcesIfNeeded();
        try
        {
            string path = PresentationPath + "/Fighter.controller";
            Directory.CreateDirectory(PresentationPath);
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimationClip deathClip = null;
            foreach (AnimationClipSpec spec in FighterAnimationSpecs)
            {
                AnimationClip clip = EnsureAnimationClip(spec);
                AddState(machine, spec.StateName, clip);
                if (spec.StateName == "Death")
                    deathClip = clip;
            }
            // A mirrored copy of the single death clip so deaths don't all look cloned —
            // Mecanim humanoid mirroring flips left/right into a believably different fall.
            if (deathClip != null)
                AddState(machine, "DeathMirror", deathClip).mirror = true;
            machine.defaultState = machine.states[0].state;
            return controller;
        }
        finally
        {
            if (createdTemporarySources)
                CleanupTemporaryAnimationSources();
        }
    }

    private static AnimatorState AddState(AnimatorStateMachine machine, string name, AnimationClip motion)
    {
        if (motion == null)
            throw new System.InvalidOperationException($"Missing animation clip for fighter state '{name}'.");
        AnimatorState state = machine.AddState(name);
        state.motion = motion;
        state.writeDefaultValues = true;
        return state;
    }

    private static AnimationClip FindClip(string name)
    {
        foreach (AnimationSourceDefinition source in AnimationSources)
        {
            if (!File.Exists(ProjectFile(source.AssetPath)))
                continue;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(source.AssetPath))
                if (asset is AnimationClip clip && clip.name == name)
                    return clip;
        }
        return null;
    }

    private static AnimationClip EnsureAnimationClip(AnimationClipSpec spec)
    {
        Directory.CreateDirectory(AnimationPath);
        string path = $"{AnimationPath}/{spec.AssetName}.anim";
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null)
            return existing;
        foreach (string sourceName in spec.SourceNames)
        {
            AnimationClip source = FindClip(sourceName);
            if (source == null)
                continue;
            AnimationClip clip = UnityEngine.Object.Instantiate(source);
            clip.name = spec.AssetName;
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
        throw new System.InvalidOperationException($"Could not extract '{spec.AssetName}'. Expected one of: {string.Join(", ", spec.SourceNames)}.");
    }

    private static bool EnsureTemporaryAnimationSourcesIfNeeded()
    {
        bool missingClip = false;
        foreach (AnimationClipSpec spec in FighterAnimationSpecs)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimationPath}/{spec.AssetName}.anim") == null)
                missingClip = true;
        }
        if (!missingClip)
            return false;

        bool created = false;
        foreach (AnimationSourceDefinition source in AnimationSources)
        {
            if (File.Exists(ProjectFile(source.AssetPath)))
                continue;
            string zipPath = ProjectFile(source.DownloadZipPath);
            if (!File.Exists(zipPath))
                throw new FileNotFoundException(
                    $"Missing ignored Quaternius animation download needed to regenerate curated clips: {source.DownloadZipPath}");
            string assetPath = ProjectFile(source.AssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            ExtractZipEntry(zipPath, source.ZipEntry, assetPath);
            AssetDatabase.ImportAsset(source.AssetPath, ImportAssetOptions.ForceSynchronousImport);
            EnsureHumanoidImporter(source.AssetPath);
            created = true;
        }
        return created;
    }

    private static void ExtractZipEntry(string zipPath, string entryName, string outputPath)
    {
        using FileStream zipStream = File.OpenRead(zipPath);
        using ZipArchive archive = new(zipStream, ZipArchiveMode.Read);
        ZipArchiveEntry entry = archive.GetEntry(entryName);
        if (entry == null)
            throw new FileNotFoundException($"Could not find '{entryName}' in '{zipPath}'.");
        using Stream input = entry.Open();
        using FileStream output = File.Create(outputPath);
        input.CopyTo(output);
    }

    private static void EnsureHumanoidImporter(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null || importer.animationType == ModelImporterAnimationType.Human)
            return;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.SaveAndReimport();
    }

    private static void CleanupTemporaryAnimationSources()
    {
        foreach (AnimationSourceDefinition source in AnimationSources)
            if (File.Exists(ProjectFile(source.AssetPath)))
                AssetDatabase.DeleteAsset(source.AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static string ProjectFile(string projectRelativePath) =>
        Path.Combine(ProjectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));

    [MenuItem("Conquer Others/Presentation/Log Imported Models")]
    public static void LogImportedModels()
    {
        foreach (string path in new[]
        {
            "Assets/ThirdParty/Quaternius/Characters/Male_Peasant.fbx",
            "Assets/ThirdParty/Quaternius/Characters/Male_Ranger.fbx"
        })
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            Debug.Log($"{path}: {string.Join(", ", System.Array.ConvertAll(assets, item => $"{item.GetType().Name}:{item.name}"))}");
        }
        foreach (string path in new[]
        {
            "Assets/ThirdParty/Quaternius/MedievalVillage/Wall_UnevenBrick_Straight.fbx",
            "Assets/ThirdParty/Quaternius/MedievalVillage/Wall_Arch.fbx",
            "Assets/ThirdParty/Quaternius/FantasyProps/Banner_1.fbx",
            "Assets/ThirdParty/Quaternius/FantasyProps/WeaponStand.fbx"
        })
        {
            GameObject prefab = LoadModel(path);
            if (prefab == null)
                continue;
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            Bounds bounds = default;
            bool found = false;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                bounds = found ? Encapsulate(bounds, renderer.bounds) : renderer.bounds;
                found = true;
            }
            Debug.Log($"{path}: bounds center={bounds.center} size={bounds.size}");
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static Bounds Encapsulate(Bounds bounds, Bounds other)
    {
        bounds.Encapsulate(other);
        return bounds;
    }

    private sealed class AnimationSourceDefinition
    {
        public readonly string AssetPath;
        public readonly string DownloadZipPath;
        public readonly string ZipEntry;

        public AnimationSourceDefinition(string assetPath, string downloadZipPath, string zipEntry)
        {
            AssetPath = assetPath;
            DownloadZipPath = downloadZipPath;
            ZipEntry = zipEntry;
        }
    }

    private sealed class AnimationClipSpec
    {
        public readonly string StateName;
        public readonly string AssetName;
        public readonly string[] SourceNames;

        public AnimationClipSpec(string stateName, string assetName, params string[] sourceNames)
        {
            StateName = stateName;
            AssetName = assetName;
            SourceNames = sourceNames;
        }
    }

    private enum FighterVariant
    {
        Captain,
        Militia,
        Veteran,
        Guard,
        Enemy
    }
}
