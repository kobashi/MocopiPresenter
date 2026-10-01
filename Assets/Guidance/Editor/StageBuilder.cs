using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Guidance.EditorTools
{
    /// <summary>
    /// 舞台（床・立ち位置の台・背面スクリーン・照明）を作る。シーン内の「Stage」だけを作り直し、他のオブジェクトには触らない。
    /// </summary>
    public static class StageBuilder
    {
        private const string AssetFolder = "Assets/Guidance/Stage";
        private const string MeshFolder = AssetFolder + "/Meshes";
        // カメラは -Z 向きなので、X がマイナスの側が客席から見て右
        private const float AvatarX = -1.4f;
        private const float ScreenX = 1.2f;
        private static readonly Color Background = new Color(0.02f, 0.03f, 0.08f);
        private static readonly Color Cyan = new Color(0.1f, 0.85f, 1f);
        private static readonly Color Magenta = new Color(1f, 0.25f, 0.7f);

        [MenuItem("Guidance/舞台を作り直す")]
        public static void RebuildInOpenScene()
        {
            Build();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        public static void Build()
        {
            Directory.CreateDirectory(AssetFolder);
            // 計算で作った形の置き場。作り直しても同じファイルを上書きする（参照が変わらないように）
            if (!AssetDatabase.IsValidFolder(MeshFolder))
            {
                AssetDatabase.CreateFolder(AssetFolder, "Meshes");
            }

            GameObject old = GameObject.Find("Stage");
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            Transform stage = new GameObject("Stage").transform;

            Material metal = Metal("Metal", new Color(0.16f, 0.18f, 0.24f), 0.85f, 0.6f);
            Material cyan = Glow("GlowCyan", Cyan);
            Material magenta = Glow("GlowMagenta", Magenta);

            // 床：1m 間隔の光るグリッド。間に細い補助線と、タイルの溝の凹凸を入れる
            Material floorMaterial = Lit("Floor", new Color(0.02f, 0.025f, 0.045f), Cyan * 1.15f, 0.45f);
            floorMaterial.SetTexture("_EmissionMap", GridTexture());
            floorMaterial.SetTexture("_BumpMap", NormalTexture("FloorNormal", 256, 3f, (x, y) =>
            {
                // グリッド線の位置が溝になる
                float edge = Mathf.Min(Mathf.Min(x, 1f - x), Mathf.Min(y, 1f - y));
                return Mathf.Clamp01(edge / 0.012f);
            }));
            floorMaterial.EnableKeyword("_NORMALMAP");
            floorMaterial.SetFloat("_Metallic", 0.15f);
            floorMaterial.SetTextureScale("_MainTex", new Vector2(40f, 40f));
            GameObject floor = Primitive(PrimitiveType.Plane, "Floor", stage, floorMaterial);
            floor.transform.localPosition = new Vector3(0f, -0.004f, 0f);
            floor.transform.localScale = new Vector3(4f, 1f, 4f);

            // 立ち位置の台と、縁の光る輪
            GameObject platform = Primitive(PrimitiveType.Cylinder, "Platform", stage, Metal("Platform", new Color(0.1f, 0.11f, 0.16f), 0.6f, 0.7f));
            platform.GetComponent<MeshFilter>().sharedMesh = DiscMesh();
            platform.transform.localPosition = new Vector3(AvatarX, -0.002f, 0f);
            platform.transform.localScale = new Vector3(2.4f, 0.002f, 2.4f);
            Shape(stage, "PlatformRing", new Vector3(AvatarX, 0.006f, 0f), MeshKit.Torus(1.23f, 0.02f, 96, 8), cyan);
            Shape(stage, "PlatformRingInner", new Vector3(AvatarX, 0.004f, 0f), MeshKit.Torus(1.05f, 0.006f, 96, 6), magenta);

            // 物理用の床（見た目はなし）。台も床も同じ高さ y=0 として扱う
            var ground = new GameObject("Ground").AddComponent<BoxCollider>();
            ground.transform.SetParent(stage, false);
            ground.center = new Vector3(0f, -0.5f, 0f);
            ground.size = new Vector3(40f, 1f, 40f);

            // 背面スクリーン（16:9）：金属の枠、内側の光る縁、四隅の飾り
            Transform frame = new GameObject("ScreenFrame").transform;
            frame.SetParent(stage, false);
            frame.localPosition = new Vector3(ScreenX, 2.05f, -3.56f);
            frame.gameObject.AddComponent<BoxCollider>().size = new Vector3(6.7f, 3.9f, 0.12f);
            foreach (float side in new[] { -1f, 1f })
            {
                Box(frame, "BarH", new Vector3(0f, side * 1.87f, 0f), new Vector3(6.76f, 0.14f, 0.14f), metal, 0.035f);
                Box(frame, "BarV", new Vector3(side * 3.31f, 0f, 0f), new Vector3(0.14f, 3.6f, 0.14f), metal, 0.035f);
                Box(frame, "LineH", new Vector3(0f, side * 1.79f, 0.05f), new Vector3(6.4f, 0.02f, 0.03f), cyan, 0.006f);
                Box(frame, "LineV", new Vector3(side * 3.23f, 0f, 0.05f), new Vector3(0.02f, 3.56f, 0.03f), cyan, 0.006f);
                foreach (float up in new[] { -1f, 1f })
                {
                    // 四隅の L 字の飾り
                    Box(frame, "CornerH", new Vector3(side * 3.1f, up * 1.95f, 0.06f), new Vector3(0.5f, 0.035f, 0.05f), magenta, 0.01f);
                    Box(frame, "CornerV", new Vector3(side * 3.39f, up * 1.72f, 0.06f), new Vector3(0.035f, 0.5f, 0.05f), magenta, 0.01f);
                }
            }

            GameObject screen = Primitive(PrimitiveType.Quad, "Screen", stage, Glow("Screen", new Color(0.045f, 0.07f, 0.15f), 1f));
            screen.transform.localPosition = new Vector3(ScreenX, 2.05f, -3.5f);
            screen.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            screen.transform.localScale = new Vector3(6.4f, 3.6f, 1f);

            // スクリーン両脇の柱：光が流れるイルミネーション
            Material illumination = LoadOrCreate("Illumination", "Guidance/Illumination");
            illumination.SetColor("_ColorA", Cyan);
            illumination.SetColor("_ColorB", Magenta);
            for (int i = 0; i < 4; i++)
            {
                float side = i < 2 ? -1f : 1f;
                float step = i % 2;
                Pillar(stage, "Pillar" + i, new Vector3(ScreenX + side * (3.75f + step * 0.5f), 0f, -3.4f + step * 0.35f), 3.0f + step * 0.9f, metal, illumination, step == 0 ? magenta : cyan);
            }

            // 照明：正面のキーライトと、背後から輪郭を出す2色のライト
            Light key = Object.FindFirstObjectByType<Light>();
            if (key != null && key.type == LightType.Directional)
            {
                key.transform.rotation = Quaternion.Euler(35f, 160f, 0f);
                key.color = new Color(1f, 0.97f, 0.92f);
                key.intensity = 0.9f;
                key.shadows = LightShadows.Soft;
            }

            Spot("RimCyan", stage, new Vector3(AvatarX - 2.6f, 3.2f, -2.2f), Cyan);
            Spot("RimMagenta", stage, new Vector3(AvatarX + 2.6f, 3.2f, -2.2f), Magenta);

            // アバターを台の上（客席から見て右）に立たせる
            Mocopi.Receiver.MocopiAvatar avatar = Object.FindFirstObjectByType<Mocopi.Receiver.MocopiAvatar>();
            if (avatar != null)
            {
                avatar.transform.position = new Vector3(AvatarX, 0f, 0f);
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.24f, 0.32f);
            // 金属に映り込む景色は、暗い空にシアンとマゼンタの明かりがある絵を計算で作って使う
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = ReflectionCube();
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Background;
            RenderSettings.fogStartDistance = 9f;
            RenderSettings.fogEndDistance = 24f;

            Camera camera = Camera.main;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.fieldOfView = 45f;
            // 光る素材は 1 を超える明るさで描き、その分をにじませて光って見せる
            camera.allowHDR = true;
            BloomEffect bloom = camera.GetComponent<BloomEffect>();
            if (bloom == null)
            {
                bloom = camera.gameObject.AddComponent<BloomEffect>();
            }

            bloom.Shader = Shader.Find("Hidden/Guidance/Bloom");
            EditorUtility.SetDirty(bloom);

            // 映像の出し先（プロジェクターと発表者の画面）
            var router = new GameObject("Displays").AddComponent<DisplayRouter>();
            router.transform.SetParent(stage, false);
            router.Source = camera;

            // カメラの画角（数字キーで切り替え）
            CameraDirector director = camera.GetComponent<CameraDirector>();
            if (director == null)
            {
                director = camera.gameObject.AddComponent<CameraDirector>();
            }

            director.Camera = camera;
            director.Avatar = avatar != null ? avatar.GetComponent<Animator>() : null;
            director.Shots = new[]
            {
                new CameraDirector.Shot { Name = "全体", Position = new Vector3(0f, 1.15f, 3.9f), LookAt = new Vector3(0f, 1.01f, 0f), FieldOfView = 45f },
                new CameraDirector.Shot { Name = "全身", Position = new Vector3(0f, 1.0f, 3.4f), LookAt = new Vector3(0f, 0.8f, 0f), FieldOfView = 35f, FollowAvatar = true },
                new CameraDirector.Shot { Name = "上半身", Position = new Vector3(0f, 1.3f, 1.7f), LookAt = new Vector3(0f, 1.15f, 0f), FieldOfView = 35f, FollowAvatar = true },
                new CameraDirector.Shot { Name = "スクリーン", Position = new Vector3(ScreenX, 2.05f, 2.9f), LookAt = new Vector3(ScreenX, 2.05f, -3.5f), FieldOfView = 35f },
            };
            director.Snap(0);
            EditorUtility.SetDirty(director);

            // スクリーンに映す場面（内容は StreamingAssets/slides.json）
            var canvas = new GameObject("ScreenCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.GetComponent<UnityEngine.UI.CanvasScaler>().dynamicPixelsPerUnit = 2f;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.SetParent(stage, false);
            canvasRect.sizeDelta = new Vector2(1600f, 900f);
            canvasRect.localScale = Vector3.one * 0.004f;
            canvasRect.localPosition = new Vector3(ScreenX, 2.05f, -3.49f);
            canvasRect.localRotation = Quaternion.Euler(0f, 180f, 0f);

            SlideDeck deck = canvas.gameObject.AddComponent<SlideDeck>();
            deck.Title = Label(canvasRect, "Title", 80f, 720f, 80f, 50f, 84f, TMPro.TextAlignmentOptions.Left, true);
            deck.Big = Label(canvasRect, "Big", 80f, 420f, 80f, 190f, 170f, TMPro.TextAlignmentOptions.Center, true);
            deck.Big.enableAutoSizing = true;
            deck.Big.fontSizeMin = 60f;
            deck.Big.fontSizeMax = 170f;
            deck.Body = Label(canvasRect, "Body", 110f, 150f, 80f, 200f, 62f, TMPro.TextAlignmentOptions.TopLeft, false);
            deck.Body.lineSpacing = 25f;
            deck.Note = Label(canvasRect, "Note", 80f, 40f, 80f, 770f, 66f, TMPro.TextAlignmentOptions.Center, true);
            deck.Page = Label(canvasRect, "Page", 1300f, 20f, 40f, 840f, 34f, TMPro.TextAlignmentOptions.Right, false);
            deck.Director = director;
            deck.Guide = Object.FindFirstObjectByType<QrGuide>();

            // 演出用のパーティクル
            Material particle = ParticleMaterial();
            ParticleSystem sparks = Sparks(stage, particle);
            Ambient(stage, particle);

            // 文字の積み木（場面の blocks で指定した文字を積む）と、アバターの当たり判定
            var template = new GameObject("BlockTemplate");
            template.transform.SetParent(stage, false);
            Shape(template.transform, "Body", Vector3.zero, MeshKit.ChamferBox(Vector3.one * 0.27f, 0.028f), Lit("Block", Color.white, Color.white, 0.65f));
            template.AddComponent<BoxCollider>();
            Rigidbody body = template.AddComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            template.AddComponent<LetterBlock>();
            template.SetActive(false);

            var blocks = new GameObject("TitleBlocks").AddComponent<TitleBlocks>();
            blocks.transform.SetParent(stage, false);
            blocks.Template = template;
            blocks.Sparks = sparks;
            blocks.Avatar = director.Avatar;

            // 効果音（起動時に合成する）
            new GameObject("Sfx").AddComponent<Sfx>().transform.SetParent(stage, false);
            deck.Blocks = blocks;

            // マーブルマシン（場面で machine を指定したときだけ出す）
            deck.Machine = MarbleMachineBuilder.Build(stage, sparks, director.Avatar, particle);
            deck.Machine.gameObject.SetActive(false);

            // コーディングエージェントの場面（場面で agents を指定したときだけ出す）
            deck.Agents = AgentSceneBuilder.Build(stage, sparks, director.Avatar, particle, deck.Body);
            deck.Agents.gameObject.SetActive(false);

            // 回転式スタンド（場面で stand を指定したときだけ出す）
            deck.Stand = BookStandBuilder.Build(stage, sparks, deck.Body, deck.Note);
            deck.Stand.gameObject.SetActive(false);

            if (avatar != null && avatar.GetComponent<AvatarColliders>() == null)
            {
                avatar.gameObject.AddComponent<AvatarColliders>();
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// スクリーン上の文字（TextMeshPro）。left / bottom / right / top は 1600x900 の面の端からの余白。
        /// フォントと飾りは実行時に SlideDeck が付ける。
        /// </summary>
        private static TMPro.TextMeshProUGUI Label(RectTransform parent, string name, float left, float bottom, float right, float top, float size, TMPro.TextAlignmentOptions alignment, bool bold)
        {
            var text = new GameObject(name, typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TextMeshProUGUI>();
            RectTransform rect = text.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            text.fontSize = size;
            text.fontStyle = bold ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
            text.alignment = alignment;
            text.textWrappingMode = TMPro.TextWrappingModes.Normal;
            text.overflowMode = TMPro.TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// 当たったとき・着地したときに散る火花。スクリプトから位置と色を指定して出す。
        /// </summary>
        private static ParticleSystem Sparks(Transform parent, Material material)
        {
            ParticleSystem system = NewParticles("Sparks", parent, material);
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
            main.gravityModifier = 0.7f;
            main.maxParticles = 3000;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.06f;
            FadeOut(system);
            return system;
        }

        /// <summary>
        /// 舞台全体にゆっくり漂う光の粒
        /// </summary>
        private static void Ambient(Transform parent, Material material)
        {
            ParticleSystem system = NewParticles("AmbientParticles", parent, material);
            system.transform.localPosition = new Vector3(0f, 2f, -0.5f);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(Cyan, Magenta);
            main.maxParticles = 600;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 35f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(11f, 4f, 7f);
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.3f;
            FadeOut(system);
        }

        private static ParticleSystem NewParticles(string name, Transform parent, Material material)
        {
            var system = new GameObject(name).AddComponent<ParticleSystem>();
            system.transform.SetParent(parent, false);
            ParticleSystem.MainModule main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            system.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return system;
        }

        private static void FadeOut(ParticleSystem system)
        {
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
        }

        /// <summary>
        /// 加算合成の丸い光の粒
        /// </summary>
        private static Material ParticleMaterial()
        {
            string path = AssetFolder + "/Dot.png";
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), Vector2.one * (size - 1) * 0.5f) / (size * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    texture.SetPixel(x, y, new Color(a * a, a * a, a * a, 1f));
                }
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            Material material = LoadOrCreate("Particle", "Legacy Shaders/Particles/Additive");
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return material;
        }

        internal static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            Object.DestroyImmediate(primitive.GetComponent<Collider>());
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            return primitive;
        }

        private static void Spot(string name, Transform parent, Vector3 position, Color color)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = position;
            light.transform.LookAt(new Vector3(AvatarX, 1.1f, 0f));
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = 1.4f;
            light.range = 9f;
            light.spotAngle = 50f;
        }

        internal static Material Lit(string name, Color color, Color emission, float smoothness)
        {
            Material material = LoadOrCreate(name, "Standard");
            material.color = color;
            material.SetFloat("_Glossiness", smoothness);
            material.SetColor("_EmissionColor", emission);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            return material;
        }

        /// <summary>
        /// 金属の素材。パネルの継ぎ目とリベットの凹凸を付ける。
        /// </summary>
        internal static Material Metal(string name, Color color, float metallic, float smoothness)
        {
            Material material = Lit(name, color, Color.black, smoothness);
            material.SetFloat("_Metallic", metallic);
            material.SetTexture("_BumpMap", NormalTexture("PanelNormal", 256, 2.5f, (x, y) =>
            {
                // 段ごとに半分ずらしたパネル割りと、四隅のリベット
                float row = Mathf.Floor(y * 2f);
                float u = Mathf.Repeat(x + row * 0.5f, 1f);
                float v = Mathf.Repeat(y * 2f, 1f);
                float seam = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v) * 0.5f);
                float height = Mathf.Clamp01(seam / 0.015f);
                float rivet = Vector2.Distance(new Vector2(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f) * 0.5f), new Vector2(0.43f, 0.19f));
                return height + Mathf.Clamp01(1f - rivet / 0.02f) * 0.6f;
            }));
            material.EnableKeyword("_NORMALMAP");
            material.SetTextureScale("_MainTex", new Vector2(2f, 2f));
            return material;
        }

        /// <summary>
        /// 自分で光る素材。intensity が 1 を超えるとにじんで光って見える。
        /// </summary>
        internal static Material Glow(string name, Color color, float intensity = 1.8f)
        {
            Material material = LoadOrCreate(name, "Guidance/Glow");
            material.color = color;
            material.SetFloat("_Intensity", intensity);
            return material;
        }

        internal static Material LoadOrCreate(string name, string shader)
        {
            string path = AssetFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader.name != shader)
            {
                material.shader = Shader.Find(shader);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// 計算で作った形を置く。形はアセットとして保存する（シーンを開き直しても残るように）。
        /// </summary>
        internal static GameObject Shape(Transform parent, string name, Vector3 position, Mesh mesh, Material material)
        {
            if (!AssetDatabase.Contains(mesh))
            {
                string path = MeshFolder + "/" + mesh.name + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null)
                {
                    AssetDatabase.CreateAsset(mesh, path);
                }
                else
                {
                    EditorUtility.CopySerialized(mesh, saved);
                    mesh = saved;
                }
            }

            var shape = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.GetComponent<MeshFilter>().sharedMesh = mesh;
            shape.GetComponent<MeshRenderer>().sharedMaterial = material;
            return shape;
        }

        /// <summary>
        /// 角を面取りした箱。bevel を省くと、大きさに合わせた面取り幅になる。
        /// </summary>
        internal static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, float bevel = -1f)
        {
            if (bevel < 0f)
            {
                bevel = Mathf.Min(0.03f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.2f);
            }

            return Shape(parent, name, position, MeshKit.ChamferBox(size, bevel), material);
        }

        /// <summary>
        /// 縁を面取りした多角柱（sides を増やすと円柱）。
        /// </summary>
        internal static GameObject Column(Transform parent, string name, Vector3 position, int sides, float radius, float height, Material material, float bevel = -1f, float topScale = 1f)
        {
            if (bevel < 0f)
            {
                bevel = Mathf.Min(0.02f, Mathf.Min(radius, height) * 0.2f);
            }

            return Shape(parent, name, position, MeshKit.Prism(sides, radius, height, bevel, topScale), material);
        }

        /// <summary>
        /// 飾りの柱。台座・芯・光の管・途中の輪・てっぺんの飾りと浮かぶ輪でできている。
        /// </summary>
        private static void Pillar(Transform parent, string name, Vector3 position, float height, Material metal, Material illumination, Material glow)
        {
            Transform pillar = new GameObject(name).transform;
            pillar.SetParent(parent, false);
            pillar.localPosition = position;

            Column(pillar, "Base", new Vector3(0f, 0.08f, 0f), 8, 0.22f, 0.16f, metal, 0.03f);
            Column(pillar, "Step", new Vector3(0f, 0.21f, 0f), 8, 0.15f, 0.1f, metal, 0.02f);
            float bottom = 0.26f;
            Column(pillar, "Core", new Vector3(0f, bottom + height * 0.5f, 0f), 6, 0.06f, height, metal, 0.005f);

            // 芯のまわりに3本の光の管
            for (int i = 0; i < 3; i++)
            {
                float angle = (i * 120f + 30f) * Mathf.Deg2Rad;
                Column(pillar, "Tube" + i, new Vector3(Mathf.Cos(angle) * 0.095f, bottom + height * 0.5f, Mathf.Sin(angle) * 0.095f), 8, 0.022f, height, illumination, 0.004f);
            }

            // 途中の輪
            int collars = Mathf.RoundToInt(height / 0.75f);
            for (int i = 1; i < collars; i++)
            {
                float y = bottom + height * i / collars;
                Column(pillar, "Collar" + i, new Vector3(0f, y, 0f), 8, 0.135f, 0.06f, metal, 0.015f);
                Shape(pillar, "CollarLight" + i, new Vector3(0f, y, 0f), MeshKit.Torus(0.14f, 0.008f, 32, 6), glow);
            }

            // てっぺん
            float top = bottom + height;
            Column(pillar, "Capital", new Vector3(0f, top + 0.05f, 0f), 8, 0.16f, 0.1f, metal, 0.025f);
            Column(pillar, "Crown", new Vector3(0f, top + 0.2f, 0f), 6, 0.09f, 0.2f, illumination, 0.003f, 0.15f);
            foreach (float direction in new[] { 1f, -1f })
            {
                GameObject ring = Shape(pillar, "Halo", new Vector3(0f, top + 0.45f + (direction > 0f ? 0f : 0.12f), 0f), MeshKit.Torus(direction > 0f ? 0.2f : 0.13f, 0.012f, 48, 6), glow);
                ring.transform.localRotation = Quaternion.Euler(direction * 18f, 0f, 12f);
                ring.AddComponent<Spinner>().DegreesPerSecond = new Vector3(0f, direction * 70f, 0f);
            }
        }

        /// <summary>
        /// 高さの式から凹凸用の画像（法線マップ）を作る。height は 0〜1 の座標を受け取り、高さ（0〜1）を返す。
        /// </summary>
        private static Texture2D NormalTexture(string name, int size, float strength, System.Func<float, float, float> height)
        {
            string path = AssetFolder + "/" + name + ".png";
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            float Sample(int x, int y) => height(Mathf.Repeat((x + 0.5f) / size, 1f), Mathf.Repeat((y + 0.5f) / size, 1f));
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (Sample(x + 1, y) - Sample(x - 1 + size, y)) * strength;
                    float dy = (Sample(x, y + 1) - Sample(x, y - 1 + size)) * strength;
                    Vector3 normal = new Vector3(-dx, -dy, 1f).normalized;
                    texture.SetPixel(x, y, new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f));
                }
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.NormalMap;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// 金属への映り込み用の景色。暗い空に、左右からシアンとマゼンタ、上から白い明かりが当たっている絵。
        /// </summary>
        private static Cubemap ReflectionCube()
        {
            const int size = 32;
            string path = AssetFolder + "/Reflection.cubemap";
            AssetDatabase.DeleteAsset(path);
            var cube = new Cubemap(size, TextureFormat.RGBAHalf, true);
            for (int face = 0; face < 6; face++)
            {
                var colors = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size * 2f - 1f;
                        float v = (y + 0.5f) / size * 2f - 1f;
                        Vector3 direction;
                        switch ((CubemapFace)face)
                        {
                            case CubemapFace.PositiveX: direction = new Vector3(1f, -v, -u); break;
                            case CubemapFace.NegativeX: direction = new Vector3(-1f, -v, u); break;
                            case CubemapFace.PositiveY: direction = new Vector3(u, 1f, v); break;
                            case CubemapFace.NegativeY: direction = new Vector3(u, -1f, -v); break;
                            case CubemapFace.PositiveZ: direction = new Vector3(u, -v, 1f); break;
                            default: direction = new Vector3(-u, -v, -1f); break;
                        }

                        direction.Normalize();
                        float horizon = 1f - Mathf.Abs(direction.y);
                        Color color = new Color(0.02f, 0.03f, 0.07f);
                        color += Cyan * Mathf.Pow(Mathf.Clamp01(direction.x), 3f) * horizon * 1.6f;
                        color += Magenta * Mathf.Pow(Mathf.Clamp01(-direction.x), 3f) * horizon * 1.6f;
                        color += new Color(0.9f, 0.95f, 1f) * Mathf.Pow(Mathf.Clamp01(direction.y), 4f) * 0.8f;
                        color += Cyan * Mathf.Pow(Mathf.Clamp01(-direction.z), 6f) * 0.5f;
                        color.a = 1f;
                        colors[y * size + x] = color;
                    }
                }

                cube.SetPixels(colors, (CubemapFace)face);
            }

            cube.Apply(true);
            AssetDatabase.CreateAsset(cube, path);
            return cube;
        }

        /// <summary>
        /// 縁が角ばらない円柱（標準の Cylinder と同じ寸法：半径 0.5、高さ 2）
        /// </summary>
        private static Mesh DiscMesh()
        {
            const int segments = 96;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();

            for (int cap = 0; cap < 2; cap++)
            {
                float y = cap == 0 ? 1f : -1f;
                int center = vertices.Count;
                vertices.Add(new Vector3(0f, y, 0f));
                normals.Add(Vector3.up * y);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * 0.5f, y, Mathf.Sin(angle) * 0.5f));
                    normals.Add(Vector3.up * y);
                }

                for (int i = 1; i <= segments; i++)
                {
                    triangles.Add(center);
                    triangles.Add(center + (cap == 0 ? i + 1 : i));
                    triangles.Add(center + (cap == 0 ? i : i + 1));
                }
            }

            int side = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var normal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(normal * 0.5f + Vector3.up);
                vertices.Add(normal * 0.5f + Vector3.down);
                normals.Add(normal);
                normals.Add(normal);
            }

            for (int i = 0; i < segments; i++)
            {
                int a = side + i * 2;
                triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }

            string path = AssetFolder + "/Disc.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Disc" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Texture2D GridTexture()
        {
            string path = AssetFolder + "/Grid.png";
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 1m ごとの太い線、25cm ごとの細い補助線、交点の明るい点
                    float major = x < 4 || y < 4 ? 1f : 0f;
                    float minor = x % (size / 4) < 2 || y % (size / 4) < 2 ? 0.07f : 0f;
                    float node = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) / 14f);
                    float value = Mathf.Clamp01(Mathf.Max(major * 0.7f, minor) + node);
                    texture.SetPixel(x, y, new Color(value, value, value));
                }
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
