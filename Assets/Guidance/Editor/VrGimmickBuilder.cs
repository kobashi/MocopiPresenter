using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Guidance.EditorTools
{
    /// <summary>
    /// VR 体験会の仕掛け（headset / tracking / insideout / vrmr / anchors / apps）を組み立てる。
    /// どれもアバター（客席から見て右、x = -1.4）を中心に、その左側の空いた所へ模型を置く。
    /// 原寸の「ゲームの世界」は舞台のずっと下（霧で見えない所）に置き、そこを写したカメラの映像をゴーグルの画面に出す。
    /// </summary>
    public static class VrGimmickBuilder
    {
        private static readonly Color Cyan = new Color(0.1f, 0.85f, 1f);
        private static readonly Color Magenta = new Color(1f, 0.25f, 0.7f);
        private static readonly Color Yellow = new Color(1f, 0.85f, 0.2f);
        private static readonly Color Green = new Color(0.3f, 1f, 0.5f);
        private static readonly Color Red = new Color(1f, 0.25f, 0.3f);
        private static readonly Color Sky = new Color(0.45f, 0.7f, 0.95f);
        private const float AvatarX = -1.4f;

        // ───────────── 場面3：ヘッドトラッキングとカメラ ─────────────

        [GimmickBuilder]
        public static Gimmick Headset(GimmickContext context)
        {
            Transform root = Root("HeadsetView", context.Stage);
            Material metal = StageBuilder.Metal("Metal", new Color(0.16f, 0.18f, 0.24f), 0.85f, 0.6f);
            Material cyan = StageBuilder.Glow("GlowCyan", Cyan);
            Material magenta = StageBuilder.Glow("GlowMagenta", Magenta);

            // 舞台の上の、ゲームの世界の模型（台の上に 1/11 で置く）
            Vector3 miniAt = new Vector3(0.1f, 0f, 0.5f);
            StageBuilder.Column(root, "MiniPedestal", miniAt + new Vector3(0f, 0.43f, 0f), 24, 0.16f, 0.86f, metal, 0.02f);
            StageBuilder.Shape(root, "MiniPedestalLight", miniAt + new Vector3(0f, 0.86f, 0f), MeshKit.Torus(0.52f, 0.008f, 64, 6), cyan);
            Transform mini = new GameObject("MiniWorld").transform;
            mini.SetParent(root, false);
            mini.localPosition = miniAt + new Vector3(0f, 0.87f, 0f);
            mini.localScale = Vector3.one * 0.09f;
            GameWorld(mini);

            // 模型の中のカメラ（大きめに作る）と、写る範囲を示す四角すい
            Transform miniCamera = new GameObject("MiniCamera").transform;
            miniCamera.SetParent(mini, false);
            miniCamera.localPosition = new Vector3(0f, 1.6f, 0f);
            CameraIcon(miniCamera, 1.6f, metal, cyan);
            Frustum(miniCamera, 80f, 80f, 5f, 0.06f, magenta);

            // 原寸のゲームの世界と、左右の目のカメラ
            Transform world = new GameObject("GameWorld").transform;
            world.SetParent(root, false);
            world.localPosition = new Vector3(0f, -300f, 0f);
            GameWorld(world);
            Transform rig = new GameObject("EyeRig").transform;
            rig.SetParent(world, false);
            rig.localPosition = new Vector3(0f, 1.6f, 0f);
            Camera left = EyeCamera("LeftEye", rig, 80f);
            Camera right = EyeCamera("RightEye", rig, 80f);

            // ゴーグルの大きな模型（目の側を客席に向ける。客席から覗き込むと、左が左目の画面）
            Transform goggle = new GameObject("Goggle").transform;
            goggle.SetParent(root, false);
            goggle.localPosition = new Vector3(1.6f, 1.05f, 0.55f);
            BigGoggle(goggle, 1.5f, 0.72f, metal, cyan, magenta, out Renderer leftLens, out Renderer rightLens, out Transform leftLabel, out Transform rightLabel);

            Transform head = HeadGoggle(root);

            HeadsetView view = root.gameObject.AddComponent<HeadsetView>();
            view.Id = "headset";
            view.Avatar = context.Avatar;
            view.MiniCamera = miniCamera;
            view.EyeRig = rig;
            view.LeftEye = left;
            view.RightEye = right;
            view.LeftLens = leftLens;
            view.RightLens = rightLens;
            view.LensLabels = new[] { leftLabel, rightLabel };
            view.HeadGoggle = head;
            view.DotTemplate = Dot(root, "SignalDot", cyan);
            view.Sparks = context.Sparks;
            return view;
        }

        // ───────────── 場面4：アウトサイドイン ─────────────

        [GimmickBuilder]
        public static Gimmick Tracking(GimmickContext context)
        {
            Transform root = Root("OutsideInTracking", context.Stage);
            Material metal = StageBuilder.Metal("Metal", new Color(0.16f, 0.18f, 0.24f), 0.85f, 0.6f);
            Material dark = StageBuilder.Metal("RobotDark", new Color(0.13f, 0.15f, 0.2f), 0.8f, 0.6f);
            Material cyan = StageBuilder.Glow("GlowCyan", Cyan);

            // カメラを立てる位置（出す順）。アバターのまわりを囲む
            Vector3[] spots =
            {
                new Vector3(AvatarX + 0.9f, 0f, 0.9f),
                new Vector3(AvatarX - 1.6f, 0f, 1.3f),
                new Vector3(AvatarX - 1.5f, 0f, -1.6f),
                new Vector3(AvatarX + 1.6f, 0f, -1.6f),
            };
            Vector3 aim = new Vector3(AvatarX, 1.0f, 0f);
            var cameras = new Transform[spots.Length];
            var lenses = new Transform[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                Transform stand = new GameObject("TrackingCamera" + (i + 1)).transform;
                stand.SetParent(root, false);
                stand.localPosition = spots[i];
                StageBuilder.Column(stand, "Foot", new Vector3(0f, 0.03f, 0f), 24, 0.22f, 0.06f, metal, 0.015f);
                StageBuilder.Column(stand, "Pole", new Vector3(0f, 0.87f, 0f), 12, 0.025f, 1.65f, metal, 0.005f);
                Transform headPart = new GameObject("Head").transform;
                headPart.SetParent(stand, false);
                headPart.localPosition = new Vector3(0f, 1.75f, 0f);
                headPart.LookAt(aim);
                StageBuilder.Box(headPart, "Body", new Vector3(0f, 0f, -0.06f), new Vector3(0.2f, 0.16f, 0.22f), dark, 0.02f);
                StageBuilder.Column(headPart, "Lens", new Vector3(0f, 0f, 0.07f), 24, 0.055f, 0.06f, metal, 0.008f).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                // レンズのまわりの光る輪（赤外線のライトのつもり）
                StageBuilder.Shape(headPart, "Ring", new Vector3(0f, 0f, 0.1f), MeshKit.Torus(0.075f, 0.012f, 32, 6), cyan).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Transform lens = new GameObject("LensPoint").transform;
                lens.SetParent(headPart, false);
                lens.localPosition = new Vector3(0f, 0f, 0.11f);
                cameras[i] = stand;
                lenses[i] = lens;
            }

            OutsideInTracking tracking = root.gameObject.AddComponent<OutsideInTracking>();
            tracking.Id = "tracking";
            tracking.Avatar = context.Avatar;
            tracking.Cameras = cameras;
            tracking.Lenses = lenses;
            tracking.Seen = StageBuilder.Glow("GlowGreen", Green, 2.2f);
            tracking.Half = StageBuilder.Glow("GlowYellow", Yellow);
            tracking.Lost = StageBuilder.Glow("GlowRed", Red, 2.2f);
            tracking.MarkerTemplate = Dot(root, "Marker", tracking.Seen);
            tracking.MarkerTemplate.transform.localScale = Vector3.one * 0.06f;
            tracking.BeamMaterial = BeamMaterial();
            tracking.Sparks = context.Sparks;
            return tracking;
        }

        // ───────────── 場面5：インサイドアウト（カメラと LiDAR） ─────────────

        [GimmickBuilder]
        public static Gimmick InsideOut(GimmickContext context)
        {
            Transform root = Root("InsideOutScan", context.Stage);
            Material magenta = StageBuilder.Glow("GlowMagenta", Magenta);

            InsideOutScan scan = root.gameObject.AddComponent<InsideOutScan>();
            scan.Id = "insideout";
            scan.Avatar = context.Avatar;
            scan.HeadGoggle = HeadGoggle(root);

            // カメラの視野（四角すい）。縦横 90° で作り、実行時に設定の角度に合わせて横と縦に伸ばす
            Transform frustum = new GameObject("CameraView").transform;
            frustum.SetParent(root, false);
            Frustum(frustum, 90f, 90f, 0.3f, 0.004f, magenta);
            scan.Frustum = frustum;

            scan.Points = PointCloud(root, "LidarPoints", 0.05f, 40000);
            scan.BeamMaterial = BeamMaterial();
            scan.HandSeen = StageBuilder.Glow("GlowGreen", Green, 2.2f);
            scan.HandLost = StageBuilder.Glow("GlowRed", Red, 2.2f);
            scan.JointTemplate = Dot(root, "Joint", scan.HandSeen);
            scan.JointTemplate.transform.localScale = Vector3.one * 0.022f;
            scan.Room = Room(root, "ScanRoom");
            scan.Sparks = context.Sparks;
            return scan;
        }

        // ───────────── 場面6：VR と MR ─────────────

        [GimmickBuilder]
        public static Gimmick Mixed(GimmickContext context)
        {
            Transform root = Root("VrMrSwitch", context.Stage);
            Material metal = StageBuilder.Metal("Metal", new Color(0.16f, 0.18f, 0.24f), 0.85f, 0.6f);
            Material cyan = StageBuilder.Glow("GlowCyan", Cyan);
            Material magenta = StageBuilder.Glow("GlowMagenta", Magenta);
            Material yellow = StageBuilder.Glow("GlowYellow", Yellow);

            // ゴーグルを付けた人に見えている景色を映す画面（16:9）
            Transform panel = new GameObject("GoggleView").transform;
            panel.SetParent(root, false);
            panel.localPosition = new Vector3(1.05f, 0.85f, 0.6f);
            const float width = 1.76f;
            const float height = 0.99f;
            GameObject display = StageBuilder.Primitive(PrimitiveType.Quad, "Display", panel, StageBuilder.LoadOrCreate("GoggleWipe", "Guidance/Wipe"));
            display.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            display.transform.localScale = new Vector3(width, height, 1f);
            foreach (float side in new[] { -1f, 1f })
            {
                StageBuilder.Box(panel, "BarH", new Vector3(0f, side * (height * 0.5f + 0.04f), -0.02f), new Vector3(width + 0.16f, 0.08f, 0.08f), metal, 0.02f);
                StageBuilder.Box(panel, "BarV", new Vector3(side * (width * 0.5f + 0.04f), 0f, -0.02f), new Vector3(0.08f, height + 0.16f, 0.08f), metal, 0.02f);
                StageBuilder.Box(panel, "LineH", new Vector3(0f, side * (height * 0.5f + 0.005f), 0.02f), new Vector3(width, 0.012f, 0.01f), cyan, 0.003f);
            }

            // 画面の右（アバター側）に、いまのモードを大きく出す
            TMP_Text label = Text(panel, "Mode", new Vector3(-width * 0.5f - 0.4f, 0.1f, 0f), new Vector2(0.6f, 0.3f), 2.2f, TextAlignmentOptions.Center);
            TMP_Text caption = Text(panel, "Caption", new Vector3(0f, -height * 0.5f - 0.17f, 0f), new Vector2(width, 0.16f), 0.9f, TextAlignmentOptions.Center);

            // 現実の景色（教室）。動画が無いときに写す。舞台のずっと下に置く
            Transform classroom = new GameObject("Classroom").transform;
            classroom.SetParent(root, false);
            classroom.localPosition = new Vector3(0f, -900f, 0f);
            Classroom(classroom);
            Camera passthrough = EyeCamera("PassthroughEye", classroom, 70f);
            passthrough.backgroundColor = new Color(0.05f, 0.05f, 0.06f);
            passthrough.depth = -12f;
            passthrough.enabled = false;
            // 360度動画を貼る背景（動画のときだけ使う）
            Material panoramaMaterial = StageBuilder.LoadOrCreate("MrPanorama", "Skybox/Panoramic");
            panoramaMaterial.SetFloat("_Mapping", 1f);
            panoramaMaterial.SetFloat("_ImageType", 0f);
            panoramaMaterial.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            panoramaMaterial.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            Skybox panorama = passthrough.gameObject.AddComponent<Skybox>();
            panorama.material = panoramaMaterial;
            panorama.enabled = false;

            // 仮想の物だけを写し、現実の景色の上に重ねるカメラ（ゴーグルを付けた人の目の位置に置く）
            Camera real = new GameObject("RealEye").AddComponent<Camera>();
            real.transform.SetParent(root, false);
            real.fieldOfView = 70f;
            real.nearClipPlane = 0.12f;
            real.farClipPlane = 30f;
            real.clearFlags = CameraClearFlags.Depth;
            real.cullingMask = 1 << VrMrSwitch.VirtualLayer;
            real.depth = -11f;
            real.enabled = false;

            // 仮想の世界と、そこを写すカメラ
            Transform world = new GameObject("VirtualWorld").transform;
            world.SetParent(root, false);
            world.localPosition = new Vector3(0f, -600f, 0f);
            GameWorld(world);
            Camera virtualEye = EyeCamera("VirtualEye", world, 70f);
            virtualEye.enabled = false;

            // MR で重ねる仮想の物（アバターのまわりに置く。客席からは見えない）
            Transform overlay = new GameObject("Overlay").transform;
            overlay.SetParent(root, false);
            GameObject cube = StageBuilder.Box(overlay, "FloatingCube", new Vector3(AvatarX + 0.1f, 1.45f, 1.3f), Vector3.one * 0.28f, magenta, 0.03f);
            Spin(cube, new Vector3(30f, 50f, 0f), 0.06f);
            Transform buddy = new GameObject("Buddy").transform;
            buddy.SetParent(overlay, false);
            buddy.localPosition = new Vector3(AvatarX + 0.75f, 1.1f, 1.0f);
            GameObject body = StageBuilder.Primitive(PrimitiveType.Sphere, "Body", buddy, yellow);
            body.transform.localScale = Vector3.one * 0.3f;
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject eye = StageBuilder.Primitive(PrimitiveType.Sphere, "Eye", buddy, StageBuilder.Lit("BuddyEye", new Color(0.05f, 0.05f, 0.08f), Color.black, 0.9f));
                // 相棒はアバター（-X 側）の方を向く
                eye.transform.localPosition = new Vector3(-0.1f, 0.05f, side * 0.06f);
                eye.transform.localScale = Vector3.one * 0.06f;
            }

            Spin(buddy.gameObject, Vector3.zero, 0.08f);
            GameObject ring = StageBuilder.Shape(overlay, "Portal", new Vector3(AvatarX - 0.9f, 1.4f, 1.2f), MeshKit.Torus(0.32f, 0.03f, 48, 8), cyan);
            ring.transform.localRotation = Quaternion.Euler(90f, 30f, 0f);
            Spin(ring, new Vector3(0f, 0f, 40f), 0.04f);
            Transform monitor = new GameObject("VirtualMonitor").transform;
            monitor.SetParent(overlay, false);
            monitor.localPosition = new Vector3(AvatarX, 1.6f, -1.4f);
            StageBuilder.Box(monitor, "Board", Vector3.zero, new Vector3(1.0f, 0.6f, 0.02f), StageBuilder.Glow("IconScreen", new Color(0.1f, 0.45f, 0.6f), 1.2f), 0.005f);
            for (int i = 0; i < 4; i++)
            {
                StageBuilder.Box(monitor, "Bar" + i, new Vector3(-0.3f + i * 0.2f, -0.2f + i * 0.06f, 0.02f), new Vector3(0.12f, 0.1f + i * 0.12f, 0.01f), i % 2 == 0 ? magenta : yellow, 0.003f);
            }

            for (int i = 0; i < 5; i++)
            {
                float angle = i * 72f + 20f;
                Vector3 at = new Vector3(AvatarX, 2.1f, 0f) + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 1.3f;
                GameObject star = StageBuilder.Column(overlay, "Star" + i, at, 5, 0.09f, 0.03f, yellow, 0.005f);
                star.transform.localRotation = Quaternion.Euler(90f, angle, 0f);
                Spin(star, new Vector3(0f, 0f, 90f), 0.05f);
            }

            overlay.gameObject.SetActive(false);

            VrMrSwitch mixed = root.gameObject.AddComponent<VrMrSwitch>();
            mixed.Id = "vrmr";
            mixed.Avatar = context.Avatar;
            mixed.HeadGoggle = HeadGoggle(root);
            mixed.RealEye = real;
            mixed.PassthroughEye = passthrough;
            mixed.Panorama = panorama;
            mixed.ClassroomOrigin = classroom;
            mixed.VirtualEye = virtualEye;
            mixed.VirtualOrigin = world;
            mixed.Overlay = overlay.gameObject;
            mixed.Display = display.GetComponent<Renderer>();
            mixed.ModeLabel = label;
            mixed.ModeCaption = caption;
            mixed.Sparks = context.Sparks;
            return mixed;
        }

        // ───────────── 場面7：空間アンカー ─────────────

        [GimmickBuilder]
        public static Gimmick Anchors(GimmickContext context)
        {
            Transform root = Root("SpatialAnchors", context.Stage);
            Material cyan = StageBuilder.Glow("GlowCyan", Cyan);
            Material magenta = StageBuilder.Glow("GlowMagenta", Magenta);
            Material yellow = StageBuilder.Glow("GlowYellow", Yellow);
            Material green = StageBuilder.Glow("GlowGreen", Green, 2.2f);
            Material wood = StageBuilder.Lit("VrWood", new Color(0.55f, 0.36f, 0.2f), Color.black, 0.3f);
            Material white = StageBuilder.Lit("Robot", new Color(0.86f, 0.89f, 0.95f), Color.black, 0.75f);

            SpatialAnchors anchors = root.gameObject.AddComponent<SpatialAnchors>();
            anchors.Id = "anchors";
            anchors.Avatar = context.Avatar;
            anchors.HeadGoggle = HeadGoggle(root);
            anchors.FeaturePoints = PointCloud(root, "FeaturePoints", 0.06f, 20000);
            // 壁は舞台のスクリーン（当たり判定あり）、床は舞台の床を使う。家具（机・箱・いす）は場面に出ている間だけ置く
            anchors.Room = Room(root, "AnchorRoom");
            anchors.BoxMaterial = BeamMaterial();
            anchors.Sparks = context.Sparks;

            // 見つけた面を示す格子
            GameObject grid = StageBuilder.Primitive(PrimitiveType.Quad, "PlaneGrid", root, GridMaterial());
            grid.SetActive(false);
            anchors.GridTemplate = grid;

            // 床に置く物（原点が床に接する所。+Z が手前）
            Transform plant = Template(root, "Plant");
            StageBuilder.Column(plant, "Pot", new Vector3(0f, 0.1f, 0f), 16, 0.1f, 0.2f, white, 0.02f, 0.8f);
            StageBuilder.Column(plant, "Leaves", new Vector3(0f, 0.38f, 0f), 7, 0.2f, 0.36f, green, 0.02f, 0.05f);
            Transform robot = Template(root, "Robot");
            StageBuilder.Box(robot, "Body", new Vector3(0f, 0.18f, 0f), new Vector3(0.24f, 0.22f, 0.18f), white, 0.04f);
            StageBuilder.Box(robot, "Face", new Vector3(0f, 0.2f, 0.092f), new Vector3(0.18f, 0.1f, 0.01f), StageBuilder.Glow("IconScreen", new Color(0.1f, 0.45f, 0.6f), 1.2f), 0.003f);
            foreach (float side in new[] { -1f, 1f })
            {
                StageBuilder.Box(robot, "Eye", new Vector3(side * 0.045f, 0.21f, 0.098f), new Vector3(0.03f, 0.03f, 0.006f), cyan, 0.002f);
                StageBuilder.Column(robot, "Wheel", new Vector3(side * 0.13f, 0.05f, 0f), 16, 0.05f, 0.04f, StageBuilder.Metal("RobotDark", new Color(0.13f, 0.15f, 0.2f), 0.8f, 0.6f), 0.008f).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            StageBuilder.Column(robot, "Antenna", new Vector3(0f, 0.34f, 0f), 6, 0.008f, 0.1f, magenta, 0.002f);
            Transform lamp = Template(root, "Lamp");
            StageBuilder.Column(lamp, "Base", new Vector3(0f, 0.02f, 0f), 16, 0.12f, 0.04f, white, 0.01f);
            StageBuilder.Column(lamp, "Stem", new Vector3(0f, 0.3f, 0f), 8, 0.015f, 0.55f, white, 0.004f);
            StageBuilder.Column(lamp, "Shade", new Vector3(0f, 0.6f, 0f), 16, 0.16f, 0.16f, yellow, 0.01f, 0.55f);
            anchors.FloorItems = new[] { plant.gameObject, robot.gameObject, lamp.gameObject };

            // 壁に掛ける物（原点が壁に接する所。+Z が壁から手前）
            Transform frame = Template(root, "Picture");
            StageBuilder.Box(frame, "Frame", new Vector3(0f, 0f, 0.02f), new Vector3(0.5f, 0.38f, 0.04f), wood, 0.01f);
            StageBuilder.Box(frame, "Sky", new Vector3(0f, 0.04f, 0.042f), new Vector3(0.42f, 0.22f, 0.005f), StageBuilder.Glow("VrSky", Sky, 1.1f), 0.002f);
            StageBuilder.Box(frame, "Hill", new Vector3(0f, -0.1f, 0.043f), new Vector3(0.42f, 0.08f, 0.005f), green, 0.002f);
            Transform clock = Template(root, "Clock");
            StageBuilder.Column(clock, "Face", new Vector3(0f, 0f, 0.02f), 32, 0.2f, 0.04f, white, 0.01f).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            StageBuilder.Shape(clock, "Rim", new Vector3(0f, 0f, 0.04f), MeshKit.Torus(0.2f, 0.015f, 48, 6), magenta).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            StageBuilder.Box(clock, "Hour", new Vector3(0f, 0.05f, 0.045f), new Vector3(0.02f, 0.1f, 0.006f), cyan, 0.002f);
            StageBuilder.Box(clock, "Minute", new Vector3(0.06f, 0f, 0.047f), new Vector3(0.14f, 0.015f, 0.006f), cyan, 0.002f);
            Transform window = Template(root, "Window");
            StageBuilder.Box(window, "Frame", new Vector3(0f, 0f, 0.02f), new Vector3(0.55f, 0.55f, 0.04f), white, 0.01f);
            StageBuilder.Box(window, "Glass", new Vector3(0f, 0f, 0.042f), new Vector3(0.47f, 0.47f, 0.005f), StageBuilder.Glow("VrSky", Sky, 1.1f), 0.002f);
            StageBuilder.Box(window, "BarV", new Vector3(0f, 0f, 0.046f), new Vector3(0.025f, 0.47f, 0.008f), white, 0.002f);
            StageBuilder.Box(window, "BarH", new Vector3(0f, 0f, 0.046f), new Vector3(0.47f, 0.025f, 0.008f), white, 0.002f);
            anchors.WallItems = new[] { frame.gameObject, clock.gameObject, window.gameObject };

            // アンカーの印（物の上に浮かぶ逆さの角すいと、根元の輪）
            Transform pin = Template(root, "AnchorPin");
            GameObject cone = StageBuilder.Column(pin, "Cone", new Vector3(0f, 0.05f, 0f), 4, 0.05f, 0.1f, magenta, 0.004f, 0.05f);
            cone.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            cone.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            StageBuilder.Shape(pin, "Ring", new Vector3(0f, 0.01f, 0f), MeshKit.Torus(0.22f, 0.008f, 48, 6), magenta);
            Spin(cone, new Vector3(0f, 90f, 0f), 0.03f);
            anchors.PinTemplate = pin.gameObject;
            return anchors;
        }

        // ───────────── 場面8：VR アプリを試そう ─────────────

        [GimmickBuilder]
        public static Gimmick Apps(GimmickContext context)
        {
            Transform root = Root("AppShowcase", context.Stage);
            Material metal = StageBuilder.Metal("Metal", new Color(0.16f, 0.18f, 0.24f), 0.85f, 0.6f);
            Material cyan = StageBuilder.Glow("GlowCyan", Cyan);
            Material magenta = StageBuilder.Glow("GlowMagenta", Magenta);
            Material yellow = StageBuilder.Glow("GlowYellow", Yellow);
            Material green = StageBuilder.Glow("GlowGreen", Green, 2.2f);
            Material red = StageBuilder.Glow("GlowRed", Red, 2.2f);
            Material blue = StageBuilder.Glow("GlowBlue", new Color(0.2f, 0.45f, 1f), 2.2f);
            Material white = StageBuilder.Lit("Robot", new Color(0.86f, 0.89f, 0.95f), Color.black, 0.75f);
            Material straw = StageBuilder.Lit("VrStraw", new Color(0.7f, 0.55f, 0.3f), Color.black, 0.2f);
            Material soil = StageBuilder.Lit("VrSoil", new Color(0.35f, 0.25f, 0.15f), Color.black, 0.1f);
            Material zombie = StageBuilder.Lit("VrZombie", new Color(0.45f, 0.7f, 0.35f), Color.black, 0.3f);
            Material dark = StageBuilder.Lit("BuddyEye", new Color(0.05f, 0.05f, 0.08f), Color.black, 0.9f);

            // アイコンの板（最初の子が浮かぶ本体。前は +Z）
            Transform tile = Template(root, "AppTile");
            Transform body = new GameObject("Body").transform;
            body.SetParent(tile, false);
            StageBuilder.Box(body, "Board", Vector3.zero, new Vector3(0.62f, 0.62f, 0.06f), StageBuilder.Lit("StandPanel", new Color(0.06f, 0.07f, 0.12f), Cyan * 0.04f, 0.6f), 0.06f);
            foreach (float side in new[] { -1f, 1f })
            {
                StageBuilder.Box(body, "EdgeV", new Vector3(side * 0.3f, 0f, 0.03f), new Vector3(0.014f, 0.56f, 0.01f), cyan, 0.004f);
                StageBuilder.Box(body, "EdgeH", new Vector3(0f, side * 0.3f, 0.03f), new Vector3(0.56f, 0.014f, 0.01f), cyan, 0.004f);
            }

            BoxCollider touch = tile.gameObject.AddComponent<BoxCollider>();
            touch.size = new Vector3(0.62f, 0.62f, 0.3f);
            touch.isTrigger = true;

            // ジェットコースター：縦の輪のレールと、坂と、車両
            Transform coaster = Template(root, "coaster");
            StageBuilder.Shape(coaster, "Loop", new Vector3(0.06f, 0.02f, 0f), MeshKit.Torus(0.12f, 0.012f, 40, 6), cyan).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject slope = StageBuilder.Box(coaster, "Slope", new Vector3(-0.13f, 0.02f, 0f), new Vector3(0.24f, 0.02f, 0.03f), cyan, 0.005f);
            slope.transform.localRotation = Quaternion.Euler(0f, 0f, -40f);
            StageBuilder.Box(coaster, "Rail", new Vector3(0.0f, -0.11f, 0f), new Vector3(0.44f, 0.02f, 0.03f), cyan, 0.005f);
            GameObject car = StageBuilder.Box(coaster, "Car", new Vector3(-0.17f, 0.08f, 0.02f), new Vector3(0.09f, 0.05f, 0.06f), magenta, 0.012f);
            car.transform.localRotation = Quaternion.Euler(0f, 0f, -40f);

            // 光る剣のリズムゲーム：交差した赤と青の剣と、矢印の付いた箱
            Transform saber = Template(root, "saber");
            foreach (float side in new[] { -1f, 1f })
            {
                Transform sword = new GameObject("Saber").transform;
                sword.SetParent(saber, false);
                sword.localRotation = Quaternion.Euler(0f, 0f, side * 35f);
                StageBuilder.Column(sword, "Blade", new Vector3(0f, 0.04f, 0f), 12, 0.014f, 0.32f, side < 0 ? red : blue, 0.006f);
                StageBuilder.Column(sword, "Grip", new Vector3(0f, -0.17f, 0f), 12, 0.02f, 0.08f, metal, 0.005f);
            }

            StageBuilder.Box(saber, "Cube", new Vector3(0f, -0.05f, 0.05f), Vector3.one * 0.11f, StageBuilder.Lit("StandPanel", new Color(0.06f, 0.07f, 0.12f), Cyan * 0.04f, 0.6f), 0.015f);
            GameObject arrow = StageBuilder.Column(saber, "Arrow", new Vector3(0f, -0.05f, 0.108f), 3, 0.035f, 0.01f, white, 0.002f);
            arrow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // 竪穴式住居：わらぶきの円すいの屋根、入口、地面
            Transform house = Template(root, "pithouse");
            StageBuilder.Column(house, "Ground", new Vector3(0f, -0.15f, 0f), 24, 0.24f, 0.03f, soil, 0.01f);
            StageBuilder.Column(house, "Roof", new Vector3(0f, -0.01f, 0f), 12, 0.2f, 0.26f, straw, 0.01f, 0.08f);
            StageBuilder.Box(house, "Door", new Vector3(0f, -0.09f, 0.15f), new Vector3(0.08f, 0.1f, 0.06f), dark, 0.01f);
            StageBuilder.Column(house, "Smoke", new Vector3(0.02f, 0.17f, 0f), 8, 0.025f, 0.06f, StageBuilder.Glow("VrSky", Sky, 1.1f), 0.01f);

            // ゾンビを撃つゲーム：緑の顔と、照準
            Transform zombieIcon = Template(root, "zombie");
            GameObject head = StageBuilder.Primitive(PrimitiveType.Sphere, "Head", zombieIcon, zombie);
            head.transform.localScale = new Vector3(0.22f, 0.26f, 0.2f);
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject eye = StageBuilder.Primitive(PrimitiveType.Sphere, "Eye", zombieIcon, red);
                eye.transform.localPosition = new Vector3(side * 0.05f, 0.03f, 0.09f);
                eye.transform.localScale = Vector3.one * 0.04f;
            }

            StageBuilder.Box(zombieIcon, "Mouth", new Vector3(0f, -0.06f, 0.095f), new Vector3(0.09f, 0.02f, 0.01f), dark, 0.003f);
            StageBuilder.Shape(zombieIcon, "Sight", new Vector3(0f, 0f, 0.13f), MeshKit.Torus(0.15f, 0.008f, 48, 6), red).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            StageBuilder.Box(zombieIcon, "SightH", new Vector3(0f, 0f, 0.13f), new Vector3(0.36f, 0.008f, 0.008f), red, 0.002f);
            StageBuilder.Box(zombieIcon, "SightV", new Vector3(0f, 0f, 0.13f), new Vector3(0.008f, 0.36f, 0.008f), red, 0.002f);

            AppShowcase apps = root.gameObject.AddComponent<AppShowcase>();
            apps.Id = "apps";
            apps.TileTemplate = tile.gameObject;
            apps.Icons = new[] { coaster.gameObject, saber.gameObject, house.gameObject, zombieIcon.gameObject };
            apps.Sparks = context.Sparks;
            return apps;
        }

        // ───────────── 共通の部品 ─────────────

        private static Transform Root(string name, Transform stage)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(stage, false);
            return root;
        }

        private static Transform Template(Transform parent, string name)
        {
            var template = new GameObject(name);
            template.transform.SetParent(parent, false);
            template.SetActive(false);
            return template.transform;
        }

        /// <summary>
        /// ゲームの世界（原寸、半径 5.5m ほど）。模型と原寸の両方をこれで作るので、同じ形になる。
        /// 向きが分かるよう、前後左右に違う物を置く（前 +Z：赤い積み木、右 +X：青いアーチ、後ろ：木、左：黄色いピラミッド）
        /// </summary>
        private static void GameWorld(Transform parent)
        {
            Material grass = StageBuilder.Lit("VrGrass", new Color(0.3f, 0.65f, 0.35f), Color.black, 0.2f);
            Material red = StageBuilder.Lit("VrRed", new Color(0.9f, 0.25f, 0.25f), Color.black, 0.5f);
            Material blue = StageBuilder.Lit("VrBlue", new Color(0.25f, 0.45f, 0.95f), Color.black, 0.5f);
            Material yellow = StageBuilder.Lit("VrYellow", new Color(0.98f, 0.82f, 0.25f), Color.black, 0.5f);
            Material leaf = StageBuilder.Lit("VrLeaf", new Color(0.15f, 0.5f, 0.25f), Color.black, 0.2f);
            Material trunk = StageBuilder.Lit("VrWood", new Color(0.55f, 0.36f, 0.2f), Color.black, 0.3f);
            Material white = StageBuilder.Lit("VrWhite", new Color(0.92f, 0.92f, 0.95f), Color.black, 0.5f);

            StageBuilder.Column(parent, "Ground", new Vector3(0f, -0.15f, 0f), 48, 5.5f, 0.3f, grass, 0.08f);

            // 前：赤い積み木
            StageBuilder.Box(parent, "RedA", new Vector3(-0.6f, 0.5f, 3.8f), Vector3.one * 1.0f, red, 0.08f);
            StageBuilder.Box(parent, "RedB", new Vector3(0.6f, 0.5f, 3.8f), Vector3.one * 1.0f, red, 0.08f);
            StageBuilder.Box(parent, "RedC", new Vector3(0f, 1.5f, 3.8f), Vector3.one * 1.0f, white, 0.08f);
            // 右：青いアーチ
            GameObject arch = StageBuilder.Shape(parent, "BlueArch", new Vector3(3.8f, 0f, 0f), MeshKit.Torus(1.4f, 0.22f, 48, 10), blue);
            arch.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // 後ろ：木
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = new Vector3(-1.6f + i * 1.6f, 0f, -3.8f - (i % 2) * 0.6f);
                StageBuilder.Column(parent, "Trunk" + i, at + new Vector3(0f, 0.4f, 0f), 8, 0.15f, 0.8f, trunk, 0.03f);
                StageBuilder.Column(parent, "Leaves" + i, at + new Vector3(0f, 1.6f, 0f), 10, 0.8f, 1.8f, leaf, 0.05f, 0.05f);
            }

            // 左：黄色いピラミッド
            StageBuilder.Column(parent, "Pyramid", new Vector3(-3.8f, 0.9f, 0f), 4, 1.4f, 1.8f, yellow, 0.05f, 0.03f);
            // 近くの物（立体視で左右の絵の違いが分かりやすいように）
            StageBuilder.Box(parent, "NearBox", new Vector3(0.5f, 0.9f, 1.4f), Vector3.one * 0.3f, yellow, 0.04f);
            StageBuilder.Column(parent, "NearPole", new Vector3(-0.6f, 0.6f, 1.6f), 12, 0.08f, 1.2f, white, 0.02f);
            for (int i = 0; i < 8; i++)
            {
                // 宙に浮かぶ小さな玉（方向の手がかり）
                float angle = i * 45f + 22.5f;
                Vector3 at = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 2.6f + Vector3.up * (2.2f + (i % 3) * 0.4f);
                StageBuilder.Column(parent, "Orb" + i, at, 12, 0.18f, 0.36f, i % 2 == 0 ? red : blue, 0.15f);
            }
        }

        /// <summary>
        /// 教室（MR の現実の景色の代わり）。原点が先生の立ち位置で、+Z に生徒の机が並ぶ。後ろ（-Z）にホワイトボード
        /// </summary>
        private static void Classroom(Transform parent)
        {
            Material floor = StageBuilder.Lit("RoomFloor", new Color(0.62f, 0.48f, 0.32f), Color.black, 0.35f);
            Material wall = StageBuilder.Lit("RoomWall", new Color(0.88f, 0.87f, 0.83f), Color.black, 0.1f);
            Material ceiling = StageBuilder.Lit("RoomCeiling", new Color(0.95f, 0.95f, 0.95f), Color.black, 0.1f);
            Material board = StageBuilder.Lit("RoomBoard", new Color(0.97f, 0.98f, 0.98f), Color.black, 0.8f);
            Material frame = StageBuilder.Lit("RoomFrame", new Color(0.55f, 0.57f, 0.6f), Color.black, 0.6f);
            Material desk = StageBuilder.Lit("RoomDesk", new Color(0.8f, 0.68f, 0.5f), Color.black, 0.4f);
            Material steel = StageBuilder.Lit("RoomSteel", new Color(0.35f, 0.36f, 0.4f), Color.black, 0.6f);
            Material chair = StageBuilder.Lit("RoomChair", new Color(0.25f, 0.4f, 0.6f), Color.black, 0.3f);
            Material window = StageBuilder.Glow("RoomWindow", new Color(0.75f, 0.88f, 1f), 1.15f);
            Material light = StageBuilder.Glow("RoomLight", new Color(1f, 0.98f, 0.92f), 1.3f);
            Material green = StageBuilder.Lit("RoomBoardGreen", new Color(0.2f, 0.35f, 0.3f), Color.black, 0.3f);
            Material ink = StageBuilder.Lit("RoomInk", new Color(0.15f, 0.25f, 0.6f), Color.black, 0.3f);

            const float w = 9f;
            const float d = 9f;
            const float h = 3f;
            const float front = -2.2f;
            StageBuilder.Box(parent, "Floor", new Vector3(0f, -0.05f, front + d * 0.5f), new Vector3(w, 0.1f, d), floor, 0.01f);
            StageBuilder.Box(parent, "Ceiling", new Vector3(0f, h + 0.05f, front + d * 0.5f), new Vector3(w, 0.1f, d), ceiling, 0.01f);
            StageBuilder.Box(parent, "WallFront", new Vector3(0f, h * 0.5f, front - 0.05f), new Vector3(w, h, 0.1f), wall, 0.01f);
            StageBuilder.Box(parent, "WallBack", new Vector3(0f, h * 0.5f, front + d + 0.05f), new Vector3(w, h, 0.1f), wall, 0.01f);
            StageBuilder.Box(parent, "WallLeft", new Vector3(-w * 0.5f - 0.05f, h * 0.5f, front + d * 0.5f), new Vector3(0.1f, h, d), wall, 0.01f);

            // 窓のある壁（+X）：腰壁、窓ガラス、窓枠
            StageBuilder.Box(parent, "WallRightLow", new Vector3(w * 0.5f + 0.05f, 0.45f, front + d * 0.5f), new Vector3(0.1f, 0.9f, d), wall, 0.01f);
            StageBuilder.Box(parent, "WallRightTop", new Vector3(w * 0.5f + 0.05f, h - 0.2f, front + d * 0.5f), new Vector3(0.1f, 0.4f, d), wall, 0.01f);
            StageBuilder.Box(parent, "Glass", new Vector3(w * 0.5f + 0.08f, 1.7f, front + d * 0.5f), new Vector3(0.02f, 1.6f, d), window, 0.002f);
            for (int i = 0; i <= 6; i++)
            {
                StageBuilder.Box(parent, "Mullion" + i, new Vector3(w * 0.5f, 1.7f, front + i * d / 6f), new Vector3(0.08f, 1.6f, 0.08f), frame, 0.01f);
            }

            StageBuilder.Box(parent, "Sill", new Vector3(w * 0.5f - 0.05f, 0.92f, front + d * 0.5f), new Vector3(0.2f, 0.04f, d), frame, 0.01f);

            // 前の壁：ホワイトボードと、書かれた線
            StageBuilder.Box(parent, "Board", new Vector3(0f, 1.6f, front + 0.03f), new Vector3(4.2f, 1.3f, 0.04f), board, 0.01f);
            StageBuilder.Box(parent, "BoardFrame", new Vector3(0f, 0.92f, front + 0.08f), new Vector3(4.3f, 0.06f, 0.12f), frame, 0.01f);
            for (int i = 0; i < 4; i++)
            {
                StageBuilder.Box(parent, "Writing" + i, new Vector3(-1.2f + (i % 2) * 0.3f, 2.0f - i * 0.22f, front + 0.055f), new Vector3(1.4f - i * 0.2f, 0.04f, 0.005f), ink, 0.002f);
            }

            // 後ろの壁：掲示板と時計
            StageBuilder.Box(parent, "Notice", new Vector3(-2f, 1.6f, front + d - 0.03f), new Vector3(2.4f, 1f, 0.04f), green, 0.01f);
            Transform clock = new GameObject("Clock").transform;
            clock.SetParent(parent, false);
            clock.localPosition = new Vector3(1.5f, 2.3f, front + d - 0.04f);
            StageBuilder.Column(clock, "Face", Vector3.zero, 32, 0.22f, 0.04f, board, 0.01f).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject hand = StageBuilder.Box(clock, "Second", new Vector3(0f, 0f, -0.03f), new Vector3(0.01f, 0.36f, 0.005f), steel, 0.002f);
            Spin(hand, new Vector3(0f, 0f, -6f), 0f);

            // 天井の明かり
            for (int row = 0; row < 3; row++)
            {
                foreach (float x in new[] { -2.2f, 0f, 2.2f })
                {
                    StageBuilder.Box(parent, "Light", new Vector3(x, h - 0.03f, front + 1.6f + row * 2.6f), new Vector3(1.2f, 0.04f, 0.25f), light, 0.005f);
                }
            }

            // 生徒の机といす（4列×4行）。先生の方（-Z）を向いている
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    Vector3 at = new Vector3(-2.7f + col * 1.8f, 0f, 1.6f + row * 1.4f);
                    StageBuilder.Box(parent, "Desk", at + new Vector3(0f, 0.72f, 0f), new Vector3(0.65f, 0.04f, 0.45f), desk, 0.01f);
                    StageBuilder.Box(parent, "DeskBody", at + new Vector3(0f, 0.6f, 0.02f), new Vector3(0.6f, 0.18f, 0.38f), steel, 0.01f);
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        StageBuilder.Box(parent, "DeskLeg", at + new Vector3(sx * 0.28f, 0.26f, 0f), new Vector3(0.03f, 0.52f, 0.35f), steel, 0.005f);
                    }

                    StageBuilder.Box(parent, "Seat", at + new Vector3(0f, 0.44f, 0.45f), new Vector3(0.4f, 0.04f, 0.38f), chair, 0.01f);
                    StageBuilder.Box(parent, "Back", at + new Vector3(0f, 0.7f, 0.64f), new Vector3(0.4f, 0.35f, 0.03f), chair, 0.01f);
                    StageBuilder.Box(parent, "ChairLeg", at + new Vector3(0f, 0.21f, 0.45f), new Vector3(0.36f, 0.42f, 0.03f), steel, 0.005f);
                }
            }

            // 教室の中は、天井や壁が影を落とさないようにする（暗くならないように）
            foreach (Renderer part in parent.GetComponentsInChildren<Renderer>())
            {
                part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static Camera EyeCamera(string name, Transform parent, float fieldOfView)
        {
            Camera camera = new GameObject(name).AddComponent<Camera>();
            camera.transform.SetParent(parent, false);
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 40f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Sky;
            camera.depth = -10f;
            return camera;
        }

        /// <summary>
        /// カメラの形（箱とレンズ）。大きさは size 倍。前は +Z
        /// </summary>
        private static void CameraIcon(Transform parent, float size, Material body, Material glow)
        {
            StageBuilder.Box(parent, "Body", new Vector3(0f, 0f, -0.3f * size), new Vector3(0.7f, 0.5f, 0.6f) * size, body, 0.06f * size);
            StageBuilder.Column(parent, "Lens", new Vector3(0f, 0f, 0.1f * size), 24, 0.18f * size, 0.25f * size, body, 0.03f * size).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            StageBuilder.Shape(parent, "LensRing", new Vector3(0f, 0f, 0.23f * size), MeshKit.Torus(0.18f * size, 0.03f * size, 32, 6), glow).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        /// <summary>
        /// 写る範囲を示す四角すい（光る細い棒。頂点が原点で +Z へ広がる）
        /// </summary>
        private static void Frustum(Transform parent, float width, float height, float length, float thickness, Material material)
        {
            float x = Mathf.Tan(width * 0.5f * Mathf.Deg2Rad) * length;
            float y = Mathf.Tan(height * 0.5f * Mathf.Deg2Rad) * length;
            var corners = new[] { new Vector3(-x, -y, length), new Vector3(x, -y, length), new Vector3(x, y, length), new Vector3(-x, y, length) };
            for (int i = 0; i < 4; i++)
            {
                Rod(parent, "Edge" + i, Vector3.zero, corners[i], thickness, material);
                Rod(parent, "Rim" + i, corners[i], corners[(i + 1) % 4], thickness, material);
            }
        }

        private static void Rod(Transform parent, string name, Vector3 from, Vector3 to, float thickness, Material material)
        {
            GameObject rod = StageBuilder.Box(parent, name, (from + to) * 0.5f, new Vector3(thickness, thickness, Vector3.Distance(from, to)), material, thickness * 0.3f);
            rod.transform.localRotation = Quaternion.LookRotation(to - from);
        }

        /// <summary>
        /// ゴーグルの大きな模型。目の側（+Z）を客席に向け、左右のレンズに画面を貼る
        /// </summary>
        private static void BigGoggle(Transform parent, float width, float height, Material metal, Material cyan, Material magenta,
            out Renderer leftLens, out Renderer rightLens, out Transform leftLabel, out Transform rightLabel)
        {
            Material shell = StageBuilder.Lit("Robot", new Color(0.86f, 0.89f, 0.95f), Color.black, 0.75f);
            Material cushion = StageBuilder.Lit("GoggleCushion", new Color(0.08f, 0.08f, 0.1f), Color.black, 0.2f);
            StageBuilder.Box(parent, "Shell", new Vector3(0f, 0f, -0.18f), new Vector3(width, height, 0.36f), shell, 0.12f);
            StageBuilder.Box(parent, "Cushion", new Vector3(0f, 0f, 0.02f), new Vector3(width - 0.06f, height - 0.06f, 0.06f), cushion, 0.03f);
            GameObject strap = StageBuilder.Shape(parent, "Strap", new Vector3(0f, 0f, -0.55f), MeshKit.Torus(0.62f, 0.035f, 64, 8), cushion);
            strap.transform.localScale = new Vector3(1.15f, 1f, 0.6f);
            StageBuilder.Shape(parent, "StrapLight", new Vector3(0f, 0.04f, -0.55f), MeshKit.Torus(0.62f, 0.012f, 64, 6), cyan).transform.localScale = new Vector3(1.16f, 1f, 0.61f);

            float lens = height * 0.78f;
            Renderer Lens(string name, float x)
            {
                GameObject quad = StageBuilder.Primitive(PrimitiveType.Quad, name, parent, StageBuilder.LoadOrCreate("GoggleLens", "Unlit/Texture"));
                quad.transform.localPosition = new Vector3(x, 0f, 0.06f);
                quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                quad.transform.localScale = new Vector3(lens, lens, 1f);
                foreach (float side in new[] { -1f, 1f })
                {
                    // 画面のふちの光る枠
                    StageBuilder.Box(parent, name + "RimV", new Vector3(x + side * (lens * 0.5f + 0.01f), 0f, 0.065f), new Vector3(0.02f, lens + 0.04f, 0.02f), magenta, 0.005f);
                    StageBuilder.Box(parent, name + "RimH", new Vector3(x, side * (lens * 0.5f + 0.01f), 0.065f), new Vector3(lens + 0.04f, 0.02f, 0.02f), magenta, 0.005f);
                }
                return quad.GetComponent<Renderer>();
            }

            float offset = width * 0.25f;
            // 客席から見て左（+X）が左目
            leftLens = Lens("LeftLens", offset);
            rightLens = Lens("RightLens", -offset);
            leftLabel = Text(parent, "LeftLabel", new Vector3(offset, -height * 0.5f - 0.12f, 0.06f), new Vector2(0.5f, 0.14f), 0.8f, TextAlignmentOptions.Center).transform;
            rightLabel = Text(parent, "RightLabel", new Vector3(-offset, -height * 0.5f - 0.12f, 0.06f), new Vector2(0.5f, 0.14f), 0.8f, TextAlignmentOptions.Center).transform;
            leftLabel.GetComponent<TMP_Text>().text = "左目";
            rightLabel.GetComponent<TMP_Text>().text = "右目";
        }

        /// <summary>
        /// アバターの顔に付ける小さなゴーグル（前は +Z）。最初は隠しておく
        /// </summary>
        private static Transform HeadGoggle(Transform parent)
        {
            Transform goggle = Template(parent, "HeadGoggle");
            Material shell = StageBuilder.Lit("Robot", new Color(0.86f, 0.89f, 0.95f), Color.black, 0.75f);
            StageBuilder.Box(goggle, "Shell", new Vector3(0f, 0f, 0.02f), new Vector3(0.2f, 0.1f, 0.09f), shell, 0.025f);
            StageBuilder.Box(goggle, "Front", new Vector3(0f, 0f, 0.067f), new Vector3(0.17f, 0.07f, 0.006f), StageBuilder.Lit("GoggleCushion", new Color(0.08f, 0.08f, 0.1f), Color.black, 0.2f), 0.002f);
            Material cyan = StageBuilder.Glow("GlowCyan", Cyan);
            foreach (float side in new[] { -1f, 1f })
            {
                // 前に付いたカメラ
                StageBuilder.Column(goggle, "Camera", new Vector3(side * 0.07f, -0.025f, 0.071f), 12, 0.012f, 0.006f, cyan, 0.002f).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            StageBuilder.Box(goggle, "Light", new Vector3(0f, 0.02f, 0.071f), new Vector3(0.1f, 0.008f, 0.004f), cyan, 0.002f);
            return goggle;
        }

        private static GameObject Dot(Transform parent, string name, Material material)
        {
            GameObject dot = StageBuilder.Primitive(PrimitiveType.Sphere, name, parent, material);
            dot.SetActive(false);
            return dot;
        }

        private static TMP_Text Text(Transform parent, string name, Vector3 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var text = new GameObject(name, typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            text.transform.SetParent(parent, false);
            text.transform.localPosition = position;
            // TextMeshPro の文字は自分の +Z の向きに見たときに読めるので、客席（+Z）から読めるよう裏返す
            text.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            text.rectTransform.sizeDelta = size;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static void Spin(GameObject target, Vector3 degreesPerSecond, float bob)
        {
            Spinner spinner = target.AddComponent<Spinner>();
            spinner.DegreesPerSecond = degreesPerSecond;
            spinner.Bob = bob;
        }

        /// <summary>
        /// 測った点を残すパーティクル（動かず、長く残る）
        /// </summary>
        private static ParticleSystem PointCloud(Transform parent, string name, float size, int max)
        {
            var system = new GameObject(name).AddComponent<ParticleSystem>();
            system.transform.SetParent(parent, false);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 600f;
            main.startSpeed = 0f;
            main.startSize = size;
            main.maxParticles = max;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            system.GetComponent<ParticleSystemRenderer>().sharedMaterial = PointMaterial();
            return system;
        }

        /// <summary>
        /// 光の線の素材（線の色は頂点の色で決める）
        /// </summary>
        private static Material BeamMaterial()
        {
            Material material = StageBuilder.LoadOrCreate("Beam", "Legacy Shaders/Particles/Additive");
            material.mainTexture = Texture2D.whiteTexture;
            NoSoftEdge(material);
            return material;
        }

        /// <summary>
        /// 測った点の素材（丸い光の粒）
        /// </summary>
        private static Material PointMaterial()
        {
            // 縁のくっきりした丸（中心ほど明るい）。ぼやけた光の粒より、測った「点」に見えるように
            const string path = "Assets/Guidance/Stage/PointDot.png";
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), Vector2.one * (size - 1) * 0.5f) / (size * 0.5f);
                    float v = Mathf.Clamp01((0.85f - d) / 0.08f) * Mathf.Lerp(1f, 0.75f, d);
                    texture.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            Material material = StageBuilder.LoadOrCreate("PointCloud", "Legacy Shaders/Particles/Additive");
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            NoSoftEdge(material);
            return material;
        }

        /// <summary>
        /// 光の粒の素材は、床や壁に近いほど薄くなる（ソフトパーティクル）。面に貼り付ける点や格子が消えないよう、薄くなる幅をごく小さくする
        /// </summary>
        private static void NoSoftEdge(Material material)
        {
            material.SetFloat("_InvFade", 200f);
        }

        /// <summary>
        /// 見つけた面を示す水色の格子の素材（加算合成なので、下の物が透けて見える）
        /// </summary>
        private static Material GridMaterial()
        {
            const string path = "Assets/Guidance/Stage/PlaneGrid.png";
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // ます目1つ分（0.5m）。ふちの線と、うすく塗った中
                    bool edge = x < 5 || y < 5 || x >= size - 5 || y >= size - 5;
                    float v = edge ? 1f : 0.22f;
                    texture.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }

            Material material = StageBuilder.LoadOrCreate("PlaneGrid", "Legacy Shaders/Particles/Additive");
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            NoSoftEdge(material);
            return material;
        }

        /// <summary>
        /// 測られる物（低い机と箱、いす）。当たり判定付き。場面に出ている間だけ置く
        /// </summary>
        private static GameObject Room(Transform parent, string name)
        {
            Transform room = Template(parent, name);
            Material white = StageBuilder.Lit("VrFurniture", new Color(0.75f, 0.77f, 0.82f), Color.black, 0.4f);
            Material wood = StageBuilder.Lit("VrWood", new Color(0.55f, 0.36f, 0.2f), Color.black, 0.3f);

            // 家具ごとにまとめる（空間アンカーの場面で、家具として見つけて箱で囲む単位）
            Transform Piece(string label)
            {
                var piece = new GameObject(label);
                piece.transform.SetParent(room, false);
                piece.AddComponent<Furniture>().Label = label;
                return piece.transform;
            }

            void Solid(Transform piece, string part, Vector3 position, Vector3 size, Material material)
            {
                GameObject box = StageBuilder.Box(piece, part, position, size, material);
                box.AddComponent<BoxCollider>().size = size;
            }

            // アバターの前、客席から見て左寄りに低い机と箱（客席からアバターが隠れないよう低くする）
            Vector3 desk = new Vector3(AvatarX + 1.0f, 0f, 1.0f);
            Transform deskPiece = Piece("机");
            Solid(deskPiece, "DeskTop", desk + new Vector3(0f, 0.42f, 0f), new Vector3(0.7f, 0.04f, 0.5f), wood);
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    Solid(deskPiece, "DeskLeg", desk + new Vector3(sx * 0.31f, 0.2f, sz * 0.21f), new Vector3(0.04f, 0.4f, 0.04f), wood);
                }
            }

            Solid(Piece("箱"), "Box", desk + new Vector3(0.12f, 0.565f, 0f), new Vector3(0.25f, 0.25f, 0.25f), white);
            // アバターの前、客席から見て右寄りにいす
            Vector3 chair = new Vector3(AvatarX - 0.9f, 0f, 1.0f);
            Transform chairPiece = Piece("いす");
            Solid(chairPiece, "Seat", chair + new Vector3(0f, 0.42f, 0f), new Vector3(0.4f, 0.05f, 0.4f), white);
            Solid(chairPiece, "SeatBase", chair + new Vector3(0f, 0.2f, 0f), new Vector3(0.26f, 0.4f, 0.26f), white);
            Solid(chairPiece, "Back", chair + new Vector3(0f, 0.68f, -0.18f), new Vector3(0.4f, 0.48f, 0.04f), white);
            return room.gameObject;
        }
    }
}
