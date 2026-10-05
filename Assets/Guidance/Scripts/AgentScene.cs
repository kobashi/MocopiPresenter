using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// コーディングエージェント（小さなロボット）を働かせて、押し寄せるバグやトラブルを片付ける場面。
    /// エージェントは放っておくと居眠りする。アバターが右手の鞭を振って当てる（または鋭く振って鳴らす）と
    /// しばらく働き、近くのバグを撃って壊す。W キーでも全員を働かせられる（リハーサルと保険用）。
    /// </summary>
    public sealed class AgentScene : Gimmick
    {
        /// <summary>
        /// 場面の JSON から読む設定
        /// </summary>
        [System.Serializable]
        private sealed class Settings
        {
            // エージェント（ロボット）の数
            public int agents = 4;
            // 押し寄せるトラブルの名前
            public string[] troubles = new string[0];
            // 鞭が鳴ったとみなす先端の速さ（m/秒）。0 なら既定値
            public float crackSpeed;
        }

        private sealed class Agent
        {
            public Transform Root;
            public Transform Head;
            public Renderer Visor;
            public TextMeshPro Label;
            public Color Color;
            public float WorkUntil;
            public float NextShot;
            public float Jump;
            public bool WasWorking;
        }

        private sealed class Bug
        {
            public Transform Root;
            public Vector3 Offset;
            public float Phase;
            public bool Targeted;
        }

        private sealed class Bolt
        {
            public Transform Root;
            public Bug Target;
            public Color Color;
        }

        public GameObject AgentTemplate;
        public GameObject BugTemplate;
        public GameObject BoltTemplate;
        public GameObject DebrisTemplate;
        public LineRenderer Whip;
        public ParticleSystem Sparks;
        public Animator Avatar;
        // 鞭を持つ手の代わりに使う位置（動作確認用。空ならアバターの右手）
        public Transform HandOverride;
        public TMP_Text Counter;

        public float WorkSeconds = 8f;
        public float WhipLength = 2.4f;
        // 鞭の先がこの速さ（m/秒）を超えると「鳴った」とみなし、全員が働き出す
        public float CrackSpeed = 8f;
        public float BugSpeed = 0.6f;
        public float SpawnInterval = 0.9f;
        public int MaxBugs = 30;
        // バグが湧く場所と、エージェントが並ぶ場所（奥から手前へ、斜めに並ぶ）
        public Vector3 SpawnCenter = new Vector3(4.8f, 0f, -0.4f);
        public Vector3 AgentLine = new Vector3(2.1f, 0f, -1.1f);
        public Vector3 AgentSpacing = new Vector3(-0.7f, 0f, 0.5f);
        public Color[] Colors =
        {
            new Color(0.1f, 0.85f, 1f),
            new Color(1f, 0.85f, 0.2f),
            new Color(0.45f, 1f, 0.5f),
            new Color(1f, 0.55f, 0.2f),
        };

        public int Killed { get; private set; }

        public int Cracks { get; private set; }

        /// <summary>
        /// これまでに出た鞭の先の最高速（m/秒）。CrackSpeed を調整するときの目安
        /// </summary>
        public float MaxTipSpeed { get; set; }

        public int BugCount => this.bugs.Count;

        private const int RopePoints = 26;
        private const int HandlePoints = 3;
        private static readonly Quaternion FaceCamera = Quaternion.Euler(0f, 180f, 0f);

        private readonly List<Agent> agents = new List<Agent>();
        private readonly List<Bug> bugs = new List<Bug>();
        private readonly List<Bolt> bolts = new List<Bolt>();
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly Vector3[] rope = new Vector3[RopePoints];
        private readonly Vector3[] ropeBefore = new Vector3[RopePoints];
        private string[] troubles = new string[0];
        private float nextSpawn;
        private float lastCrack;
        private float ropeTime;

        /// <summary>
        /// 場面の切り替えで呼ばれる。count が 0 なら片付けて隠す。
        /// </summary>
        protected override void OnEnter(string json, SlideDeck deck)
        {
            Settings settings = Read<Settings>(json);
            // 撃破数はスクリーンの本文の欄に出す
            this.Counter = deck.Body;
            this.Set(settings.agents, settings.troubles, settings.crackSpeed);
        }

        protected override void OnExit(SlideDeck deck)
        {
            this.Set(0, null, 0f);
        }

        /// <summary>
        /// ロボットを count 体並べ直し、バグや弾を片付ける。count が 0 なら片付けるだけ。
        /// </summary>
        public void Set(int count, string[] newTroubles, float crackSpeed)
        {
            if (crackSpeed > 0f)
            {
                this.CrackSpeed = crackSpeed;
            }

            if (!Application.isPlaying)
            {
                return;
            }

            foreach (GameObject item in this.spawned)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }

            this.spawned.Clear();
            this.agents.Clear();
            this.bugs.Clear();
            this.bolts.Clear();
            this.Killed = 0;
            this.Cracks = 0;
            this.MaxTipSpeed = 0f;
            this.troubles = newTroubles != null && newTroubles.Length > 0 ? newTroubles : new[] { "バグ" };

            for (int i = 0; i < count; i++)
            {
                GameObject instance = this.Spawn(this.AgentTemplate, this.AgentLine + this.AgentSpacing * i);
                var agent = new Agent
                {
                    Root = instance.transform,
                    Head = instance.transform.Find("Head"),
                    Color = this.Colors[i % this.Colors.Length],
                };
                agent.Visor = agent.Head.Find("Visor").GetComponent<Renderer>();
                agent.Label = this.Label(instance.transform, 0.95f, 1.6f, agent.Color);
                this.agents.Add(agent);
            }

            // 柄は手の向きに、その先は真下へ、床に届いた先は床の上に寝かせておく（最初に暴れないように）
            float segment = this.WhipLength / (RopePoints - 1);
            Vector3 point = this.Hand();
            for (int i = 0; i < RopePoints; i++)
            {
                if (i > 0)
                {
                    point += (i < HandlePoints ? this.HandDirection() : point.y > 0.02f + segment ? Vector3.down : Vector3.right) * segment;
                }

                this.rope[i] = this.ropeBefore[i] = point;
            }

            this.lastCrack = Time.time;
            this.ropeTime = 0f;
        }

        /// <summary>
        /// 全員を働かせる（W キーと同じ）。
        /// </summary>
        public void CrackAll()
        {
            this.Cracks++;
            Sfx.Play("whip");
            foreach (Agent agent in this.agents)
            {
                this.Motivate(agent);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                this.CrackAll();
            }

            float dt = Mathf.Min(Time.deltaTime, 0.033f);
            if (dt <= 0f)
            {
                return;
            }

            this.UpdateWhip(dt);
            this.UpdateBugs(dt);
            this.UpdateAgents(dt);
            this.UpdateBolts(dt);

            if (this.Counter != null)
            {
                this.Counter.text = "撃破 " + this.Killed + "　　残り " + this.bugs.Count;
            }
        }

        /// <summary>
        /// 鞭は、点を糸でつないだ簡単な計算（ベルレ法）で動かす。根元は手に固定し、残りは重力と勢いで振られる。
        /// 画面の更新間隔に左右されないよう、1/120 秒きざみで進める。
        /// </summary>
        private void UpdateWhip(float dt)
        {
            const float step = 1f / 120f;
            Vector3 handNow = this.Hand();
            this.ropeTime += dt;
            int steps = Mathf.FloorToInt(this.ropeTime / step);
            this.ropeTime -= steps * step;
            for (int n = 1; n <= steps; n++)
            {
                this.StepWhip(Vector3.Lerp(this.rope[0], handNow, (float)n / steps), step);
            }

            this.Whip.positionCount = RopePoints;
            this.Whip.SetPositions(this.rope);
        }

        private void StepWhip(Vector3 hand, float step)
        {
            float segment = this.WhipLength / (RopePoints - 1);
            for (int i = HandlePoints; i < RopePoints; i++)
            {
                Vector3 velocity = (this.rope[i] - this.ropeBefore[i]) * 0.997f;
                this.ropeBefore[i] = this.rope[i];
                this.rope[i] += velocity + Physics.gravity * (0.6f * step * step);
            }

            // 柄：手から腕の向きにまっすぐ伸びる固い部分。手首を返すと、てこのように先が大きく振られる
            Vector3 direction = this.HandDirection();
            for (int i = 0; i < HandlePoints; i++)
            {
                this.ropeBefore[i] = this.rope[i];
                this.rope[i] = hand + direction * segment * i;
            }

            for (int pass = 0; pass < 12; pass++)
            {
                for (int i = HandlePoints; i < RopePoints; i++)
                {
                    // 先へ行くほど軽い。重い根元の動きが軽い先端に伝わるほど速くなり、先端が走る
                    float heavy = i - 1 < HandlePoints ? 0f : Lightness(i - 1);
                    float light = Lightness(i);
                    Vector3 delta = this.rope[i] - this.rope[i - 1];
                    Vector3 fix = delta.normalized * (delta.magnitude - segment);
                    this.rope[i - 1] += fix * (heavy / (heavy + light));
                    this.rope[i] -= fix * (light / (heavy + light));
                }
            }

            for (int i = HandlePoints; i < RopePoints; i++)
            {
                this.rope[i].y = Mathf.Max(this.rope[i].y, 0.02f);
            }

            // 先端が鋭く振られたら「鳴った」とみなし、全員が働き出す
            Vector3 tip = this.rope[RopePoints - 1];
            float tipSpeed = (tip - this.ropeBefore[RopePoints - 1]).magnitude / step;
            this.MaxTipSpeed = Mathf.Max(this.MaxTipSpeed, tipSpeed);
            if (tipSpeed > this.CrackSpeed && Time.time - this.lastCrack > 0.4f)
            {
                this.lastCrack = Time.time;
                this.Cracks++;
                this.Burst(tip, Color.white, 50);
                Sfx.Play("whip");
                foreach (Agent agent in this.agents)
                {
                    this.Motivate(agent);
                }
            }

            // 鞭の先のほうが動きながら直接当たった場合も、そのエージェントが働き出す
            for (int i = RopePoints / 2; i < RopePoints; i++)
            {
                float speed = (this.rope[i] - this.ropeBefore[i]).magnitude / step;
                foreach (Agent agent in this.agents)
                {
                    if (speed > 4f && Time.time > agent.WorkUntil - this.WorkSeconds + 0.5f
                        && Vector3.Distance(agent.Root.position + Vector3.up * 0.4f, this.rope[i]) < 0.35f)
                    {
                        this.Motivate(agent);
                    }
                }
            }
        }

        private void Motivate(Agent agent)
        {
            agent.WorkUntil = Time.time + this.WorkSeconds;
            agent.Jump = 1f;
            Sfx.Play("yelp", Random.Range(0.9f, 1.3f), 0.8f, 0.06f);
            this.Burst(agent.Root.position + Vector3.up * 0.6f, agent.Color, 30);
        }

        private void UpdateAgents(float dt)
        {
            foreach (Agent agent in this.agents)
            {
                bool working = Time.time < agent.WorkUntil;
                if (agent.WasWorking && !working)
                {
                    Sfx.Play("sleep", 1f, 0.7f, 0.3f);
                }

                agent.WasWorking = working;
                agent.Jump = Mathf.Max(0f, agent.Jump - dt * 2.5f);
                float hop = Mathf.Sin(agent.Jump * Mathf.PI) * 0.35f + (working ? Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 0.03f : 0f);
                Vector3 position = agent.Root.position;
                agent.Root.position = new Vector3(position.x, hop, position.z);

                // 居眠り中はうなだれる
                agent.Head.localRotation = Quaternion.Slerp(agent.Head.localRotation, Quaternion.Euler(working ? 0f : 28f, 0f, 0f), dt * 8f);
                var block = new MaterialPropertyBlock();
                block.SetColor("_Color", working ? agent.Color : agent.Color * 0.15f);
                agent.Visor.SetPropertyBlock(block);
                agent.Label.text = working ? "作業中!" : "Zzz…";
                agent.Label.transform.rotation = FaceCamera;

                Bug target = working ? this.Nearest(agent.Root.position) : null;
                Quaternion facing = Quaternion.identity;
                if (target != null)
                {
                    Vector3 direction = target.Root.position - agent.Root.position;
                    direction.y = 0f;
                    facing = Quaternion.LookRotation(direction);
                    if (Time.time >= agent.NextShot)
                    {
                        agent.NextShot = Time.time + 0.6f;
                        target.Targeted = true;
                        Sfx.Play("pew", Random.Range(0.9f, 1.2f), 0.6f, 0.05f);
                        GameObject bolt = this.Spawn(this.BoltTemplate, agent.Root.position + Vector3.up * 0.62f);
                        bolt.GetComponent<TrailRenderer>().startColor = agent.Color;
                        this.bolts.Add(new Bolt { Root = bolt.transform, Target = target, Color = agent.Color });
                    }
                }

                agent.Root.rotation = Quaternion.Slerp(agent.Root.rotation, facing, dt * 10f);
            }
        }

        private Bug Nearest(Vector3 from)
        {
            Bug best = null;
            float bestDistance = float.MaxValue;
            foreach (Bug bug in this.bugs)
            {
                float distance = (bug.Root.position - from).sqrMagnitude;
                if (!bug.Targeted && distance < bestDistance)
                {
                    best = bug;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// バグは舞台の端から湧いて、アバターめがけて跳ねながら寄ってくる。着いたら周りに群がる。
        /// </summary>
        private void UpdateBugs(float dt)
        {
            if (Time.time >= this.nextSpawn && this.bugs.Count < this.MaxBugs)
            {
                this.nextSpawn = Time.time + this.SpawnInterval;
                GameObject instance = this.Spawn(this.BugTemplate, this.SpawnCenter + new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-1.6f, 1.6f)));
                float angle = Random.Range(-80f, 80f) * Mathf.Deg2Rad;
                var bug = new Bug
                {
                    Root = instance.transform,
                    Offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(0.6f, 1.1f),
                    Phase = Random.value * 10f,
                };
                TextMeshPro label = this.Label(instance.transform, 0.45f, 1.4f, Color.white);
                label.text = this.troubles[Random.Range(0, this.troubles.Length)];
                this.bugs.Add(bug);
            }

            Vector3 prey = this.Avatar != null ? this.Avatar.transform.position : Vector3.zero;
            if (this.Avatar != null && this.Avatar.isHuman && this.Avatar.GetBoneTransform(HumanBodyBones.Hips) != null)
            {
                prey = this.Avatar.GetBoneTransform(HumanBodyBones.Hips).position;
            }

            prey.y = 0f;
            foreach (Bug bug in this.bugs)
            {
                Vector3 position = bug.Root.position;
                position.y = 0f;
                Vector3 toGoal = prey + bug.Offset - position;
                if (toGoal.magnitude > 0.05f)
                {
                    position += toGoal.normalized * Mathf.Min(this.BugSpeed * dt, toGoal.magnitude);
                    bug.Root.rotation = Quaternion.Slerp(bug.Root.rotation, Quaternion.LookRotation(toGoal.normalized), dt * 6f);
                }

                position.y = Mathf.Abs(Mathf.Sin(Time.time * 7f + bug.Phase)) * 0.08f;
                bug.Root.position = position;
                bug.Root.GetChild(bug.Root.childCount - 1).rotation = FaceCamera;
            }
        }

        private void UpdateBolts(float dt)
        {
            for (int i = this.bolts.Count - 1; i >= 0; i--)
            {
                Bolt bolt = this.bolts[i];
                if (!this.bugs.Contains(bolt.Target))
                {
                    Destroy(bolt.Root.gameObject);
                    this.bolts.RemoveAt(i);
                    continue;
                }

                Vector3 goal = bolt.Target.Root.position + Vector3.up * 0.16f;
                bolt.Root.position = Vector3.MoveTowards(bolt.Root.position, goal, 14f * dt);
                if (Vector3.Distance(bolt.Root.position, goal) < 0.05f)
                {
                    this.Destroy(bolt.Target, bolt.Color);
                    Destroy(bolt.Root.gameObject);
                    this.bolts.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// バグを壊す。破片は物理で飛び散る。
        /// </summary>
        private void Destroy(Bug bug, Color color)
        {
            Vector3 center = bug.Root.position + Vector3.up * 0.16f;
            this.Burst(center, color, 35);
            for (int i = 0; i < 7; i++)
            {
                GameObject debris = this.Spawn(this.DebrisTemplate, center + Random.insideUnitSphere * 0.08f);
                Rigidbody body = debris.GetComponent<Rigidbody>();
                body.linearVelocity = Random.onUnitSphere * Random.Range(1.5f, 4f) + Vector3.up * 2.5f;
                body.angularVelocity = Random.onUnitSphere * 12f;
                Destroy(debris, 2.5f);
            }

            Sfx.Play("boom", Random.Range(0.85f, 1.25f), 0.8f, 0.06f);
            this.bugs.Remove(bug);
            Destroy(bug.Root.gameObject);
            this.Killed++;
        }

        /// <summary>
        /// 先へ行くほど大きい値（動かされやすさ）。根元の 1 から先端の 6 まで。
        /// </summary>
        private static float Lightness(int index)
        {
            return 1f + 5f * (index - HandlePoints) / (RopePoints - 1 - HandlePoints);
        }

        /// <summary>
        /// 柄の向き（前腕から手への向き）
        /// </summary>
        private Vector3 HandDirection()
        {
            if (this.HandOverride != null)
            {
                return this.HandOverride.forward;
            }

            if (this.Avatar != null && this.Avatar.isHuman)
            {
                Transform hand = this.Avatar.GetBoneTransform(HumanBodyBones.RightHand);
                Transform arm = this.Avatar.GetBoneTransform(HumanBodyBones.RightLowerArm);
                if (hand != null && arm != null && hand.position != arm.position)
                {
                    return (hand.position - arm.position).normalized;
                }
            }

            return Vector3.down;
        }

        private Vector3 Hand()
        {
            if (this.HandOverride != null)
            {
                return this.HandOverride.position;
            }

            if (this.Avatar != null && this.Avatar.isHuman)
            {
                Transform hand = this.Avatar.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null)
                {
                    return hand.position;
                }
            }

            return this.transform.position + Vector3.up;
        }

        private GameObject Spawn(GameObject template, Vector3 position)
        {
            GameObject instance = Instantiate(template, position, Quaternion.identity, this.transform);
            instance.SetActive(true);
            this.spawned.Add(instance);
            return instance;
        }

        private TextMeshPro Label(Transform parent, float height, float size, Color color)
        {
            var label = new GameObject("Label", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.transform.localPosition = Vector3.up * height;
            label.rectTransform.sizeDelta = new Vector2(2f, 0.3f);
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            JapaneseFont.Style(label, "label", Color.white, color);
            return label;
        }

        private void Burst(Vector3 position, Color color, int count)
        {
            if (this.Sparks != null)
            {
                this.Sparks.Emit(new ParticleSystem.EmitParams { position = position, startColor = color, applyShapeToPosition = true }, count);
            }
        }
    }
}
