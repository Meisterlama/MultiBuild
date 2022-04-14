using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class BatchBuildWindow : EditorWindow
{
    [Serializable]
    private class BuildConfig
    {
        public string Name;
        public string BuildRootDirectory;

        public bool CopyFolder;
        public string CopyFolderPath;

        public bool BuildWindows;
        public bool BuildLinux;
        public bool BuildMac;
        public bool BuildServers;
        public bool EnableSteam;
        
        public void SetDefaults()
        {
            Name = PlayerSettings.productName;
            BuildRootDirectory = $"{Directory.GetParent(Application.dataPath)}/Build";

            CopyFolder = false;
            CopyFolderPath = $"{Application.dataPath}/CopyFolder";

            BuildWindows = true;
            BuildLinux = true;
            BuildMac = true;
            BuildServers = false;
            EnableSteam = false;
        }
    }

    private BuildConfig buildConfig;

    private const float _space = 20;

    private string BuildSettingsKey;


    private void OnEnable()
    {
        BuildSettingsKey = PlayerSettings.companyName + PlayerSettings.productName + "MULTIBUILD_Settings";
        buildConfig = new BuildConfig();
        InitFields();
    }

    private void Awake()
    {
        InitFields();

        minSize = new Vector2(400, 400);
    }


    [MenuItem("Build/Batch Build")]
    public static void ShowWindow()
    {
        GetWindow<BatchBuildWindow>("Batch Build");
    }


    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Reset Settings"))
        {
            EditorPrefs.DeleteKey(BuildSettingsKey);
            buildConfig.SetDefaults();
        }

        EditorGUILayout.EndHorizontal();

        buildConfig.Name = EditorGUILayout.TextField("Build Name", buildConfig.Name);

        buildConfig.BuildRootDirectory =
            EditorGUILayout.TextField("Build Root Directory", buildConfig.BuildRootDirectory);
       
        if (GUILayout.Button("Select Build Root Directory"))
        {
            string selectedFolder = EditorUtility.SaveFolderPanel("Select Build Root Directory", "", "");
            if (selectedFolder != "")
                buildConfig.BuildRootDirectory = selectedFolder;
        }


        EditorGUILayout.Separator();

        buildConfig.BuildWindows = EditorGUILayout.Toggle("Build Windows", buildConfig.BuildWindows);
        buildConfig.BuildLinux = EditorGUILayout.Toggle("Build Linux", buildConfig.BuildLinux);
        buildConfig.BuildMac = EditorGUILayout.Toggle("Build Mac", buildConfig.BuildMac);

        EditorGUILayout.Separator();

        buildConfig.BuildServers = EditorGUILayout.Toggle("Bundle Server Build", buildConfig.BuildServers);

        EditorGUILayout.Separator();

        /* TODO: Support Steam SDK
         buildConfig.EnableSteam = EditorGUILayout.Toggle(
            new GUIContent("Enabled Steam (?)", "Enable only assemblies definitions"),
            buildConfig.EnableSteam);

        EditorGUILayout.Separator();
        */

        buildConfig.CopyFolder = EditorGUILayout.Toggle("Copy Folder ?", buildConfig.CopyFolder);
        if (buildConfig.CopyFolder)
        {
            buildConfig.CopyFolderPath = EditorGUILayout.TextField("Copy Folder Path", buildConfig.CopyFolderPath);
                   
            if (GUILayout.Button("Select Copy Folder Directory"))
            {
                string selectedFolder = EditorUtility.SaveFolderPanel("Select Copy Folder Directory", "", "");
                if (selectedFolder != "")
                    buildConfig.CopyFolderPath = selectedFolder;
            }
        }

        EditorGUILayout.Separator();
        if (GUILayout.Button("Build"))
        {
            Build();
        }
    }

    private void OnLostFocus()
    {
        SaveFields();
    }

    private void OnDestroy()
    {
        SaveFields();
    }

    private void Build()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        string subBuildPath = System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

        BuildPlayerOptions options = new BuildPlayerOptions();

        options.scenes = new string[EditorBuildSettings.scenes.Length];
        for (int i = 0; i < options.scenes.Length; i++)
        {
            options.scenes[i] = EditorBuildSettings.scenes[i].path;
        }

        options.options = BuildOptions.None;

        string outputDir = $"{buildConfig.BuildRootDirectory}/{subBuildPath}";

        if (buildConfig.BuildWindows)
        {
            ProcessPlatform(BuildTarget.StandaloneWindows64, options, outputDir);
        }

        if (buildConfig.BuildMac)
        {
            ProcessPlatform(BuildTarget.StandaloneOSX, options, outputDir);
        }

        if (buildConfig.BuildLinux)
        {
            ProcessPlatform(BuildTarget.StandaloneLinux64, options, outputDir);
        }

        stopwatch.Stop();
        EditorUtility.RevealInFinder(outputDir);
        Debug.Log($"MultiBuild done in {stopwatch.Elapsed}");
    }

    private void ProcessPlatform(BuildTarget target, BuildPlayerOptions options, string baseDir)
    {
        string outputDir = $"{baseDir}/{target.ToString()}";
        string platformExtension = GetPlatformExtension(target);
        options.target = target;
        options.locationPathName = $"{outputDir}/{buildConfig.Name}.{platformExtension}";

        if (buildConfig.CopyFolder)
            DirectoryCopy(buildConfig.CopyFolderPath, outputDir + "/Bonus", true);

        BuildPlatform(options);
        if (buildConfig.BuildServers)
        {
            outputDir += $"/server";
            options.locationPathName = $"{outputDir}/{buildConfig.Name}.{platformExtension}";
            options.options |= BuildOptions.EnableHeadlessMode;
            BuildPlatform(options);
        }
    }

    private void BuildPlatform(BuildPlayerOptions options)
    {
        Debug.Log("Build");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Build succeeded in {summary.totalTime} with size: {summary.totalSize}");
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError($"{options.target.ToString()} failed");
            Debug.LogError(summary);
            throw new Exception();
        }
        else if (summary.result == BuildResult.Cancelled)
        {
            Debug.LogWarning("Build Cancelled");
        }
    }

    private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
    {
        // Get the subdirectories for the specified directory.
        DirectoryInfo dir = new DirectoryInfo(sourceDirName);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException(
                "Source directory does not exist or could not be found: "
                + sourceDirName);
        }

        DirectoryInfo[] dirs = dir.GetDirectories();
        // If the destination directory doesn't exist, create it.
        if (!Directory.Exists(destDirName))
        {
            Directory.CreateDirectory(destDirName);
        }

        // Get the files in the directory and copy them to the new location.
        FileInfo[] files = dir.GetFiles();
        foreach (FileInfo file in files)
        {
            if (file.Extension == ".meta") continue;

            string temppath = Path.Combine(destDirName, file.Name);
            file.CopyTo(temppath, false);
        }

        // If copying subdirectories, copy them and their contents to new location.
        if (copySubDirs)
        {
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath, copySubDirs);
            }
        }
    }

    private void InitFields()
    {
        if (EditorPrefs.HasKey(BuildSettingsKey))
        {
            buildConfig = JsonUtility.FromJson<BuildConfig>(EditorPrefs.GetString(BuildSettingsKey));
        }
        else
        {
            buildConfig.SetDefaults();
        }
    }

    private string GetPlatformExtension(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.StandaloneOSX:
                return "app";
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return "exe";
            case BuildTarget.StandaloneLinux64:
                return "x86_64";
            default:
                throw new Exception("Unhandled platform");
        }
    }

    private void SaveFields()
    {
        EditorPrefs.SetString(BuildSettingsKey, JsonUtility.ToJson(buildConfig));
    }
}