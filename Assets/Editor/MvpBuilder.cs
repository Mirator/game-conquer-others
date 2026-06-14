using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MvpBuilder
{
    [MenuItem("Conquer Others/Build Windows MVP")]
    public static void BuildWindows()
    {
        EnsureProceduralShaders();
        string outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Windows"));
        Directory.CreateDirectory(outputDirectory);

        BuildPlayerOptions options = new()
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = Path.Combine(outputDirectory, "ConquerOthers.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"MVP build failed: {report.summary.result}");

        Debug.Log($"Conquer Others MVP built at {options.locationPathName}");
    }

    private static void EnsureProceduralShaders()
    {
        Object graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
        SerializedObject serialized = new(graphicsSettings);
        SerializedProperty shaders = serialized.FindProperty("m_AlwaysIncludedShaders");
        string[] names = { "Standard", "Legacy Shaders/Diffuse", "Sprites/Default" };

        foreach (string shaderName in names)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                continue;
            bool found = false;
            for (int i = 0; i < shaders.arraySize; i++)
                found |= shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader;
            if (found)
                continue;
            int index = shaders.arraySize;
            shaders.InsertArrayElementAtIndex(index);
            shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
