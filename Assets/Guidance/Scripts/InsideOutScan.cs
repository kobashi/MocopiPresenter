using System.Collections.Generic;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// インサイドアウト方式（Meta Quest のように、ゴーグルに付いたカメラと LiDAR で自分のまわりを測る）を見せる仕掛け。
    /// アバターの顔にゴーグルを付け、顔の向いている方へ LiDAR の光の線を扇のように振って、当たった所に点を残していく。
    /// 点がたまっていくと、床や壁や机の形が浮かび上がる（空間を測っている様子）。
    /// 同時に、ゴーグルのカメラの視野（光る四角すい）の中に入った手は、指の骨組みが緑で重なる（ハンドトラッキング）。
    /// 視野の外に出た手は赤くなり「見失った」状態になる。
    /// 測る向きは頭の向きと連動する。C キーで今の向きを正面にし直し、L キーで測った点を消す。
    /// A / D キーで測る向きを左右に回せる（体の動きが届かないときの保険）。
    /// </summary>
    public sealed class InsideOutScan : Gimmick
    {
        [System.Serializable]
        private sealed class Settings
        {
            public Scan scan = new Scan();
        }

        [System.Serializable]
        private sealed class Scan
        {
            // ゴーグルのカメラの視野（左右・上下の角度）
            public float cameraWidth = 100f;
            public float cameraHeight = 80f;
            // LiDAR の線の本数（1回に振る扇の線の数）と、1秒に何回振るか
            public int rays = 40;
            // LiDAR の扇の広さ（左右の角度）と、上下に振る範囲（下向きが正）
            public float scanWidth = 170f;
            public float scanUp = -10f;
            public float scanDown = 70f;
            // 1秒に残す点の数（多すぎると古い点がすぐ消える）
            public float pointsPerSecond = 900f;
            // 点が残る秒数
            public float pointLife = 40f;
            // 光の線の太さ（m）
            public float beamWidth = 0.025f;
            public float sweepsPerSecond = 3f;
            // LiDAR が届く距離（m）
            public float range = 4f;
            // A / D キー1回で回す角度
            public float keyTurn = 30f;
        }

        public Animator Avatar;
        public Transform HeadGoggle;
        // ゴーグルのカメラの視野を示す四角すい（頭の向きに合わせて回す）
        public Transform Frustum;
        public ParticleSystem Points;
        public Material BeamMaterial;
        public Material HandSeen;
        public Material HandLost;
        public GameObject JointTemplate;
        // 測られる物（机・棚・壁）。場面に出ている間だけ置く
        public GameObject Room;
        public ParticleSystem Sparks;

        private static readonly HumanBodyBones[][] Fingers =
        {
            new[] { HumanBodyBones.LeftHand, HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal },
            new[] { HumanBodyBones.LeftHand, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal },
            new[] { HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal },
            new[] { HumanBodyBones.LeftHand, HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal },
            new[] { HumanBodyBones.LeftHand, HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal },
            new[] { HumanBodyBones.RightHand, HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal },
            new[] { HumanBodyBones.RightHand, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal },
            new[] { HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal },
            new[] { HumanBodyBones.RightHand, HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal },
            new[] { HumanBodyBones.RightHand, HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal },
        };

        private readonly List<LineRenderer> beams = new List<LineRenderer>();
        private readonly List<(Transform[] bones, LineRenderer line, List<Transform> joints, bool left)> fingers = new List<(Transform[], LineRenderer, List<Transform>, bool)>();
        private Scan settings = new Scan();
        private HeadPose pose;
        private SlideDeck deck;
        private string baseBody = "";
        private string lastStatus = "";
        private float keyYaw;
        private float sweep;
        private float pending;

        /// <summary>
        /// これまでに測った点の数
        /// </summary>
        public int Measured { get; private set; }

        /// <summary>
        /// 左手・右手がカメラの視野に入っているか
        /// </summary>
        public bool LeftTracked { get; private set; }

        public bool RightTracked { get; private set; }

        protected override void OnEnter(string json, SlideDeck deck)
        {
            this.settings = Read<Settings>(json).scan ?? new Scan();
            this.deck = deck;
            this.baseBody = deck.Body != null ? deck.Body.text : "";
            this.lastStatus = "";
            this.pose = new HeadPose(this.Avatar);
            this.keyYaw = 0f;
            this.pose.Wear(this.HeadGoggle, true, this.transform);
            this.Room.SetActive(true);
            this.Clear();
            this.MakeBeams();
            this.MakeHands();
        }

        protected override void OnExit(SlideDeck deck)
        {
            this.pose?.Wear(this.HeadGoggle, false, this.transform);
            this.Room.SetActive(false);
            this.Clear();
            foreach (LineRenderer beam in this.beams)
            {
                Destroy(beam.gameObject);
            }

            this.beams.Clear();
            foreach ((Transform[] _, LineRenderer line, List<Transform> joints, bool _) in this.fingers)
            {
                Destroy(line.gameObject);
                foreach (Transform joint in joints)
                {
                    Destroy(joint.gameObject);
                }
            }

            this.fingers.Clear();
        }

        /// <summary>
        /// 測った点を消す（L キーと同じ）
        /// </summary>
        public void Clear()
        {
            this.Points.Clear();
            this.Measured = 0;
        }

        public void Calibrate()
        {
            this.pose?.Calibrate();
            this.keyYaw = 0f;
        }

        public void Turn(int direction)
        {
            this.keyYaw += direction * this.settings.keyTurn;
            Sfx.Play("blip", direction > 0 ? 1.2f : 0.9f);
        }

        /// <summary>
        /// 測る向き（頭の向き ＋ キーで回した分）
        /// </summary>
        public Quaternion Facing => Quaternion.Euler(0f, this.keyYaw, 0f) * this.pose.Turn * this.pose.Body;

        private void MakeBeams()
        {
            for (int i = 0; i < Mathf.Max(1, this.settings.rays); i++)
            {
                var beam = new GameObject("LidarBeam", typeof(LineRenderer)).GetComponent<LineRenderer>();
                beam.transform.SetParent(this.transform, false);
                beam.sharedMaterial = this.BeamMaterial;
                beam.positionCount = 2;
                beam.widthMultiplier = this.settings.beamWidth;
                beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beam.receiveShadows = false;
                this.beams.Add(beam);
            }
        }

        private void MakeHands()
        {
            if (this.Avatar == null || !this.Avatar.isHuman)
            {
                return;
            }

            foreach (HumanBodyBones[] finger in Fingers)
            {
                var bones = new List<Transform>();
                foreach (HumanBodyBones bone in finger)
                {
                    Transform found = this.Avatar.GetBoneTransform(bone);
                    if (found != null)
                    {
                        bones.Add(found);
                    }
                }

                // 指の骨が無いモデルでは、手首だけの点になる
                if (bones.Count == 0 || (bones.Count == 1 && this.fingers.Exists(f => f.bones[0] == bones[0])))
                {
                    continue;
                }

                var line = new GameObject("Finger", typeof(LineRenderer)).GetComponent<LineRenderer>();
                line.transform.SetParent(this.transform, false);
                line.sharedMaterial = this.BeamMaterial;
                line.positionCount = bones.Count;
                line.widthMultiplier = 0.008f;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var joints = new List<Transform>();
                foreach (Transform _ in bones)
                {
                    GameObject joint = Instantiate(this.JointTemplate, this.transform);
                    joint.SetActive(true);
                    joints.Add(joint.transform);
                }

                this.fingers.Add((bones.ToArray(), line, joints, finger[0] == HumanBodyBones.LeftHand));
            }
        }

        private void Update()
        {
            if (!this.InUse)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                this.Calibrate();
                Sfx.Play("pickup");
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                this.Clear();
                Sfx.Play("poof");
            }

            if (Input.GetKeyDown(KeyCode.A))
            {
                this.Turn(-1);
            }

            if (Input.GetKeyDown(KeyCode.D))
            {
                this.Turn(1);
            }
        }

        private void LateUpdate()
        {
            if (!this.InUse)
            {
                return;
            }

            Vector3 eye = this.pose.Eye;
            Quaternion facing = this.Facing;
            this.Frustum.SetPositionAndRotation(eye, facing);
            this.Sweep(eye, facing);
            this.TrackHands(eye, facing);
            this.ShowStatus();
        }

        /// <summary>
        /// LiDAR の扇を上下に振りながら線を飛ばし、当たった所に点を残す
        /// </summary>
        private void Sweep(Vector3 eye, Quaternion facing)
        {
            this.sweep += Time.deltaTime * this.settings.sweepsPerSecond;
            // 扇は横に広がり、上下に往復する（下向き：足元の床や机を測る）
            float tilt = Mathf.Lerp(this.settings.scanUp, this.settings.scanDown, Mathf.PingPong(this.sweep, 1f));
            float width = this.settings.scanWidth;
            this.pending += Time.deltaTime * this.settings.pointsPerSecond;
            // 1本あたりに点を残す割合（線の数より残す点が少ないときは、間引いて残す）
            float keep = Mathf.Clamp01(this.pending / Mathf.Max(1, this.beams.Count));
            for (int i = 0; i < this.beams.Count; i++)
            {
                float yaw = Mathf.Lerp(-width * 0.5f, width * 0.5f, this.beams.Count > 1 ? i / (this.beams.Count - 1f) : 0.5f);
                // 線ごとに少しずらして、同じ所ばかり測らないようにする
                float jitter = Mathf.Sin(this.sweep * 13.7f + i * 2.3f) * 3f;
                Vector3 direction = facing * Quaternion.Euler(tilt + jitter, yaw, 0f) * Vector3.forward;
                LineRenderer beam = this.beams[i];
                if (this.Cast(eye, direction, out RaycastHit hit))
                {
                    beam.enabled = true;
                    beam.SetPosition(0, eye);
                    beam.SetPosition(1, hit.point);
                    // 近い所は水色、遠い所は桃色
                    float near = Mathf.Clamp01(hit.distance / this.settings.range);
                    Color color = Color.Lerp(new Color(0.1f, 1f, 0.9f), new Color(1f, 0.3f, 0.8f), near);
                    beam.startColor = new Color(color.r, color.g, color.b, 0.15f);
                    beam.endColor = new Color(color.r, color.g, color.b, 1f);
                    if (Random.value < keep)
                    {
                        this.pending -= 1f;
                        this.Points.Emit(new ParticleSystem.EmitParams { position = hit.point + hit.normal * 0.015f, startColor = color, startLifetime = this.settings.pointLife, applyShapeToPosition = false }, 1);
                        this.Measured++;
                    }
                }
                else
                {
                    beam.enabled = false;
                }
            }

            // 当たらなかった分がたまりすぎないようにする
            this.pending = Mathf.Min(this.pending, this.beams.Count * 2f);
        }

        private bool Cast(Vector3 from, Vector3 direction, out RaycastHit nearest)
        {
            nearest = default;
            RaycastHit[] hits = Physics.RaycastAll(from, direction, this.settings.range, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                // 自分の体（アバターの当たり判定）は測らない
                if (hit.distance < best && hit.collider.GetComponent<AvatarColliders.Part>() == null)
                {
                    best = hit.distance;
                    nearest = hit;
                }
            }

            return best < float.MaxValue;
        }

        /// <summary>
        /// 手がゴーグルのカメラの視野に入っていれば、指の骨組みを緑で重ねる。外なら赤
        /// </summary>
        private void TrackHands(Vector3 eye, Quaternion facing)
        {
            bool InView(Vector3 point)
            {
                Vector3 local = Quaternion.Inverse(facing) * (point - eye);
                if (local.z <= 0.05f)
                {
                    return false;
                }

                float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                float pitch = Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
                return Mathf.Abs(yaw) <= this.settings.cameraWidth * 0.5f && Mathf.Abs(pitch) <= this.settings.cameraHeight * 0.5f;
            }

            bool leftBefore = this.LeftTracked;
            bool rightBefore = this.RightTracked;
            Transform leftHand = this.Avatar != null ? this.Avatar.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            Transform rightHand = this.Avatar != null ? this.Avatar.GetBoneTransform(HumanBodyBones.RightHand) : null;
            this.LeftTracked = leftHand != null && InView(leftHand.position);
            this.RightTracked = rightHand != null && InView(rightHand.position);

            foreach ((Transform[] bones, LineRenderer line, List<Transform> joints, bool left) in this.fingers)
            {
                bool tracked = left ? this.LeftTracked : this.RightTracked;
                Material material = tracked ? this.HandSeen : this.HandLost;
                Color color = tracked ? new Color(0.3f, 1f, 0.5f, 0.95f) : new Color(1f, 0.25f, 0.3f, 0.5f);
                line.startColor = color;
                line.endColor = color;
                for (int i = 0; i < bones.Length; i++)
                {
                    line.SetPosition(i, bones[i].position);
                    joints[i].position = bones[i].position;
                    joints[i].GetComponent<Renderer>().sharedMaterial = material;
                }
            }

            if ((this.LeftTracked && !leftBefore) || (this.RightTracked && !rightBefore))
            {
                Sfx.Play("blip", 1.5f, 0.6f);
            }
        }

        private void ShowStatus()
        {
            if (this.deck == null || this.deck.Body == null)
            {
                return;
            }

            string Hand(bool tracked) => tracked ? "<color=#66ff88>追跡中</color>" : "<color=#ff6677>見失った</color>";
            string status = "手：左 " + Hand(this.LeftTracked) + "　右 " + Hand(this.RightTracked)
                + "\n測った点 " + (this.Measured / 100 * 100).ToString("N0");
            if (status == this.lastStatus)
            {
                return;
            }

            this.lastStatus = status;
            this.deck.Body.text = string.IsNullOrEmpty(this.baseBody) ? status : this.baseBody + "\n" + status;
        }
    }
}
