using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// アウトサイドイン方式のトラッキングを見せる仕掛け。
    /// アバターの体に付けたマーカー（光る点）を、まわりに立てたカメラが外から捉え、線で結んで見せる。
    /// 2台以上のカメラに見えているマーカーは、空間の座標（x, y, z）が決まり、その数字が横に出る。
    /// 1台にしか見えていないマーカーは方向しか分からず、どのカメラにも見えないマーカーは「死角」になる。
    /// 体の陰になるかどうかは、アバターの体の当たり判定（AvatarColliders）でまじめに確かめている。
    /// G キーでカメラの台数を 1 → 2 → … → 全部 → 1 と切り替え、台数を増やすと死角が減ることを見せる。
    /// </summary>
    public sealed class OutsideInTracking : Gimmick
    {
        /// <summary>
        /// 場面の JSON から読む設定
        /// </summary>
        [System.Serializable]
        private sealed class Settings
        {
            public Tracking tracking = new Tracking();
        }

        [System.Serializable]
        private sealed class Tracking
        {
            // 場面に入ったときのカメラの台数
            public int cameras = 1;
            // 座標を出すか
            public bool coordinates = true;
        }

        public Animator Avatar;
        // まわりに立てるカメラ。出す順に並べる（1台目、2台目……）
        public Transform[] Cameras = new Transform[0];
        // カメラの中で線が出る点（レンズ）
        public Transform[] Lenses = new Transform[0];
        // マーカーのひな形（光る小さな球）
        public GameObject MarkerTemplate;
        public Material BeamMaterial;
        public Material Seen;
        public Material Lost;
        public Material Half;
        public ParticleSystem Sparks;
        public float BeamWidth = 0.012f;

        private static readonly HumanBodyBones[] Bones =
        {
            HumanBodyBones.Head, HumanBodyBones.Chest, HumanBodyBones.Hips,
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
        };

        private readonly List<Marker> markers = new List<Marker>();
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Tracking settings = new Tracking();
        private SlideDeck deck;
        private string baseBody = "";
        private string lastStatus = "";

        private sealed class Marker
        {
            public Transform Bone;
            public Transform Dot;
            public Renderer Renderer;
            public TextMeshPro Label;
            public LineRenderer[] Beams;
            public int SeenBy;
        }

        /// <summary>
        /// いま出しているカメラの台数
        /// </summary>
        public int ActiveCameras { get; private set; }

        /// <summary>
        /// 座標が決まっている（2台以上に見えている）マーカーの数
        /// </summary>
        public int Located { get; private set; }

        /// <summary>
        /// どのカメラにも見えていないマーカーの数
        /// </summary>
        public int Hidden { get; private set; }

        public int MarkerCount => this.markers.Count;

        protected override void OnEnter(string json, SlideDeck deck)
        {
            this.settings = Read<Settings>(json).tracking ?? new Tracking();
            this.deck = deck;
            this.baseBody = deck.Body != null ? deck.Body.text : "";
            this.lastStatus = "";
            this.MakeMarkers();
            this.SetCameras(this.settings.cameras);
        }

        protected override void OnExit(SlideDeck deck)
        {
            foreach (Marker marker in this.markers)
            {
                if (marker.Dot != null)
                {
                    Destroy(marker.Dot.gameObject);
                }

                if (marker.Label != null)
                {
                    Destroy(marker.Label.gameObject);
                }

                foreach (LineRenderer beam in marker.Beams)
                {
                    if (beam != null)
                    {
                        Destroy(beam.gameObject);
                    }
                }
            }

            this.markers.Clear();
        }

        /// <summary>
        /// カメラの台数を変える（1〜全部）
        /// </summary>
        public void SetCameras(int count)
        {
            this.ActiveCameras = Mathf.Clamp(count, 1, this.Cameras.Length);
            for (int i = 0; i < this.Cameras.Length; i++)
            {
                bool on = i < this.ActiveCameras;
                if (on && !this.Cameras[i].gameObject.activeSelf && this.Sparks != null)
                {
                    this.Sparks.Emit(new ParticleSystem.EmitParams { position = this.Lenses[i].position, startColor = new Color(0.1f, 0.85f, 1f), applyShapeToPosition = true }, 40);
                }

                this.Cameras[i].gameObject.SetActive(on);
            }
        }

        /// <summary>
        /// カメラを1台増やす。全部出ていたら1台に戻す（G キーと同じ）
        /// </summary>
        public void NextCameraCount()
        {
            int next = this.ActiveCameras >= this.Cameras.Length ? 1 : this.ActiveCameras + 1;
            this.SetCameras(next);
            Sfx.Play(next == 1 ? "prev" : "pickup", 1f + next * 0.08f);
        }

        private void MakeMarkers()
        {
            this.OnExit(null);
            if (this.Avatar == null || !this.Avatar.isHuman)
            {
                return;
            }

            foreach (HumanBodyBones bone in Bones)
            {
                Transform target = this.Avatar.GetBoneTransform(bone);
                if (target == null)
                {
                    continue;
                }

                var marker = new Marker { Bone = target };
                GameObject dot = Instantiate(this.MarkerTemplate, this.transform);
                dot.SetActive(true);
                marker.Dot = dot.transform;
                marker.Renderer = dot.GetComponent<Renderer>();

                var label = new GameObject("Coordinate", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
                label.transform.SetParent(this.transform, false);
                label.rectTransform.sizeDelta = new Vector2(0.9f, 0.12f);
                label.alignment = TextAlignmentOptions.Left;
                label.fontSize = 0.55f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                JapaneseFont.Style(label, "label", Color.white, new Color(0.6f, 1f, 0.7f));
                marker.Label = label;

                marker.Beams = new LineRenderer[this.Cameras.Length];
                for (int i = 0; i < this.Cameras.Length; i++)
                {
                    var beam = new GameObject("Beam", typeof(LineRenderer)).GetComponent<LineRenderer>();
                    beam.transform.SetParent(this.transform, false);
                    beam.sharedMaterial = this.BeamMaterial;
                    beam.positionCount = 2;
                    beam.widthMultiplier = this.BeamWidth;
                    beam.numCapVertices = 2;
                    beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    beam.receiveShadows = false;
                    marker.Beams[i] = beam;
                }

                this.markers.Add(marker);
            }
        }

        private void Update()
        {
            if (!this.InUse)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                this.NextCameraCount();
            }
        }

        private void LateUpdate()
        {
            if (!this.InUse)
            {
                return;
            }

            this.Located = 0;
            this.Hidden = 0;
            Transform viewer = Camera.main != null ? Camera.main.transform : null;
            foreach (Marker marker in this.markers)
            {
                Vector3 point = marker.Bone.position;
                marker.Dot.position = point;
                marker.SeenBy = 0;
                for (int i = 0; i < this.Cameras.Length; i++)
                {
                    LineRenderer beam = marker.Beams[i];
                    bool visible = i < this.ActiveCameras && this.CanSee(this.Lenses[i].position, point);
                    beam.enabled = visible;
                    if (visible)
                    {
                        marker.SeenBy++;
                        beam.SetPosition(0, this.Lenses[i].position);
                        beam.SetPosition(1, point);
                    }
                }

                // 2台以上：座標が決まる／1台：方向だけ／0台：死角
                bool located = marker.SeenBy >= 2;
                this.Located += located ? 1 : 0;
                this.Hidden += marker.SeenBy == 0 ? 1 : 0;
                marker.Renderer.sharedMaterial = located ? this.Seen : marker.SeenBy == 1 ? this.Half : this.Lost;
                foreach (LineRenderer beam in marker.Beams)
                {
                    Color color = located ? new Color(0.3f, 1f, 0.5f, 0.9f) : new Color(1f, 0.8f, 0.2f, 0.7f);
                    beam.startColor = color;
                    beam.endColor = color;
                }

                // 座標は舞台の床の中心を原点にした cm（客席から見て右が x の正、上が y、手前が z）
                marker.Label.gameObject.SetActive(this.settings.coordinates);
                if (this.settings.coordinates)
                {
                    marker.Label.text = located
                        ? string.Format("({0:0}, {1:0}, {2:0})", -point.x * 100f, point.y * 100f, point.z * 100f)
                        : marker.SeenBy == 1 ? "(?, ?, ?)" : "死角";
                    if (viewer != null)
                    {
                        // 客席側のカメラから読めるように向け、点の右に少しずらして出す
                        marker.Label.transform.rotation = Quaternion.LookRotation(marker.Label.transform.position - viewer.position);
                        marker.Label.transform.position = point + viewer.right * 0.48f + Vector3.up * 0.02f;
                    }
                }
            }

            this.ShowStatus();
        }

        /// <summary>
        /// レンズから点までの間に、アバターの体（点のある部分以外）があるか確かめる
        /// </summary>
        private bool CanSee(Vector3 lens, Vector3 point)
        {
            Vector3 toPoint = point - lens;
            float distance = toPoint.magnitude;
            int count = Physics.RaycastNonAlloc(lens, toPoint / distance, this.hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                // 点のすぐ手前（点を包む部分の当たり判定）は数えない
                if (this.hits[i].distance < distance - 0.2f && this.hits[i].collider.GetComponent<AvatarColliders.Part>() != null)
                {
                    return false;
                }
            }

            return true;
        }

        private void ShowStatus()
        {
            if (this.deck == null || this.deck.Body == null)
            {
                return;
            }

            string status = "カメラ " + this.ActiveCameras + " 台：座標が分かる点 " + this.Located + " / " + this.markers.Count
                + (this.Hidden > 0 ? "　死角 " + this.Hidden : "");
            if (status == this.lastStatus)
            {
                return;
            }

            this.lastStatus = status;
            this.deck.Body.text = string.IsNullOrEmpty(this.baseBody) ? status : this.baseBody + "\n" + status;
        }
    }
}
