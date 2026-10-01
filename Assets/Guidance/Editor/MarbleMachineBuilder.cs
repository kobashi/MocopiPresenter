using UnityEditor;
using UnityEngine;

namespace Guidance.EditorTools
{
    /// <summary>
    /// マーブルマシンのコースを組み立てる。座標は機械の左下を原点に、客席から見て右が +X、上が +Y（単位はメートル）。
    /// 玉は奥行き方向には動かないようにしてあるので、コースは1枚の板の上に描くように作れる。
    /// </summary>
    public static class MarbleMachineBuilder
    {
        private const float Depth = 0.16f;
        private static readonly Color Cyan = new Color(0.1f, 0.85f, 1f);
        private static readonly Color Magenta = new Color(1f, 0.25f, 0.7f);
        private static readonly Color Yellow = new Color(1f, 0.85f, 0.2f);

        public static MarbleMachine Build(Transform stage, ParticleSystem sparks, Animator avatar, Material particle)
        {
            Transform root = new GameObject("MarbleMachine").transform;
            root.SetParent(stage, false);
            // 180°回して、機械の +X が客席から見て右になるようにする（+Z は客席から見て奥）
            root.SetPositionAndRotation(new Vector3(2.3f, 0f, -0.8f), Quaternion.Euler(0f, 180f, 0f));

            Material rail = StageBuilder.Glow("GlowCyan", Cyan);
            Material wall = StageBuilder.Glow("GlowMagenta", Magenta);
            Material moving = StageBuilder.Glow("GlowYellow", Yellow);
            Material metal = StageBuilder.Metal("Metal", new Color(0.16f, 0.18f, 0.24f), 0.85f, 0.6f);

            // 骨組み（見た目だけ。コースの奥に立つ2本の柱と、上下の梁、レールを支える腕）
            Transform frame = new GameObject("Frame").transform;
            frame.SetParent(root, false);
            StageBuilder.Box(frame, "Plinth", new Vector3(1.17f, 0.04f, 0.13f), new Vector3(2.8f, 0.08f, 0.34f), metal, 0.025f);
            StageBuilder.Box(frame, "TopBeam", new Vector3(1.17f, 2.46f, 0.15f), new Vector3(2.7f, 0.07f, 0.07f), metal, 0.02f);
            foreach (float x in new[] { -0.1f, 2.45f })
            {
                StageBuilder.Column(frame, "Post", new Vector3(x, 1.27f, 0.15f), 8, 0.045f, 2.38f, metal, 0.01f);
                StageBuilder.Column(frame, "PostFoot", new Vector3(x, 0.11f, 0.15f), 8, 0.09f, 0.06f, metal, 0.015f);
            }

            foreach (Vector2 arm in new[]
            {
                new Vector2(0.5f, 1.93f), new Vector2(1.8f, 1.77f), new Vector2(1.7f, 1.35f), new Vector2(0.85f, 1.24f),
                new Vector2(0.5f, 0.71f), new Vector2(1.75f, 0.57f), new Vector2(1.9f, 0.29f), new Vector2(0.6f, 0.16f),
            })
            {
                StageBuilder.Box(frame, "Arm", new Vector3(arm.x, arm.y - 0.03f, 0.115f), new Vector3(0.03f, 0.03f, 0.1f), metal, 0.008f);
            }

            // リフトの縦穴
            Segment(root, "ShaftLeft", new Vector2(-0.01f, 0f), new Vector2(-0.01f, 2.35f), 0.02f, wall, true);
            Segment(root, "ShaftRight", new Vector2(0.23f, 0.36f), new Vector2(0.23f, 1.93f), 0.02f, wall, true);

            // レール（上から順に、右へ・左へ・右へ・左へと折り返す）
            Segment(root, "RampA", new Vector2(0.245f, 1.98f), new Vector2(2.05f, 1.76f), 0.03f, rail, true);
            Segment(root, "RampB", new Vector2(1.97f, 1.40f), new Vector2(0.62f, 1.23f), 0.03f, rail, true);
            Segment(root, "RampC", new Vector2(0.245f, 0.76f), new Vector2(2.0f, 0.56f), 0.03f, rail, true);
            Segment(root, "RampD", new Vector2(2.28f, 0.34f), new Vector2(0.225f, 0.145f), 0.03f, rail, true);
            Segment(root, "RightWall", new Vector2(2.28f, 0.12f), new Vector2(2.28f, 0.85f), 0.02f, wall, true);

            // 欠けている部品：一番上のレールの右端で玉を受け止めて、下のレールへ折り返させる
            Transform part = new GameObject("Part").transform;
            part.SetParent(root, false);
            part.localPosition = new Vector3(2.2f, 1.6f, 0f);
            Transform ghost = new GameObject("Ghost").transform;
            ghost.SetParent(root, false);
            ghost.localPosition = part.localPosition;
            Material ghostMaterial = StageBuilder.Glow("GlowGhost", new Color(0.25f, 0.22f, 0.08f), 1f);
            foreach ((Transform parent, Material material, bool solid) in new[] { (part, moving, true), (ghost, ghostMaterial, false) })
            {
                Vector2 origin = part.localPosition;
                Segment(parent, "Wall", new Vector2(2.42f, 1.30f) - origin, new Vector2(2.42f, 1.95f) - origin, 0.03f, material, solid);
                Segment(parent, "Floor", new Vector2(2.42f, 1.47f) - origin, new Vector2(1.93f, 1.415f) - origin, 0.03f, material, solid);
            }

            // 風車：落ちてきた玉が当たると回る
            var windmill = new GameObject("Windmill").AddComponent<Rigidbody>();
            windmill.transform.SetParent(root, false);
            windmill.transform.localPosition = new Vector3(0.43f, 1.0f, 0f);
            windmill.mass = 0.03f;
            windmill.angularDamping = 0.2f;
            windmill.useGravity = false;
            windmill.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            Segment(windmill.transform, "BladeA", new Vector2(-0.15f, 0f), new Vector2(0.15f, 0f), 0.02f, moving, true);
            Segment(windmill.transform, "BladeB", new Vector2(0f, -0.15f), new Vector2(0f, 0.15f), 0.02f, moving, true);
            GameObject hub = StageBuilder.Column(windmill.transform, "Hub", Vector3.zero, 12, 0.035f, 0.2f, metal, 0.008f);
            hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // リフトの受け皿と、その下で入口をふさぐ板
            Rigidbody tray = Kinematic(Segment(root, "Tray", new Vector2(0.01f, 0.14f), new Vector2(0.21f, 0.14f), 0.02f, moving, true));
            Rigidbody skirt = Kinematic(Segment(root, "Skirt", new Vector2(0.215f, -1.89f), new Vector2(0.215f, 0.11f), 0.02f, moving, true));

            // 玉のひな形
            var bouncy = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Guidance/Stage/Marble.physicMaterial");
            if (bouncy == null)
            {
                bouncy = new PhysicsMaterial("Marble");
                AssetDatabase.CreateAsset(bouncy, "Assets/Guidance/Stage/Marble.physicMaterial");
            }

            bouncy.bounciness = 0.3f;
            bouncy.dynamicFriction = 0.3f;
            bouncy.staticFriction = 0.3f;
            EditorUtility.SetDirty(bouncy);

            GameObject marble = StageBuilder.Primitive(PrimitiveType.Sphere, "MarbleTemplate", root, StageBuilder.Glow("Marble", Color.white, 1.7f));
            marble.transform.localScale = Vector3.one * 0.09f;
            marble.AddComponent<SphereCollider>().sharedMaterial = bouncy;
            Rigidbody body = marble.AddComponent<Rigidbody>();
            body.mass = 0.05f;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            // 奥行き方向には動かさない（コースから手前や奥へ落ちない）
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            marble.AddComponent<MarbleSound>();
            TrailRenderer trail = marble.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.startWidth = 0.07f;
            trail.endWidth = 0f;
            trail.sharedMaterial = particle;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            marble.SetActive(false);

            MarbleMachine machine = root.gameObject.AddComponent<MarbleMachine>();
            machine.Tray = tray;
            machine.Skirt = skirt;
            machine.Part = part;
            machine.Ghost = ghost.gameObject;
            machine.MarbleTemplate = marble;
            machine.Sparks = sparks;
            machine.Avatar = avatar;
            return machine;
        }

        /// <summary>
        /// 2点を結ぶ細い板（角は面取り）。レールにも壁にも使う。
        /// </summary>
        private static GameObject Segment(Transform parent, string name, Vector2 from, Vector2 to, float thickness, Material material, bool solid)
        {
            Vector2 direction = to - from;
            GameObject segment = StageBuilder.Box(parent, name, (from + to) * 0.5f, new Vector3(direction.magnitude, thickness, Depth), material, thickness * 0.3f);
            segment.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            if (solid)
            {
                // 当たり判定は、形の大きさに合わせた箱になる
                segment.AddComponent<BoxCollider>();
            }

            return segment;
        }

        private static Rigidbody Kinematic(GameObject target)
        {
            Rigidbody body = target.AddComponent<Rigidbody>();
            body.isKinematic = true;
            return body;
        }
    }
}
