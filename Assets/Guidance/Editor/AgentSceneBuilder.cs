using UnityEngine;

namespace Guidance.EditorTools
{
    /// <summary>
    /// コーディングエージェントの場面で使う、ロボット・バグ・弾・破片のひな形と鞭を作る。
    /// 形は面取りした箱や多角柱を組み合わせ、光る部分（目・胸のランプ・アンテナ）を足している。
    /// </summary>
    public static class AgentSceneBuilder
    {
        [GimmickBuilder]
        public static Gimmick Build(GimmickContext context)
        {
            Transform stage = context.Stage;
            Material particle = context.Particle;
            Transform root = new GameObject("AgentScene").transform;
            root.SetParent(stage, false);

            Material shell = StageBuilder.Lit("Robot", new Color(0.86f, 0.89f, 0.95f), Color.black, 0.75f);
            shell.SetFloat("_Metallic", 0.25f);
            Material dark = StageBuilder.Metal("RobotDark", new Color(0.13f, 0.15f, 0.2f), 0.8f, 0.6f);
            Material glow = StageBuilder.Glow("Marble", Color.white, 1.7f);
            Material cyan = StageBuilder.Glow("GlowCyan", new Color(0.1f, 0.85f, 1f));
            Material yellow = StageBuilder.Glow("GlowYellow", new Color(1f, 0.85f, 0.2f));
            Material bugShell = StageBuilder.Lit("BugShell", new Color(0.55f, 0.03f, 0.09f), new Color(0.35f, 0.01f, 0.04f), 0.85f);
            bugShell.SetFloat("_Metallic", 0.5f);
            Material red = StageBuilder.Glow("GlowRed", new Color(1f, 0.15f, 0.2f));

            // ロボット（正面は +Z）
            Transform agent = Template("AgentTemplate", root);
            StageBuilder.Box(agent, "Body", new Vector3(0f, 0.29f, 0f), new Vector3(0.3f, 0.3f, 0.24f), shell, 0.045f);
            StageBuilder.Box(agent, "Belt", new Vector3(0f, 0.16f, 0f), new Vector3(0.31f, 0.05f, 0.25f), dark, 0.012f);
            StageBuilder.Box(agent, "Chest", new Vector3(0f, 0.32f, 0.121f), new Vector3(0.2f, 0.13f, 0.012f), dark, 0.004f);
            for (int i = 0; i < 3; i++)
            {
                StageBuilder.Box(agent, "Lamp" + i, new Vector3((i - 1) * 0.055f, 0.34f, 0.129f), new Vector3(0.035f, 0.035f, 0.008f), i == 1 ? yellow : cyan, 0.003f);
            }

            StageBuilder.Box(agent, "Meter", new Vector3(0f, 0.285f, 0.129f), new Vector3(0.15f, 0.015f, 0.008f), cyan, 0.003f);
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject shoulder = StageBuilder.Primitive(PrimitiveType.Sphere, "Shoulder", agent, dark);
                shoulder.transform.localPosition = new Vector3(side * 0.175f, 0.39f, 0f);
                shoulder.transform.localScale = Vector3.one * 0.09f;
                StageBuilder.Box(agent, "Arm", new Vector3(side * 0.195f, 0.28f, 0f), new Vector3(0.06f, 0.2f, 0.08f), shell, 0.02f);
                GameObject hand = StageBuilder.Primitive(PrimitiveType.Sphere, "Hand", agent, dark);
                hand.transform.localPosition = new Vector3(side * 0.195f, 0.16f, 0f);
                hand.transform.localScale = Vector3.one * 0.075f;
                StageBuilder.Box(agent, "Leg", new Vector3(side * 0.08f, 0.09f, 0f), new Vector3(0.08f, 0.1f, 0.1f), dark, 0.02f);
                StageBuilder.Box(agent, "Foot", new Vector3(side * 0.08f, 0.025f, 0.02f), new Vector3(0.11f, 0.05f, 0.18f), shell, 0.018f);
            }

            // 頭は首の位置を軸にうなだれる
            Transform head = new GameObject("Head").transform;
            head.SetParent(agent, false);
            head.localPosition = new Vector3(0f, 0.45f, 0f);
            StageBuilder.Column(head, "Neck", new Vector3(0f, 0.005f, 0f), 12, 0.05f, 0.04f, dark, 0.005f);
            StageBuilder.Box(head, "Skull", new Vector3(0f, 0.14f, 0f), new Vector3(0.34f, 0.24f, 0.28f), shell, 0.06f);
            StageBuilder.Box(head, "Face", new Vector3(0f, 0.145f, 0.137f), new Vector3(0.28f, 0.13f, 0.012f), dark, 0.005f);
            StageBuilder.Box(head, "Visor", new Vector3(0f, 0.15f, 0.144f), new Vector3(0.24f, 0.07f, 0.008f), glow, 0.003f);
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject ear = StageBuilder.Column(head, "Ear", new Vector3(side * 0.18f, 0.14f, 0f), 12, 0.05f, 0.03f, dark, 0.008f);
                ear.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                GameObject earLight = StageBuilder.Column(head, "EarLight", new Vector3(side * 0.197f, 0.14f, 0f), 12, 0.025f, 0.008f, cyan, 0.002f);
                earLight.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            StageBuilder.Column(head, "Antenna", new Vector3(0f, 0.32f, 0f), 8, 0.01f, 0.12f, dark, 0.002f);
            GameObject tip = StageBuilder.Primitive(PrimitiveType.Sphere, "AntennaTip", head, yellow);
            tip.transform.localPosition = new Vector3(0f, 0.39f, 0f);
            tip.transform.localScale = Vector3.one * 0.04f;

