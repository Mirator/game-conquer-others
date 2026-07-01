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
    private const string BuildingsPath = PresentationPath + "/Buildings";
    // Optional curated CC0 music drop-in folder. Files named Battle.*, Overworld.*,
    // and Victory.* here are auto-wired into the catalog; absent, the synthesized
    // ProceduralMusic themes play instead.
    private const string MusicPath = "Assets/ThirdParty/OpenGameArt/Music";
    private const string WeaponsModelPath = "Assets/ThirdParty/Quaternius/Weapons/";
    private const string VillageModelPath = "Assets/ThirdParty/Quaternius/MedievalVillage/";
    private const string WeaponsZip = "AssetDownloads/Quaternius/Medieval Weapons Pack by @Quaternius.zip";
    private const string WeaponsZipRoot = "Medieval Weapons Pack by @Quaternius/FBX/";
    private const string VillageZip = "AssetDownloads/Quaternius/Medieval Village MegaKit[Standard].zip";
    private const string VillageZipRoot = "Medieval Village MegaKit[Standard]/FBX/";
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
        // On domain reload, only regenerate when something is actually missing or
        // invalid. Rebuilding unconditionally re-minted the controller, building
        // prefabs, and head assets with fresh fileIDs on every reload (constant git
        // churn) and risked leaving the animator controller with zero layers when an
        // asset import raced. The menu item still forces a full rebuild on demand.
        EditorApplication.delayCall += EnsureCatalogValid;
    }

    // Auto entry (domain reload): rebuild only when the catalog or its generated assets
    // are absent or invalid — self-healing. A valid catalog is left completely
    // untouched, so no asset is re-serialized and nothing shows up as modified in git.
    public static void EnsureCatalogValid()
    {
        if (!CatalogIsValid())
            RebuildCatalog();
    }

    [MenuItem("Conquer Others/Presentation/Rebuild Catalog")]
    public static void RebuildCatalog()
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
        // Curated CC0 music is optional: when present it replaces the synthesized
        // ProceduralMusic fallback (drop files into MusicPath and re-run this rebuild).
        catalog.battleMusic = LoadMusic("Battle");
        catalog.mapMusic = LoadMusic("Overworld");
        catalog.victoryMusic = LoadMusic("Victory");
        catalog.panelBorder = LoadSprite("Assets/ThirdParty/Kenney/FantasyUI/panel-border-024.png");
        catalog.buttonBorder = LoadSprite("Assets/ThirdParty/Kenney/FantasyUI/panel-border-010.png");
        EnsureHumanoidImporters();
        RuntimeAnimatorController controller = EnsureFighterController();
        EnsureFighterHead(catalog);
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
        // Bow/arrow ship in the Medieval Weapons Pack (vertex-coloured FBX, no external
        // textures — same as the existing props); extracted on demand from the download.
        catalog.bowPrefab = EnsureExtractedModel(WeaponsModelPath + "Bow_Wooden.fbx", WeaponsZip, WeaponsZipRoot + "Bow_Wooden.fbx");
        catalog.arrowPrefab = EnsureExtractedModel(WeaponsModelPath + "Arrow.fbx", WeaponsZip, WeaponsZipRoot + "Arrow.fbx");
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

        // Campaign settlement structures, composed from Medieval Village MegaKit pieces
        // (extracted on demand). The existing villageWall/villageTowerRoof are reused as
        // the wall face and tower cap. MapDioramaBuilder falls back to primitive blocks
        // whenever a slot stays null, so missing downloads never break the diorama.
        GameObject houseRoof = EnsureExtractedModel(VillageModelPath + "Roof_RoundTiles_4x6.fbx", VillageZip, VillageZipRoot + "Roof_RoundTiles_4x6.fbx");
        GameObject hallRoof = EnsureExtractedModel(VillageModelPath + "Roof_RoundTiles_6x8.fbx", VillageZip, VillageZipRoot + "Roof_RoundTiles_6x8.fbx");
        catalog.houseSmall = EnsureBuildingPrefab("HouseSmall", catalog.villageWall, houseRoof, new Vector3(1.6f, 1.4f, 1.4f), 1);
        catalog.houseLarge = EnsureBuildingPrefab("HouseLarge", catalog.villageWall, houseRoof, new Vector3(2.0f, 1.8f, 1.7f), 1);
        catalog.townHall = EnsureBuildingPrefab("TownHall", catalog.villageWall, hallRoof, new Vector3(2.6f, 2.6f, 2.2f), 2);
        catalog.castleKeep = EnsureBuildingPrefab("CastleKeep", catalog.villageWall, hallRoof, new Vector3(3.0f, 4.2f, 2.8f), 3);
        catalog.castleTower = EnsureTowerPrefab("CastleTower", catalog.villageWall, catalog.villageTowerRoof, new Vector2(0.95f, 3.4f));

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

    // True when the catalog and its always-generated assets already exist and are
    // usable, letting the domain-reload auto-rebuild be skipped. Gates only the
    // essentials that are unconditionally generated (themes, fighter prefabs,
    // buildings, core SFX/UI) plus a structurally sound animator controller.
    // Download-dependent slots (bow/arrow, nature variant pools) are deliberately not
    // gated, so a missing optional download never forces a perpetual rebuild.
    private static bool CatalogIsValid()
    {
        PresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<PresentationCatalog>(CatalogPath);
        if (catalog == null)
            return false;
        bool refs = catalog.courtyard != null && catalog.forest != null && catalog.marsh != null
            && catalog.highlands != null && catalog.captainPrefab != null && catalog.militiaPrefab != null
            && catalog.veteranPrefab != null && catalog.guardPrefab != null && catalog.enemyPrefab != null
            && catalog.swordPrefab != null && catalog.shieldPrefab != null && catalog.houseSmall != null
            && catalog.houseLarge != null && catalog.townHall != null && catalog.castleKeep != null
            && catalog.castleTower != null && catalog.panelBorder != null && catalog.buttonBorder != null;
        bool sfx = catalog.swings != null && catalog.swings.Length > 0 && catalog.blocks != null
            && catalog.blocks.Length > 0 && catalog.footsteps != null && catalog.footsteps.Length > 0
            && catalog.ui != null && catalog.ui.Length > 0;
        return refs && sfx && ControllerHasExpectedStates();
    }

    // The generated Fighter.controller must have a base layer carrying every state the
    // runtime cross-fades to. A zero-layer or state-missing controller (which a raced
    // import can leave behind) counts as invalid, so the next reload regenerates it.
    private static bool ControllerHasExpectedStates()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            PresentationPath + "/Fighter.controller");
        if (controller == null || controller.layers.Length == 0)
            return false;
        HashSet<string> states = new();
        foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            states.Add(child.state.name);
        foreach (AnimationClipSpec spec in FighterAnimationSpecs)
            if (!states.Contains(spec.StateName))
                return false;
        return states.Contains("DeathMirror");
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

    // Finds an optional curated music clip by exact base name in the drop-in folder.
    // Returns null when the folder or file is absent, leaving the synth fallback active.
    private static AudioClip LoadMusic(string baseName)
    {
        if (!Directory.Exists(MusicPath))
            return null;
        foreach (string guid in AssetDatabase.FindAssets($"t:AudioClip {baseName}", new[] { MusicPath }))
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip != null && clip.name == baseName)
                return clip;
        }
        return null;
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

    // Loads a model, extracting its FBX from the (gitignored) download zip on first use
    // if it isn't already in the project. Quaternius "Standard" FBX carry vertex colours,
    // so no companion textures are needed. Returns null (caller falls back) if the zip is
    // absent — e.g. a checkout without the downloads.
    private static GameObject EnsureExtractedModel(string assetPath, string zipPath, string zipEntry)
    {
        string assetFile = ProjectFile(assetPath);
        if (!File.Exists(assetFile))
        {
            string zipFile = ProjectFile(zipPath);
            if (!File.Exists(zipFile))
            {
                Debug.LogWarning($"Missing download '{zipPath}'; cannot extract '{assetPath}'. Using primitive fallback.");
                return null;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(assetFile));
            ExtractZipEntry(zipFile, zipEntry, assetFile);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }
        return LoadModel(assetPath);
    }

    // World-space AABB of an already-placed instance, expressed in `reference` space.
    // Uses MeshFilter.sharedMesh.bounds corners (not Renderer.bounds, which reads stale
    // immediately after InstantiatePrefab during an editor batch) so measurements are
    // reliable mid-composition.
    private static Bounds MeasureBounds(GameObject instance, Transform reference)
    {
        Bounds bounds = default;
        bool found = false;
        foreach (MeshFilter mf in instance.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null)
                continue;
            Bounds lb = mf.sharedMesh.bounds;
            Transform tr = mf.transform;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = lb.center + Vector3.Scale(lb.extents,
                    new Vector3((i & 1) == 0 ? 1 : -1, (i & 2) == 0 ? 1 : -1, (i & 4) == 0 ? 1 : -1));
                Vector3 w = reference.InverseTransformPoint(tr.TransformPoint(corner));
                if (!found) { bounds = new Bounds(w, Vector3.zero); found = true; }
                else bounds.Encapsulate(w);
            }
        }
        return found ? bounds : new Bounds(Vector3.zero, Vector3.one);
    }

    // Target world thickness of a composed wall panel (the wall mesh is a thin slab; this is
    // how thick it reads after scaling, in diorama units).
    private const float WallThickness = 0.16f;

    // Raw mesh size of a wall piece (model space, x=width, y=thickness, z=height). Read from
    // the mesh rather than an instance so the prefab's internal axis-conversion rotation does
    // not reshuffle the axes.
    private static Vector3 WallMeshSize(GameObject wall)
    {
        MeshFilter mf = wall.GetComponentInChildren<MeshFilter>(true);
        return mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size : new Vector3(0.02f, 0.004f, 0.031f);
    }

    // Composes an enclosed building from modular kit pieces: four upright wall faces per
    // floor topped with a tiled gable roof, saved under Resources so MapDioramaBuilder can
    // instantiate it. The Quaternius wall ships lying flat (height along local Z) and at
    // ~1x import scale, while roofs import at ~100x — so walls are stood upright with a
    // per-axis scale and the roof is scaled by a *relative multiplier* (preserving its
    // peak). size = (width, height, depth). Returns null when a piece is unavailable.
    private static GameObject EnsureBuildingPrefab(string name, GameObject wall, GameObject roof,
        Vector3 size, int floors, float roofOverhang = 1.06f)
    {
        if (wall == null)
            return null;
        Directory.CreateDirectory(BuildingsPath);
        string prefabPath = $"{BuildingsPath}/{name}.prefab";
        float w = size.x, h = size.y, d = size.z;

        GameObject root = new(name);

        // Wall natural dimensions from the raw mesh (x=width, y=thickness, z=height). We read
        // the mesh directly rather than an instance AABB: the prefab carries an internal
        // axis-conversion rotation that would otherwise swap the height/thickness axes. AddWall
        // applies the target dimensions as an absolute local scale, then stands the panel up.
        Vector3 nat = WallMeshSize(wall);
        float natW = Mathf.Max(1e-4f, nat.x), natT = Mathf.Max(1e-4f, nat.y), natH = Mathf.Max(1e-4f, nat.z);
        float thickScale = WallThickness / natT;
        float floorH = h / Mathf.Max(1, floors);

        for (int floor = 0; floor < floors; floor++)
        {
            float baseY = floor * floorH;
            AddWall(root.transform, wall, 0f,   new Vector3(0f, baseY, -d * 0.5f), w / natW, thickScale, floorH / natH);
            AddWall(root.transform, wall, 180f, new Vector3(0f, baseY,  d * 0.5f), w / natW, thickScale, floorH / natH);
            AddWall(root.transform, wall, 90f,  new Vector3(-w * 0.5f, baseY, 0f), d / natW, thickScale, floorH / natH);
            AddWall(root.transform, wall, 270f, new Vector3( w * 0.5f, baseY, 0f), d / natW, thickScale, floorH / natH);
        }

        if (roof != null)
            AddRoof(root.transform, roof, w, d, h, roofOverhang);

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    // Stands one wall panel upright (Euler 90 about X) then yaws it to face outward, scales
    // it per local axis (x=width, y=thickness, z=height), and seats its base at the floor Y.
    private static void AddWall(Transform parent, GameObject wall, float yaw, Vector3 facePosition,
        float widthScale, float thickScale, float heightScale)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(wall);
        go.name = "Wall";
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(widthScale, thickScale, heightScale);
        go.transform.localPosition = facePosition;
        Bounds b = MeasureBounds(go, parent);
        go.transform.localPosition += new Vector3(0f, facePosition.y - b.min.y, 0f);
    }

    // Caps a footprint with a tiled roof, scaled by a uniform relative multiplier (so the
    // roof's natural peak is preserved) and seated on top of the walls at height `topY`.
    private static void AddRoof(Transform parent, GameObject roof, float width, float depth, float topY, float overhang)
    {
        GameObject r = (GameObject)PrefabUtility.InstantiatePrefab(roof);
        r.name = "Roof";
        r.transform.SetParent(parent, false);
        Bounds natural = MeasureBounds(r, parent);
        float s = width * overhang / Mathf.Max(1e-4f, natural.size.x);
        r.transform.localScale *= s;
        Bounds b = MeasureBounds(r, parent);
        // Sink the roof a touch into the wall tops so no seam shows at the eave line.
        r.transform.localPosition += new Vector3(-b.center.x, topY - b.min.y - 0.05f, -b.center.z);
    }

    // A square corner tower is just a single-floor building with a square footprint and the
    // kit's tower roof (which overhangs more). Used four times per castle by MapDioramaBuilder.
    // size = (footprint, height).
    private static GameObject EnsureTowerPrefab(string name, GameObject wall, GameObject roof, Vector2 size)
        => EnsureBuildingPrefab(name, wall, roof, new Vector3(size.x, size.y, size.x), 1, 1.25f);

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

    private const string CharactersPath = "Assets/ThirdParty/Quaternius/Characters/";
    private const string BaseCharacterModel = CharactersPath + "Superhero_Male_FullBody.fbx";

    // Quaternius modular outfits (Male_Peasant) ship without a head — the head is meant
    // to come from the Universal Base Character. We carve a head-only mesh out of the
    // base body (verts weighted to the Head/neck bones), bake it into Head-bone-local
    // space so it can mount rigidly on the animated Head bone, and pair it with the
    // separate Eyes/Eyebrows meshes. The result is saved as a prefab the runtime mounts
    // in place of the primitive sphere. Returns null (→ sphere fallback) if the base
    // character or its textures are not imported.
    private static void EnsureFighterHead(PresentationCatalog catalog)
    {
        GameObject baseModel = LoadModel(BaseCharacterModel);
        if (baseModel == null)
            return;
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(baseModel);
        try
        {
            SkinnedMeshRenderer body = null, eyes = null, brow = null;
            foreach (SkinnedMeshRenderer smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.name == "SuperHero_Male") body = smr;
                else if (smr.name == "Eyes") eyes = smr;
                else if (smr.name == "Eyebrows") brow = smr;
            }
            if (body == null)
                return;
            // Save the carved meshes as assets, then reference them (plus the textured
            // materials) directly from the catalog. Referencing mesh assets from the
            // ScriptableObject is reliable, whereas baking them into a prefab during this
            // same builder pass serialized null MeshFilter references.
            SaveMesh(BuildHeadMesh(body, true), "FighterHeadSkin");
            if (eyes != null) SaveMesh(BuildHeadMesh(eyes, false), "FighterHeadEyes");
            if (brow != null) SaveMesh(BuildHeadMesh(brow, false), "FighterHeadBrows");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            catalog.headSkinMesh = AssetDatabase.LoadAssetAtPath<Mesh>(PresentationPath + "/FighterHeadSkin.asset");
            catalog.headEyesMesh = AssetDatabase.LoadAssetAtPath<Mesh>(PresentationPath + "/FighterHeadEyes.asset");
            catalog.headBrowsMesh = AssetDatabase.LoadAssetAtPath<Mesh>(PresentationPath + "/FighterHeadBrows.asset");
            catalog.headSkinMaterial = EnsureHeadMaterial("FighterHeadSkin", "T_Superhero_Male_Dark.png", "T_Superhero_Male_Normal.png", 0.15f);
            catalog.headEyesMaterial = EnsureHeadMaterial("FighterHeadEyes", "T_Eye_Brown.png", null, 0.3f);
            catalog.headBrowsMaterial = EnsureHeadMaterial("FighterHeadBrows", "T_Hair_1_BaseColor.png", null, 0.1f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inst);
        }
    }

    // Carves a sub-mesh out of a skinned base-character mesh and rebases its vertices
    // into the Head bone's local space (via the bind pose) so a plain MeshRenderer
    // parented to the animated Head bone reproduces the bind-pose head and follows it.
    // headOnly keeps only vertices predominantly weighted to Head/neck (the skin body
    // mesh); for the already-head-local Eyes/Eyebrows meshes it keeps everything.
    private static Mesh BuildHeadMesh(SkinnedMeshRenderer smr, bool headOnly)
    {
        Transform[] bones = smr.bones;
        int headIdx = -1, neckIdx = -1;
        for (int i = 0; i < bones.Length; i++)
        {
            string n = bones[i].name.ToLower();
            if (n == "head") headIdx = i;
            else if (n == "neck_01") neckIdx = i;
        }
        Mesh src = smr.sharedMesh;
        BoneWeight[] bw = src.boneWeights;
        Vector3[] vs = src.vertices;
        Vector3[] ns = src.normals;
        Vector2[] uv = src.uv;
        Matrix4x4 toHead = src.bindposes[headIdx];

        int count = vs.Length;
        bool[] keep = new bool[count];
        int[] map = new int[count];
        int kept = 0;
        for (int i = 0; i < count; i++)
        {
            bool k = !headOnly || WeightOf(bw[i], headIdx) + (neckIdx >= 0 ? WeightOf(bw[i], neckIdx) : 0f) >= 0.5f;
            if (k) { keep[i] = true; map[i] = kept++; }
        }
        List<Vector3> nv = new(); List<Vector3> nn = new(); List<Vector2> nu = new();
        for (int i = 0; i < count; i++)
            if (keep[i])
            {
                nv.Add(toHead.MultiplyPoint3x4(vs[i]));
                nn.Add(toHead.MultiplyVector(ns[i]).normalized);
                nu.Add(i < uv.Length ? uv[i] : Vector2.zero);
            }
        int[] tri = src.triangles;
        List<int> nt = new();
        for (int i = 0; i < tri.Length; i += 3)
            if (keep[tri[i]] && keep[tri[i + 1]] && keep[tri[i + 2]])
            {
                nt.Add(map[tri[i]]); nt.Add(map[tri[i + 1]]); nt.Add(map[tri[i + 2]]);
            }
        Mesh mesh = new() { name = "FighterHead" };
        mesh.SetVertices(nv);
        mesh.SetNormals(nn);
        mesh.SetUVs(0, nu);
        mesh.SetTriangles(nt, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static float WeightOf(BoneWeight w, int idx)
    {
        float t = 0f;
        if (w.boneIndex0 == idx) t += w.weight0;
        if (w.boneIndex1 == idx) t += w.weight1;
        if (w.boneIndex2 == idx) t += w.weight2;
        if (w.boneIndex3 == idx) t += w.weight3;
        return t;
    }

    private static void SaveMesh(Mesh mesh, string name)
    {
        string path = $"{PresentationPath}/{name}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
    }

    private static Material EnsureHeadMaterial(string name, string baseTexture, string normalTexture, float glossiness)
    {
        string path = $"{PresentationPath}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(CharactersPath + baseTexture);
        mat.SetFloat("_Glossiness", glossiness);
        mat.SetFloat("_Metallic", 0f);
        if (normalTexture != null)
        {
            string normalPath = CharactersPath + normalTexture;
            if (AssetImporter.GetAtPath(normalPath) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (normal != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normal);
            }
        }
        EditorUtility.SetDirty(mat);
        return mat;
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
