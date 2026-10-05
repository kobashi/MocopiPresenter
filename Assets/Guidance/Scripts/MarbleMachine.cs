using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 物理演算で動くマーブルマシン（ピタゴラ装置）。最初は部品が1つ欠けていて、玉はコースの途中からこぼれ落ちる。
    /// アバターが手で部品に触れて運び、欠けた場所に近づけるとはまって完成し、玉がリフトで上に戻って回り続ける。
    /// M キーでも「はめる／最初に戻す」を切り替えられる（リハーサルと、うまく運べなかったときの保険）。
    /// コースの形は Editor/MarbleMachineBuilder.cs で作っている。座標はすべてこのオブジェクトから見た位置。
    /// </summary>
    public sealed class MarbleMachine : Gimmick
    {
        private sealed class Marble
        {
            public Rigidbody Body;
            public Color Color;
            public bool Spilled;
            public bool WasLow;
            public float Still;
        }

        public Rigidbody Tray;
        public Rigidbody Skirt;
        public Transform Part;
        public GameObject Ghost;
        public GameObject MarbleTemplate;
        public ParticleSystem Sparks;
        public Animator Avatar;
        public int MarbleCount = 8;
        // 部品が最初に浮かんでいる場所（ワールド座標）
        public Vector3 LoosePosition = new Vector3(-0.8f, 1.2f, 0.3f);
        public float GrabRadius = 0.25f;
        // 持った部品が、欠けた場所からこの距離（m）まで近づくと吸い付いてはまる。
        // 距離は客席から見た上下左右だけで測る（手前・奥のずれは数えない）
        public float SnapRadius = 0.75f;
        // この距離まで近づくと、部品が欠けた場所のほうへ引き寄せられ始める
        public float MagnetRadius = 1.5f;
        // 玉を補充する場所（一番上のレールの上）
        public Vector3 Hopper = new Vector3(0.6f, 2.2f, 0f);
        public float TrayBottom = 0.14f;
        public float TrayTop = 2.05f;
        public Color[] Colors =
        {
            new Color(0.1f, 0.85f, 1f),
            new Color(1f, 0.3f, 0.7f),
            new Color(1f, 0.85f, 0.2f),
            new Color(0.45f, 1f, 0.5f),
        };

        public bool Installed { get; private set; }

        /// <summary>
        /// 玉がリフトで上まで運ばれた回数（動作確認用）
        /// </summary>
        public int Laps { get; private set; }

        private readonly List<Marble> marbles = new List<Marble>();
        private readonly Queue<Marble> refill = new Queue<Marble>();
        private Vector3 slot;
        private bool slotKnown;
        private Transform holder;
        private bool snapping;
        private float cycle;
        private float nextRefill;
        private float cycleBefore;

        protected override void OnEnter(string json, SlideDeck deck)
        {
            if (Application.isPlaying)
            {
                this.ResetMachine();
            }
        }

        /// <summary>
        /// 未完成の状態に戻し、玉を上から入れ直す。
        /// </summary>
        public void ResetMachine()
        {
            if (!this.slotKnown)
            {
                // シーン上では部品をはまった位置に置いてあるので、それを「欠けた場所」として覚える
                this.slot = this.Part.localPosition;
                this.slotKnown = true;
            }

            this.StopAllCoroutines();
            foreach (Marble marble in this.marbles)
            {
                Destroy(marble.Body.gameObject);
            }

            this.marbles.Clear();
            this.refill.Clear();
            this.Installed = false;
            this.snapping = false;
            this.holder = null;
            this.cycle = 0f;
            this.Laps = 0;
            this.Part.position = this.LoosePosition;
            this.SetPartSolid(false);
            this.Ghost.SetActive(true);

            for (int i = 0; i < this.MarbleCount; i++)
            {
                GameObject instance = Instantiate(this.MarbleTemplate, this.transform);
                instance.SetActive(false);
                var marble = new Marble { Body = instance.GetComponent<Rigidbody>(), Color = this.Colors[i % this.Colors.Length] };
                var block = new MaterialPropertyBlock();
                block.SetColor("_Color", marble.Color);
                instance.GetComponent<Renderer>().SetPropertyBlock(block);
                TrailRenderer trail = instance.GetComponent<TrailRenderer>();
                trail.startColor = marble.Color;
                trail.endColor = new Color(marble.Color.r, marble.Color.g, marble.Color.b, 0f);
                this.marbles.Add(marble);
                this.refill.Enqueue(marble);
            }
        }

        /// <summary>
        /// 部品を欠けた場所にはめて完成させる。こぼれていた玉は上から入れ直す。
        /// </summary>
        public void Install()
        {
            this.StopAllCoroutines();
            this.snapping = false;
            this.holder = null;
            this.Part.localPosition = this.slot;
            this.SetPartSolid(true);
            this.Ghost.SetActive(false);
            this.Installed = true;
            foreach (Color color in this.Colors)
            {
                this.Burst(this.Part.position, color, 60);
            }

            Sfx.Play("fanfare");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                if (this.Installed)
                {
                    this.ResetMachine();
                }
                else
                {
                    this.Install();
                }
            }

            if (!this.Installed && !this.snapping)
            {
                this.CarryPart();
            }

            this.WatchMarbles();

            if (this.refill.Count > 0 && Time.time >= this.nextRefill)
            {
                this.nextRefill = Time.time + 0.35f;
                this.Drop(this.refill.Dequeue());
            }
        }

        /// <summary>
        /// 部品は浮かんで待っていて、アバターの手が触れるとその手に付いていく。欠けた場所に近づくとはまる。
        /// </summary>
        private void CarryPart()
        {
            if (this.holder == null)
            {
                this.Part.position = this.LoosePosition + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.05f;
                if (this.Avatar != null && this.Avatar.isHuman)
                {
                    foreach (HumanBodyBones bone in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
                    {
                        Transform hand = this.Avatar.GetBoneTransform(bone);
                        if (hand != null && Vector3.Distance(hand.position, this.Part.position) < this.GrabRadius)
                        {
                            this.holder = hand;
                            Sfx.Play("pickup");
                            this.Burst(hand.position, this.Colors[2], 30);
                        }
                    }
                }
            }
            float pull = 0f;
            if (this.holder != null)
            {
                Vector3 carried = this.holder.position + Vector3.up * 0.1f;
                Vector3 local = this.transform.InverseTransformPoint(carried);
                float distance = Vector2.Distance(new Vector2(local.x, local.y), new Vector2(this.slot.x, this.slot.y));
                if (distance < this.SnapRadius)
                {
                    this.Part.position = carried;
                    this.StartCoroutine(this.SnapIn());
                    return;
                }

                // 近づくほど、手から離れて欠けた場所のほうへ寄っていく
                pull = Mathf.Clamp01(Mathf.InverseLerp(this.MagnetRadius, this.SnapRadius, distance));
                this.Part.position = Vector3.Lerp(carried, this.transform.TransformPoint(this.slot), pull * 0.45f);
            }

            // 欠けた場所の目印は、ゆっくり明滅し、部品が近づくほど明るくなる
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", new Color(0.25f, 0.22f, 0.08f) * (1f + 0.35f * Mathf.Sin(Time.time * 4f) + pull * 5f));
            foreach (Renderer renderer in this.Ghost.GetComponentsInChildren<Renderer>())
            {
                renderer.SetPropertyBlock(block);
            }
        }

        private IEnumerator SnapIn()
        {
            this.snapping = true;
            Vector3 from = this.Part.position;
            Vector3 to = this.transform.TransformPoint(this.slot);
            const float duration = 0.35f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                this.Part.position = Vector3.Lerp(from, to, k * k * (3f - 2f * k));
                yield return null;
            }

            this.Install();
        }

        private void WatchMarbles()
        {
            foreach (Marble marble in this.marbles)
            {
                if (!marble.Body.gameObject.activeSelf)
                {
                    continue;
                }

                Vector3 local = this.transform.InverseTransformPoint(marble.Body.position);
                bool outside = local.x > 2.5f || local.x < -0.1f || local.y < 0.06f;
                if (outside && !marble.Spilled)
                {
                    // こぼれた玉は遠くまで転がっていかないよう、床で止まりやすくする
                    marble.Spilled = true;
                    Sfx.Play("spill", Random.Range(0.9f, 1.2f), 0.8f, 0.1f);
                    marble.Body.linearDamping = 1.5f;
                }

                if (local.y < 0.6f)
                {
                    marble.WasLow = true;
                }
                else if (marble.WasLow && local.y > 1.9f)
                {
                    marble.WasLow = false;
                    this.Laps++;
                }

                if (!this.Installed)
                {
                    continue;
                }

                // 完成後は、こぼれた玉や、どこかに引っかかって止まった玉を上から入れ直す
                marble.Still = marble.Body.linearVelocity.magnitude < 0.03f ? marble.Still + Time.deltaTime : 0f;
                if ((marble.Spilled || marble.Still > 12f) && !this.refill.Contains(marble))
                {
                    this.Burst(marble.Body.position, marble.Color, 15);
                    marble.Body.gameObject.SetActive(false);
                    this.refill.Enqueue(marble);
                }
            }
        }

        private void Drop(Marble marble)
        {
            marble.Spilled = false;
            marble.WasLow = false;
            marble.Still = 0f;
            Vector3 position = this.transform.TransformPoint(this.Hopper + Vector3.right * Random.Range(-0.1f, 0.1f));
            marble.Body.transform.position = position;
            marble.Body.gameObject.SetActive(true);
            marble.Body.position = position;
            marble.Body.linearVelocity = Vector3.zero;
            marble.Body.angularVelocity = Vector3.zero;
            marble.Body.linearDamping = 0f;
            marble.Body.GetComponent<TrailRenderer>().Clear();
            Sfx.Play("blip", Random.Range(0.9f, 1.3f), 0.6f);
        }

        /// <summary>
        /// リフト（受け皿）の動き。下で玉を受け、上がり、右に傾けて玉を一番上のレールに流し、下りる。
        /// 受け皿の下に付いた板（Skirt）は、受け皿が上にいる間、待っている玉が縦穴に落ちないよう入口をふさぐ。
        /// </summary>
        private void FixedUpdate()
        {
            this.cycle = (this.cycle + Time.fixedDeltaTime) % 4.4f;
            float t = this.cycle;
            float height;
            float tilt;
            if (t < 0.9f)
            {
                height = 0f;
                tilt = 5f;
            }
            else if (t < 2.2f)
            {
                height = Mathf.SmoothStep(0f, 1f, (t - 0.9f) / 1.3f);
                tilt = 5f;
            }
            else if (t < 2.45f)
            {
                height = 1f;
                tilt = Mathf.Lerp(5f, -25f, (t - 2.2f) / 0.25f);
            }
            else if (t < 3.05f)
            {
                height = 1f;
                tilt = -25f;
            }
            else if (t < 3.3f)
            {
                height = 1f;
                tilt = Mathf.Lerp(-25f, 5f, (t - 3.05f) / 0.25f);
            }
            else
            {
                height = Mathf.SmoothStep(1f, 0f, (t - 3.3f) / 1.1f);
                tilt = 5f;
            }

            // 受け皿が上がり始めるときに1回鳴らす
            if (t >= 0.9f && this.cycleBefore < 0.9f)
            {
                Sfx.Play("lift", 1f, 0.5f);
            }

            this.cycleBefore = t;
            float y = Mathf.Lerp(this.TrayBottom, this.TrayTop, height);
            this.Tray.MovePosition(this.transform.TransformPoint(new Vector3(0.11f, y, 0f)));
            this.Tray.MoveRotation(this.transform.rotation * Quaternion.Euler(0f, 0f, tilt));
            this.Skirt.MovePosition(this.transform.TransformPoint(new Vector3(0.215f, y - 1.03f, 0f)));
        }

        private void SetPartSolid(bool solid)
        {
            foreach (Collider part in this.Part.GetComponentsInChildren<Collider>(true))
            {
                part.enabled = solid;
            }
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