            // バグ（正面は +Z）。最後の子は実行時に付ける名札
            Transform bug = Template("BugTemplate", root);
            GameObject abdomen = StageBuilder.Primitive(PrimitiveType.Sphere, "Abdomen", bug, bugShell);
            abdomen.transform.localPosition = new Vector3(0f, 0.17f, -0.04f);
            abdomen.transform.localScale = new Vector3(0.26f, 0.2f, 0.3f);
            GameObject bugHead = StageBuilder.Primitive(PrimitiveType.Sphere, "Head", bug, bugShell);
            bugHead.transform.localPosition = new Vector3(0f, 0.15f, 0.14f);
            bugHead.transform.localScale = new Vector3(0.16f, 0.13f, 0.14f);
            for (int i = 0; i < 3; i++)
            {
                // 背中の光る筋と、とげ
                StageBuilder.Box(bug, "Stripe" + i, new Vector3(0f, 0.262f - Mathf.Abs(i - 1) * 0.012f, -0.04f + (i - 1) * 0.075f), new Vector3(0.17f - Mathf.Abs(i - 1) * 0.03f, 0.012f, 0.02f), red, 0.004f);
                StageBuilder.Column(bug, "Spike" + i, new Vector3(0f, 0.31f - Mathf.Abs(i - 1) * 0.02f, -0.0025f + (i - 1) * 0.075f), 5, 0.022f, 0.08f, bugShell, 0.002f, 0.1f);
            }

            foreach (float side in new[] { -1f, 1f })
            {
                GameObject eye = StageBuilder.Primitive(PrimitiveType.Sphere, "Eye", bug, yellow);
                eye.transform.localPosition = new Vector3(side * 0.05f, 0.18f, 0.19f);
                eye.transform.localScale = Vector3.one * 0.045f;
                GameObject fang = StageBuilder.Column(bug, "Fang", new Vector3(side * 0.035f, 0.11f, 0.215f), 5, 0.014f, 0.07f, dark, 0.002f, 0.1f);
                fang.transform.localRotation = Quaternion.Euler(115f, side * -20f, 0f);
                for (int i = 0; i < 3; i++)
                {
                    // 脚は「外へ上がる部分」と「地面へ下りる部分」の2節
                    float z = -0.1f + i * 0.09f;
                    float splay = (i - 1) * 20f;
                    GameObject upper = StageBuilder.Box(bug, "LegUpper", new Vector3(side * 0.17f, 0.17f, z), new Vector3(0.16f, 0.022f, 0.022f), dark, 0.006f);
                    upper.transform.localRotation = Quaternion.Euler(0f, side * -splay, side * 28f);
                    GameObject lower = StageBuilder.Box(bug, "LegLower", new Vector3(side * 0.265f, 0.1f, z + Mathf.Sin(splay * Mathf.Deg2Rad) * 0.05f), new Vector3(0.022f, 0.2f, 0.022f), dark, 0.006f);
                    lower.transform.localRotation = Quaternion.Euler(0f, 0f, side * 14f);
                }
            }

            // 弾
            Transform bolt = Template("BoltTemplate", root);
            StageBuilder.Primitive(PrimitiveType.Sphere, "Core", bolt, glow).transform.localScale = Vector3.one * 0.08f;
            TrailRenderer trail = bolt.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.15f;
            trail.startWidth = 0.08f;
            trail.endWidth = 0f;
            trail.sharedMaterial = particle;

            // 壊れたバグの破片
            Transform debris = Template("DebrisTemplate", root);
            StageBuilder.Box(debris, "Chip", Vector3.zero, Vector3.one * 0.07f, red, 0.015f);
            debris.gameObject.AddComponent<BoxCollider>().size = Vector3.one * 0.07f;
            debris.gameObject.AddComponent<Rigidbody>().mass = 0.02f;

            // 鞭
            var whip = new GameObject("Whip").AddComponent<LineRenderer>();
            whip.transform.SetParent(root, false);
            whip.useWorldSpace = true;
            whip.sharedMaterial = StageBuilder.Glow("GlowWhip", new Color(1f, 0.6f, 0.15f));
            whip.widthCurve = new AnimationCurve(new Keyframe(0f, 0.05f), new Keyframe(0.15f, 0.04f), new Keyframe(1f, 0.006f));
            whip.numCornerVertices = 3;
            whip.numCapVertices = 3;
            whip.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            AgentScene scene = root.gameObject.AddComponent<AgentScene>();
            scene.AgentTemplate = agent.gameObject;
            scene.BugTemplate = bug.gameObject;
            scene.BoltTemplate = bolt.gameObject;
            scene.DebrisTemplate = debris.gameObject;
            scene.Whip = whip;
            scene.Id = "agents";
            scene.Sparks = context.Sparks;
            scene.Avatar = context.Avatar;
            return scene;
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
