using System;
using System.IO;
using System.Linq;
using TheTrail.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Canvas = TheTrail.Core.Canvas;

namespace TheTrail.EditorTools
{
    /// <summary>
    /// One-time project configuration so a fresh clone opens ready to play and build: creates the
    /// (empty) main scene, adds it to Build Settings, applies iOS player settings and the app icon.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string BundleId = "com.rushingtech.theogtrail";
        public const string ProductName = "The OG Trail";
        /// <summary>CFBundleShortVersionString; must match the App Store Connect version.</summary>
        public const string AppVersion = "1.0";
        const string DoneKey = "TheTrail.ProjectSetup.v1";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode) return;
                if (File.Exists(ScenePath) && EditorPrefs.GetBool(DoneKey + Application.dataPath, false)) return;
                Run();
                EditorPrefs.SetBool(DoneKey + Application.dataPath, true);
            };
        }

        [MenuItem("The Trail/Set Up Project")]
        public static void Run()
        {
            EnsureScene();
            ApplyPlayerSettings();
            AppIcon.Generate();
            AssetDatabase.SaveAssets();
            Debug.Log("[TheTrail] Project set up. Open " + ScenePath + " and press Play.");
        }

        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                // Empty scene: Boot builds everything at runtime.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Rushing Technologies";
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleId);
            PlayerSettings.bundleVersion = AppVersion;
            // Gamma: the palette colours go to the screen exactly as written.
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = false;
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.All;
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.backgroundColor = Color.black;
            PlayerSettings.iOS.SetiPhoneLaunchScreenType(iOSLaunchScreenType.Default);
            PlayerSettings.runInBackground = false;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultScreenWidth = 1152;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        }
    }

    /// <summary>Paints the app icon from the game's own pixels: the wagon on a green phosphor screen.</summary>
    public static class AppIcon
    {
        public const string IconPath = "Assets/Art/AppIcon.png";

        static readonly Color32[] Green =
        {
            new Color32(5, 12, 7, 255), new Color32(14, 54, 26, 255), new Color32(26, 104, 46, 255),
            new Color32(46, 168, 74, 255), new Color32(84, 238, 114, 255), new Color32(206, 255, 210, 255),
        };

        public static void Generate()
        {
            // 64x64 of game pixels, scaled 16x with scanlines.
            var c = new Canvas();
            c.Clear();
            for (int y = 40; y < 64; y++) for (int x = 0; x < 64; x++) c.SetShade(x, y, 0.9f + (y - 40) * 0.03f);
            c.HLine(0, 63, 40, Canvas.Mid);
            Art.Wagon(c, 12, 18, 0.4f);
            c.Text(12, 4, "TRAIL", Canvas.Hot);
            const int n = 1024, s = 16;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var col = Green[c.Get(x / s, 63 - y / s)];
                    float scan = (y % s) < 3 ? 0.7f : 1f;
                    float u = x / (float)n - 0.5f, v = y / (float)n - 0.5f;
                    float vig = 1f - (u * u + v * v) * 0.9f;
                    px[y * n + x] = new Color32((byte)(col.r * scan * vig), (byte)(col.g * scan * vig), (byte)(col.b * scan * vig), 255);
                }
            tex.SetPixels32(px);
            Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
            File.WriteAllBytes(IconPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(IconPath);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }
    }

    /// <summary>
    /// Command-line build: Unity -batchmode -quit -projectPath . -executeMethod TheTrail.EditorTools.BuildScript.BuildIos
    /// Produces an Xcode project in ./ios, signed and archived later on the Mac (Codemagic).
    /// </summary>
    public static class BuildScript
    {
        public const string IosOutput = "ios";

        [MenuItem("The Trail/Build iOS Xcode Project")]
        public static void BuildIos()
        {
            ProjectSetup.EnsureScene();
            ProjectSetup.ApplyPlayerSettings();
            AppIcon.Generate();
            var buildNumber = Environment.GetEnvironmentVariable("BUILD_NUMBER");
            if (!string.IsNullOrEmpty(buildNumber)) PlayerSettings.iOS.buildNumber = buildNumber;
            var team = Environment.GetEnvironmentVariable("APPLE_TEAM_ID");
            if (!string.IsNullOrEmpty(team)) PlayerSettings.iOS.appleDeveloperTeamID = team;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = IosOutput,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            });
            Debug.Log("[TheTrail] iOS build " + report.summary.result + " in " + report.summary.totalTime + ", " + report.summary.totalErrors + " errors");
            bool ok = report.summary.result == BuildResult.Succeeded && report.summary.totalErrors == 0;
            if (!ok) Debug.LogError("[TheTrail] iOS build failed");
            if (Application.isBatchMode && !ok) EditorApplication.Exit(1);
        }
    }
}
