using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class PresentationValidationTests
{
    [Test]
    public void Catalog_HasThemesAndCuratedRecordedAudio()
    {
        PresentationCatalog catalog = PresentationCatalog.Load();
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.courtyard, Is.Not.Null);
        Assert.That(catalog.forest, Is.Not.Null);
        Assert.That(catalog.marsh, Is.Not.Null);
        Assert.That(catalog.highlands, Is.Not.Null);
        Assert.That(catalog.panelBorder, Is.Not.Null);
        Assert.That(catalog.buttonBorder, Is.Not.Null);
        Assert.That(catalog.swings, Is.Not.Empty);
        Assert.That(catalog.blocks, Is.Not.Empty);
        Assert.That(catalog.footsteps, Is.Not.Empty);
        Assert.That(catalog.ui, Is.Not.Empty);
        Assert.That(catalog.captainPrefab, Is.Not.Null);
        Assert.That(catalog.militiaPrefab, Is.Not.Null);
        Assert.That(catalog.veteranPrefab, Is.Not.Null);
        Assert.That(catalog.guardPrefab, Is.Not.Null);
        Assert.That(catalog.enemyPrefab, Is.Not.Null);
        Assert.That(catalog.veteranPrefab, Is.Not.SameAs(catalog.captainPrefab));
        Assert.That(catalog.guardPrefab, Is.Not.SameAs(catalog.captainPrefab));
        Assert.That(catalog.enemyPrefab, Is.Not.SameAs(catalog.militiaPrefab));
        Assert.That(catalog.swordPrefab, Is.Not.Null);
        Assert.That(catalog.shieldPrefab, Is.Not.Null);
        Assert.That(catalog.villageWall, Is.Not.Null);
        Assert.That(catalog.banner, Is.Not.Null);
        Assert.That(catalog.barrel, Is.Not.Null);
        Assert.That(catalog.weaponStand, Is.Not.Null);
        Assert.That(catalog.commonTree, Is.Not.Null);
        FighterView fighter = catalog.captainPrefab.GetComponent<FighterView>();
        Assert.That(fighter, Is.Not.Null);
        Assert.That(fighter.animator, Is.Not.Null);
        Assert.That(fighter.anchors.rightHand, Is.Not.Null);
        Assert.That(fighter.anchors.leftHand, Is.Not.Null);
    }

    [Test]
    public void FighterController_UsesCuratedAnimationAssetsOnly()
    {
        string[] sourceLibraries =
        {
            "Assets/ThirdParty/Quaternius/Animations/UAL1_Standard.fbx",
            "Assets/ThirdParty/Quaternius/Animations2/UAL2_Standard.fbx"
        };
        foreach (string path in sourceLibraries)
        {
            Assert.That(File.Exists(path), Is.False, $"{path} should remain an ignored temporary source, not a committed asset.");
            Assert.That(File.Exists(path + ".meta"), Is.False, $"{path}.meta should not be committed.");
        }

        string[] animationAssets =
        {
            "Sword_Idle", "Walk", "FormationWalk", "Jog", "Sword_Attack", "Sword_Block", "Hit", "Death"
        };
        foreach (string assetName in animationAssets)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"Assets/Resources/Presentation/Animations/{assetName}.anim");
            Assert.That(clip, Is.Not.Null, $"{assetName}.anim must be a generated curated clip.");
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Resources/Presentation/Fighter.controller");
        Assert.That(controller, Is.Not.Null);
        AnimatorState[] states = controller.layers[0].stateMachine.states.Select(child => child.state).ToArray();
        foreach (string expectedState in new[] { "Idle", "Walk", "FormationWalk", "Jog", "Attack", "Block", "Hit", "Death" })
            Assert.That(states.Any(state => state.name == expectedState), Is.True, $"Missing fighter state {expectedState}.");
        foreach (AnimatorState state in states)
        {
            Assert.That(state.motion, Is.Not.Null, $"{state.name} has no animation motion.");
            string assetPath = AssetDatabase.GetAssetPath(state.motion);
            Assert.That(assetPath, Does.StartWith("Assets/Resources/Presentation/Animations/"),
                $"{state.name} should use a generated clip, not a source FBX clip.");
        }
    }

    // Curated music clips (battleMusic/mapMusic/victoryMusic, ArenaThemeDefinition.music)
    // ship empty, so the audible music layer resolves through the synthesized fallback.
    // Guard that both themes generate real, in-range, mono audio rather than silence.
    [Test]
    public void ProceduralMusic_GeneratesAudibleInRangeThemes()
    {
        foreach (AudioClip clip in new[] { ProceduralMusic.BattleTheme(), ProceduralMusic.OverworldTheme() })
        {
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.samples, Is.GreaterThan(0));
            float[] data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            Assert.That(data.Any(sample => Mathf.Abs(sample) > 0.001f), Is.True, "theme should contain audio, not silence.");
            Assert.That(data.All(sample => sample >= -1f && sample <= 1f), Is.True, "samples must stay within [-1, 1].");
        }
    }

    [Test]
    public void Settings_RoundTripThroughPlayerPrefs()
    {
        SettingsService.ResetForTests();
        SettingsService.Current.mouseSensitivity = 1.37f;
        SettingsService.Current.cameraShake = 0.42f;
        SettingsService.Current.reduceMotion = true;
        SettingsService.SaveAndApply();
        SettingsService.Load();
        Assert.That(SettingsService.Current.mouseSensitivity, Is.EqualTo(1.37f).Within(0.001f));
        Assert.That(SettingsService.Current.cameraShake, Is.EqualTo(0.42f).Within(0.001f));
        Assert.That(SettingsService.Current.reduceMotion, Is.True);
        SettingsService.ResetForTests();
    }
}
