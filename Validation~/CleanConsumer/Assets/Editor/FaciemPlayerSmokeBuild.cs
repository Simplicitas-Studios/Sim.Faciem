using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FaciemPlayerSmokeBuild
{
    public static string Build()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
        {
            throw new InvalidOperationException("Build requires idle Edit Mode.");
        }

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
            PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.Mono2x)
        {
            throw new InvalidOperationException("Expected existing StandaloneWindows64/Mono2x configuration; no settings changed.");
        }

        const string scenePath = "Assets/U1bBuildSmoke.unity";
        if (File.Exists(scenePath) || File.Exists(scenePath + ".meta"))
        {
            throw new InvalidOperationException("Refusing to overwrite an existing scene.");
        }

        Scene originalActiveScene = SceneManager.GetActiveScene();
        Scene[] originalScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        bool[] originalDirty = originalScenes.Select(scene => scene.isDirty).ToArray();
        GameObject[][] originalRoots = originalScenes.Select(scene => scene.GetRootGameObjects()).ToArray();
        try
        {
            File.Copy("SceneFixtures~/UtilitySmoke.unity", scenePath);
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                throw new InvalidOperationException("Could not import the owned saved smoke scene fixture.");
            }

            Directory.CreateDirectory("Logs");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = "Builds/FaciemConsumerSmoke.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.DetailedBuildReport
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            SaveNativeReport(report);
            UnityEditor.Compilation.Assembly[] assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            string[] expected = { "Sim.Utility", "Sim.Faciem", "Sim.Faciem.Shared", "Sim.Faciem.Controls" };
            RuntimeAssemblyEvidence[] runtime = expected.Select(name =>
            {
                UnityEditor.Compilation.Assembly assembly = assemblies.Single(item => item.name == name);
                return new RuntimeAssemblyEvidence
                {
                    name = name,
                    references = assembly.assemblyReferences.Select(item => item.name)
                        .Concat(assembly.compiledAssemblyReferences.Select(Path.GetFileNameWithoutExtension))
                        .Distinct().OrderBy(item => item).ToArray(),
                    editorDefinePresent = assembly.defines.Contains("UNITY_EDITOR")
                };
            }).ToArray();
            var evidence = new BuildEvidence
            {
                unity = Application.unityVersion,
                target = options.target.ToString(),
                backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone).ToString(),
                result = report.summary.result.ToString(),
                errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings,
                buildSeconds = report.summary.totalTime.TotalSeconds,
                executable = options.locationPathName,
                runtimeAssemblies = runtime,
                optionalEditorAssemblyIncluded = assemblies.Any(item => item.name == "Sim.Utility.Faciem.Editor")
            };
            string json = JsonUtility.ToJson(evidence, true);
            File.WriteAllText("Logs/u1b-player-build.json", json + Environment.NewLine);
            if (report.summary.result != BuildResult.Succeeded || evidence.optionalEditorAssemblyIncluded ||
                runtime.Any(item => item.editorDefinePresent || item.references.Any(name => name.StartsWith("UnityEditor", StringComparison.Ordinal))))
            {
                throw new InvalidOperationException("Player build/reference validation failed. See Logs/u1b-player-build.json.");
            }

            return json;
        }
        finally
        {
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }

            AssetDatabase.DeleteAsset(scenePath);
            bool unchanged = originalScenes.All(scene => scene.IsValid() && scene.isLoaded) &&
                originalScenes.Select((scene, index) => scene.isDirty == originalDirty[index] &&
                    scene.GetRootGameObjects().SequenceEqual(originalRoots[index])).All(value => value) &&
                SceneManager.sceneCount == originalScenes.Length && SceneManager.GetActiveScene() == originalActiveScene;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/u1b-player-scene-restoration.txt", unchanged ? "preserved\n" : "CHANGED\n");
            if (!unchanged)
            {
                throw new InvalidOperationException("Original scene state changed during smoke build; inspect without retrying.");
            }
        }
    }

    private static void SaveNativeReport(BuildReport report)
    {
        var native = new NativeReportEvidence
        {
            guid = report.summary.guid.ToString(),
            result = report.summary.result.ToString(),
            platform = report.summary.platform.ToString(),
            outputPath = report.summary.outputPath,
            totalErrors = report.summary.totalErrors,
            totalWarnings = report.summary.totalWarnings,
            totalSize = report.summary.totalSize,
            totalSeconds = report.summary.totalTime.TotalSeconds,
            startedUtc = report.summary.buildStartedAt.ToUniversalTime().ToString("O"),
            endedUtc = report.summary.buildEndedAt.ToUniversalTime().ToString("O"),
            steps = report.steps.Select(step => new NativeStepEvidence
            {
                name = step.name,
                seconds = step.duration.TotalSeconds,
                messages = step.messages.Select(message => new NativeMessageEvidence
                {
                    type = message.type.ToString(),
                    content = message.content
                }).ToArray()
            }).ToArray()
        };
        File.WriteAllText("Logs/u1b-final-native-buildreport.json", JsonUtility.ToJson(native, true) + Environment.NewLine);
        if (File.Exists("Library/LastBuild.buildreport"))
        {
            File.Copy("Library/LastBuild.buildreport", "Logs/u1b-final-native-player.buildreport", true);
        }
    }

    [Serializable]
    private sealed class NativeReportEvidence
    {
        public string guid;
        public string result;
        public string platform;
        public string outputPath;
        public int totalErrors;
        public int totalWarnings;
        public ulong totalSize;
        public double totalSeconds;
        public string startedUtc;
        public string endedUtc;
        public NativeStepEvidence[] steps;
    }

    [Serializable]
    private sealed class NativeStepEvidence
    {
        public string name;
        public double seconds;
        public NativeMessageEvidence[] messages;
    }

    [Serializable]
    private sealed class NativeMessageEvidence
    {
        public string type;
        public string content;
    }

    [Serializable]
    private sealed class BuildEvidence
    {
        public string unity;
        public string target;
        public string backend;
        public string result;
        public int errors;
        public int warnings;
        public double buildSeconds;
        public string executable;
        public RuntimeAssemblyEvidence[] runtimeAssemblies;
        public bool optionalEditorAssemblyIncluded;
    }

    [Serializable]
    private sealed class RuntimeAssemblyEvidence
    {
        public string name;
        public string[] references;
        public bool editorDefinePresent;
    }
}
