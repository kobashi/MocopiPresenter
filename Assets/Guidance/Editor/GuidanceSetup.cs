using System.IO;
using Mocopi.Receiver;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Guidance.EditorTools
{
    /// <summary>
    /// ガイダンス用シーンの生成と macOS アプリのビルド。メニュー「Guidance」またはバッチモードから実行する。
    /// </summary>
    public static class GuidanceSetup
    {
        private const string SampleScenePath = "Assets/MocopiReceiver/Samples/ReceiverSample/Scenes/ReceiverSample.unity";
        private const string ScenePath = "Assets/Guidance/Scenes/Guidance.unity";
        private const string QrPath = "Assets/Guidance/Textures/GameQr.png";
        // Sony 配布の RAYNOS（再配布禁止のため Git 管理外）。無ければサンプルアバターのまま使う
        private const string AvatarVrmPath = "Assets/Guidance/Avatars/RAYNOS/RAYNOS-chan_1.0.6.vrm";
        private const string AvatarPrefabPath = "Assets/Guidance/Avatars/RAYNOS/RAYNOS-chan_1.0.6.prefab";

        /// <summary>
        /// バッチモード用（-quit を付けずに実行する）。UniVRM は prefab を delayCall で生成するため、できあがるまで待つ。
        /// </summary>
        public static void ImportAvatarAndCreateScene()
        {
            // TextMeshPro の基本リソース（シェーダーと設定）は、Window > TextMeshPro > Import TMP Essential Resources で入れておく
            if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                Debug.LogError("TMP Essential Resources が未導入です");
                EditorApplication.Exit(1);
                return;
            }

            // RAYNOS の VRM が無ければ、プラグイン同梱のサンプルアバターのままシーンを作る
            if (!File.Exists(AvatarVrmPath))
            {
                Debug.Log("RAYNOS の VRM が無いので、サンプルアバターでシーンを作ります: " + AvatarVrmPath);
                CreateScene();
                EditorApplication.Exit(0);
                return;
            }

            AssetDatabase.ImportAsset(AvatarVrmPath, ImportAssetOptions.ForceUpdate);
            double deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Wait;

            void Wait()
            {
                bool ready = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPrefabPath) != null;
                if (!ready && EditorApplication.timeSinceStartup < deadline)
                {
                    return;
                }

                EditorApplication.update -= Wait;
                try
                {
                    if (!ready)
                    {
                        throw new IOException("VRM の prefab が生成されません: " + AvatarPrefabPath);
                    }

                    CreateScene();
                    EditorApplication.Exit(0);
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                    EditorApplication.Exit(1);
                }
            }
        }

        [MenuItem("Guidance/シーンを作り直す")]
        public static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(SampleScenePath, ScenePath))
            {
                throw new IOException("サンプルシーンをコピーできません: " + SampleScenePath);
            }

            EditorSceneManager.OpenScene(ScenePath);

            MocopiSimpleReceiver receiver = Object.FindFirstObjectByType<MocopiSimpleReceiver>();
            if (receiver == null)
            {
                throw new System.InvalidOperationException("シーンに MocopiSimpleReceiver がありません");
            }

            ReplaceAvatar(receiver);
            StageBuilder.Build();

            var hud = new GameObject("ConnectionHud").AddComponent<ConnectionHud>();
            hud.Receiver = receiver;

            var importer = (TextureImporter)AssetImporter.GetAtPath(QrPath);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();

            var guide = new GameObject("QrGuide").AddComponent<QrGuide>();
            guide.QrCode = AssetDatabase.LoadAssetAtPath<Texture2D>(QrPath);
            guide.gameObject.AddComponent<ScreenshotOption>().Guide = guide;
            Object.FindFirstObjectByType<SlideDeck>().Guide = guide;

            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "NagoyaBunri";
            PlayerSettings.productName = "MocopiPresenter";
            // アプリが背面に回っても受信とアバターの動きを止めない
            PlayerSettings.runInBackground = true;
            // UniVRM（MToon）の推奨設定
            PlayerSettings.colorSpace = ColorSpace.Linear;
            AssetDatabase.SaveAssets();
        }

        private static void ReplaceAvatar(MocopiSimpleReceiver receiver)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("RAYNOS の prefab が無いため、サンプルアバターのままにします: " + AvatarPrefabPath);
                return;
            }

            MocopiAvatar sample = receiver.AvatarSettings[0].MocopiAvatar;
            var avatar = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            avatar.name = "RAYNOS";
            avatar.transform.SetPositionAndRotation(sample.transform.position, sample.transform.rotation);
            receiver.AvatarSettings[0].MocopiAvatar = avatar.AddComponent<MocopiAvatar>();
            Object.DestroyImmediate(sample.gameObject);
            EditorUtility.SetDirty(receiver);
        }

        [MenuItem("Guidance/プレビュー画像を書き出す")]
        public static void RenderPreview()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Camera camera = Camera.main;
            CameraDirector director = camera.GetComponent<CameraDirector>();
            Directory.CreateDirectory("Build");
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            camera.targetTexture = target;

            SlideDeck deck = Object.FindFirstObjectByType<SlideDeck>();
            deck.Load();

            void Save(string name)
            {
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes("Build/" + name + ".png", image.EncodeToPNG());
            }

            // カメラの画角ごとに1枚ずつ、続けて場面ごとに1枚ずつ書き出す
            deck.Show(1);
            for (int i = 0; i < director.Shots.Length; i++)
            {
                director.Snap(i);
                Save("preview-" + (i + 1));
            }

            director.Snap(0);
            for (int i = 0; i < deck.Slides.Length; i++)
            {
                deck.Show(i);
                Save("slide-" + (i + 1));
            }

            camera.targetTexture = null;
            RenderTexture.active = null;
        }

        [MenuItem("Guidance/macOS アプリをビルド")]
        public static void BuildMac()
        {
            Build(BuildTarget.StandaloneOSX, "Build/MocopiPresenter.app");
        }

        [MenuItem("Guidance/Windows アプリをビルド")]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "Build/Windows/MocopiPresenter/MocopiPresenter.exe");
        }

        private static void Build(BuildTarget target, string path)
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.Exception("ビルド失敗: " + report.summary.result);
            }

            Debug.Log("ビルド完了: " + path);
        }
    }
}
