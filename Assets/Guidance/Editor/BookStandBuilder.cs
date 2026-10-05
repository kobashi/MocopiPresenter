using UnityEngine;

namespace Guidance.EditorTools
{
    /// <summary>
    /// 回転式スタンドの土台・回る部分・面のひな形と、各項目のアイコン（面取りした箱や多角柱を組み合わせた小さな模型）を作る。
    /// </summary>
    public static class BookStandBuilder
    {
        private static readonly Color Cyan = new Color(0.1f, 0.85f, 1f);

        [GimmickBuilder]
        public static Gimmick Build(GimmickContext context)
        {
            Transform stage = context.Stage;
            Transform root = new GameObject("BookStand").transform;
            root.SetParent(stage, false);
            // アバターが半歩寄って手を伸ばせば届く位置（腕を広げて立っただけでは触れない距離）
            root.localPosition = new Vector3(-0.05f, 0f, 0.25f);

            Material shell = StageBuilder.Lit("Robot", new Color(0.86f, 0.89f, 0.95f), Color.black, 0.75f);
            Material dark = StageBuilder.Metal("RobotDark", new Color(0.13f, 0.15f, 0.2f), 0.8f, 0.6f);
            Material metal = StageBuilder.Metal("Metal", new Color(0.16f, 0.18f, 0.24f), 0.85f, 0.6f);
            Material cyan = StageBuilder.Glow("GlowCyan", Cyan);
            Material magenta = StageBuilder.Glow("GlowMagenta", new Color(1f, 0.25f, 0.7f));
            Material yellow = StageBuilder.Glow("GlowYellow", new Color(1f, 0.85f, 0.2f));
            Material screen = StageBuilder.Glow("IconScreen", new Color(0.1f, 0.45f, 0.6f), 1.2f);

            // 土台と支柱
            StageBuilder.Column(root, "Base", new Vector3(0f, 0.03f, 0f), 40, 0.36f, 0.06f, metal, 0.02f);
            StageBuilder.Shape(root, "BaseLight", new Vector3(0f, 0.062f, 0f), MeshKit.Torus(0.3f, 0.008f, 64, 6), cyan);
            StageBuilder.Column(root, "BaseStep", new Vector3(0f, 0.09f, 0f), 12, 0.09f, 0.06f, metal, 0.015f);
            StageBuilder.Column(root, "Pole", new Vector3(0f, 0.49f, 0f), 12, 0.035f, 0.76f, metal, 0.005f);
            StageBuilder.Column(root, "Bearing", new Vector3(0f, 0.85f, 0f), 12, 0.08f, 0.05f, metal, 0.015f);

            // 回る部分
            var drum = new GameObject("Drum").AddComponent<Rigidbody>();
            drum.transform.SetParent(root, false);
            drum.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            drum.isKinematic = true;
            foreach (float side in new[] { -1f, 1f })
            {
                StageBuilder.Column(drum.transform, "Cap", new Vector3(0f, side * 0.425f, 0f), 40, 0.5f, 0.04f, metal, 0.012f);
                StageBuilder.Shape(drum.transform, "CapLight", new Vector3(0f, side * 0.425f, 0f), MeshKit.Torus(0.5f, 0.01f, 64, 6), cyan);
            }

            StageBuilder.Column(drum.transform, "Finial", new Vector3(0f, 0.5f, 0f), 6, 0.06f, 0.12f, cyan, 0.003f, 0.15f);

            // 面のひな形（外側は +Z）。最初の子が板
            Transform panel = Template("PanelTemplate", root);
            StageBuilder.Box(panel, "Board", Vector3.zero, new Vector3(0.55f, 0.8f, 0.03f), StageBuilder.Lit("StandPanel", new Color(0.06f, 0.07f, 0.12f), Cyan * 0.04f, 0.6f), 0.012f);
            foreach (float side in new[] { -1f, 1f })
            {
                // 面のふちの光る枠
                StageBuilder.Box(panel, "EdgeV", new Vector3(side * 0.255f, 0f, 0.016f), new Vector3(0.012f, 0.74f, 0.008f), cyan, 0.003f);
                StageBuilder.Box(panel, "EdgeH", new Vector3(0f, side * 0.375f, 0.016f), new Vector3(0.52f, 0.012f, 0.008f), cyan, 0.003f);
            }

            // 手が触れたことを知るための範囲（板より少し厚くして、素早く払っても拾えるようにする）
            BoxCollider touch = panel.gameObject.AddComponent<BoxCollider>();
            touch.size = new Vector3(0.55f, 0.8f, 0.14f);
            touch.isTrigger = true;

            Quaternion toViewer = Quaternion.Euler(90f, 0f, 0f);

            // センサー：丸い本体と、体に巻くバンド
            Transform sensor = Template("sensor", root);
            StageBuilder.Box(sensor, "Band", new Vector3(0f, 0f, -0.005f), new Vector3(0.42f, 0.075f, 0.014f), dark, 0.005f);
            StageBuilder.Column(sensor, "Body", new Vector3(0f, 0f, 0.012f), 32, 0.11f, 0.045f, shell, 0.015f).transform.localRotation = toViewer;
            StageBuilder.Shape(sensor, "Ring", new Vector3(0f, 0f, 0.036f), MeshKit.Torus(0.06f, 0.006f, 32, 6), cyan).transform.localRotation = toViewer;
            StageBuilder.Column(sensor, "Light", new Vector3(0f, 0f, 0.036f), 16, 0.022f, 0.006f, cyan, 0.002f).transform.localRotation = toViewer;

            // レシーバー：USB に挿す小さな受信機と、出ている電波
            Transform receiver = Template("receiver", root);
            StageBuilder.Box(receiver, "Body", new Vector3(-0.05f, 0.03f, 0f), new Vector3(0.14f, 0.24f, 0.045f), shell, 0.02f);
            StageBuilder.Box(receiver, "Plug", new Vector3(-0.05f, -0.125f, 0f), new Vector3(0.085f, 0.075f, 0.028f), dark, 0.006f);
            StageBuilder.Column(receiver, "Light", new Vector3(-0.05f, 0.09f, 0.024f), 12, 0.018f, 0.006f, cyan, 0.002f).transform.localRotation = toViewer;
            for (int i = 0; i < 3; i++)
            {
                StageBuilder.Box(receiver, "Wave" + i, new Vector3(0.07f + i * 0.045f, 0.08f, 0f), new Vector3(0.016f, 0.07f + i * 0.06f, 0.012f), cyan, 0.004f);
            }

            // PC：モニターと台
            Transform pc = Template("pc", root);
            StageBuilder.Box(pc, "Monitor", new Vector3(0f, 0.04f, 0f), new Vector3(0.4f, 0.26f, 0.03f), dark, 0.012f);
            StageBuilder.Box(pc, "Screen", new Vector3(0f, 0.045f, 0.016f), new Vector3(0.35f, 0.2f, 0.006f), screen, 0.002f);
            for (int i = 0; i < 3; i++)
            {
                // 画面に映っているコードの行
                StageBuilder.Box(pc, "Code" + i, new Vector3(-0.06f + i * 0.02f, 0.1f - i * 0.05f, 0.021f), new Vector3(0.18f + (i % 2) * 0.06f, 0.014f, 0.003f), cyan, 0.001f);
            }

            StageBuilder.Box(pc, "Neck", new Vector3(0f, -0.115f, 0f), new Vector3(0.045f, 0.07f, 0.025f), dark, 0.006f);
            StageBuilder.Box(pc, "Foot", new Vector3(0f, -0.158f, 0f), new Vector3(0.2f, 0.018f, 0.09f), dark, 0.006f);

            // 3Dアバター：人の形
            Transform avatar = Template("avatar", root);
            GameObject head = StageBuilder.Primitive(PrimitiveType.Sphere, "Head", avatar, magenta);
            head.transform.localPosition = new Vector3(0f, 0.135f, 0f);
            head.transform.localScale = Vector3.one * 0.1f;
            StageBuilder.Box(avatar, "Body", new Vector3(0f, 0.02f, 0f), new Vector3(0.11f, 0.13f, 0.055f), magenta, 0.02f);
            StageBuilder.Box(avatar, "Arms", new Vector3(0f, 0.06f, 0f), new Vector3(0.34f, 0.034f, 0.04f), magenta, 0.012f);
            foreach (float side in new[] { -1f, 1f })
            {
                StageBuilder.Box(avatar, "Leg", new Vector3(side * 0.03f, -0.12f, 0f), new Vector3(0.04f, 0.16f, 0.04f), magenta, 0.012f);
                // 体に付いたセンサーの位置を示す点
                GameObject dot = StageBuilder.Primitive(PrimitiveType.Sphere, "Dot", avatar, cyan);
                dot.transform.localPosition = new Vector3(side * 0.15f, 0.06f, 0.025f);
                dot.transform.localScale = Vector3.one * 0.03f;
            }

            // プロジェクター：本体とレンズと光
            Transform projector = Template("projector", root);
            StageBuilder.Box(projector, "Body", new Vector3(-0.05f, 0f, 0f), new Vector3(0.26f, 0.12f, 0.1f), shell, 0.025f);
            StageBuilder.Column(projector, "LensRim", new Vector3(-0.11f, 0f, 0.05f), 24, 0.045f, 0.03f, dark, 0.006f).transform.localRotation = toViewer;
            StageBuilder.Column(projector, "Lens", new Vector3(-0.11f, 0f, 0.064f), 24, 0.032f, 0.008f, yellow, 0.002f).transform.localRotation = toViewer;
            foreach (float side in new[] { -1f, 1f })
            {
                StageBuilder.Column(projector, "Foot", new Vector3(-0.05f + side * 0.09f, -0.07f, 0f), 8, 0.015f, 0.03f, dark, 0.003f);
            }

            for (int i = 0; i < 3; i++)
            {
                GameObject ray = StageBuilder.Box(projector, "Ray" + i, new Vector3(0.16f, (i - 1) * 0.075f, 0f), new Vector3(0.15f, 0.014f, 0.012f), yellow, 0.004f);
                ray.transform.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * 25f);
            }

            BookStand stand = root.gameObject.AddComponent<BookStand>();
            stand.Drum = drum;
            stand.PanelTemplate = panel.gameObject;
            stand.Icons = new[] { sensor.gameObject, receiver.gameObject, pc.gameObject, avatar.gameObject, projector.gameObject };
            stand.Id = "stand";
            stand.Sparks = context.Sparks;
            return stand;
        }

        private static Transform Template(string name, Transform parent)
        {
            var template = new GameObject(name);
            template.transform.SetParent(parent, false);
            template.SetActive(false);
            return template.transform;
        }
    }
}
