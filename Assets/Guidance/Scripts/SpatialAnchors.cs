using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 空間アンカーの仕組みを見せる仕掛け。
    /// 1. アバターの顔が向いている所に、ゴーグルのカメラが見つけた「特徴点」（黄色い点）がたまっていく。
    /// 2. 同じ面の上に点が十分たまると、その面を「水平な面（床や机）」や「垂直な面（壁）」として見つけ、水色の格子で示す。
    /// 3. 見つけた面の上に仮想の物を置く（床には床に置く物、壁には壁に掛ける物）。置いた物には「アンカー」の印が付き、
    ///    アバターが動いても、その場所に固定されたまま残る。
    /// 面を見つけると、最初の1個は自動で置く。そのあとは、片手を頭より上に上げると、顔の向いている面に物を置く（K キーでも置ける）。
    /// C キーで今の頭の向きを正面にし直し、L キーで点・面・置いた物を全部消す。A / D キーで見る向きを左右に回せる。
    /// </summary>
    public sealed class SpatialAnchors : Gimmick
    {
        [System.Serializable]
        private sealed class Settings
        {
            public Anchors anchors = new Anchors();
        }

        [System.Serializable]
        private sealed class Anchors
        {
            // 1秒に見つける特徴点の数
            public float pointsPerSecond = 40f;
            // 面として見つけるのに要る点の数
            public int pointsForPlane = 25;
            // ゴーグルのカメラの視野（度）
            public float view = 70f;
            // 特徴点を見つける距離（m）。床の点が遠くまで散らばらないように
            public float range = 4.5f;
            // 片手を頭よりどれだけ上に上げたら置くか（m）と、その姿勢を続ける秒数
            public float handUp = 0.05f;
            public float hold = 0.4f;
            public float keyTurn = 30f;
        }

        private sealed class Plane
        {
            public Collider Surface;
            public bool Floor;
            public Vector3 Normal;
            public readonly List<Vector3> Points = new List<Vector3>();
            public Transform Grid;
            public bool Found;
            public int Placed;
        }

        public Animator Avatar;
        public Transform HeadGoggle;
        public ParticleSystem FeaturePoints;
        // 面を示す格子のひな形（Quad。表は -Z）
        public GameObject GridTemplate;
        public GameObject[] FloorItems = new GameObject[0];
        public GameObject[] WallItems = new GameObject[0];
        public GameObject PinTemplate;
        // 特徴点が見つかる物（壁など）。場面に出ている間だけ置く
        public GameObject Room;
        public ParticleSystem Sparks;

        private readonly Dictionary<Collider, Plane> planes = new Dictionary<Collider, Plane>();
        private readonly List<GameObject> placed = new List<GameObject>();
        private Anchors settings = new Anchors();
        private HeadPose pose;
        private SlideDeck deck;
        private string baseBody = "";
        private string lastStatus = "";
        private float pending;
        private float raised;
        private bool armed = true;
        private float keyYaw;
        private int floorIndex;
        private int wallIndex;

        public int PointCount { get; private set; }

        public int FloorsFound { get; private set; }

        public int WallsFound { get; private set; }

        public int PlacedCount => this.placed.Count;

        protected override void OnEnter(string json, SlideDeck deck)
        {
            this.settings = Read<Settings>(json).anchors ?? new Anchors();
            this.deck = deck;
            this.baseBody = deck.Body != null ? deck.Body.text : "";
            this.lastStatus = "";
            this.pose = new HeadPose(this.Avatar);
            this.keyYaw = 0f;
            this.pose.Wear(this.HeadGoggle, true, this.transform);
            this.Room.SetActive(true);
            this.Clear();
        }

        protected override void OnExit(SlideDeck deck)
        {
            this.StopAllCoroutines();
            this.pose?.Wear(this.HeadGoggle, false, this.transform);
            this.Clear();
            this.Room.SetActive(false);
        }

        /// <summary>
        /// 点・面・置いた物を全部消す（L キーと同じ）
        /// </summary>
        public void Clear()
        {
            this.FeaturePoints.Clear();
            foreach (Plane plane in this.planes.Values)
            {
                if (plane.Grid != null)
                {
                    Destroy(plane.Grid.gameObject);
                }
            }

            this.planes.Clear();
            foreach (GameObject item in this.placed)
            {
                Destroy(item);
            }

            this.placed.Clear();
            this.PointCount = 0;
            this.FloorsFound = 0;
            this.WallsFound = 0;
            this.floorIndex = 0;
            this.wallIndex = 0;
        }

        public void Turn(int direction)
        {
            this.keyYaw += direction * this.settings.keyTurn;
            Sfx.Play("blip", direction > 0 ? 1.2f : 0.9f);
        }

        /// <summary>
        /// 見る向き（頭の向き ＋ キーで回した分）
        /// </summary>
        public Quaternion Facing => Quaternion.Euler(0f, this.keyYaw, 0f) * this.pose.Turn * this.pose.Body;

        /// <summary>
        /// 顔の向いている面（見つけ済みのもの）に物を置く。向いている先に無ければ、最後に見つけた面に置く（K キーと同じ）
        /// </summary>
        public bool PlaceAtGaze()
        {
            Vector3 eye = this.pose.Eye;
            Vector3 forward = this.Facing * Vector3.forward;
            if (Physics.Raycast(eye, forward, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore)
                && this.planes.TryGetValue(hit.collider, out Plane plane) && plane.Found)
            {
                this.Place(plane, hit.point);
                return true;
            }

            // 見つけた面の中から、顔の向きに一番近い所を探す
            Plane best = null;
            Vector3 bestPoint = Vector3.zero;
            float bestScore = float.MaxValue;
            foreach (Plane candidate in this.planes.Values)
            {
                if (!candidate.Found)
                {
                    continue;
                }

                foreach (Vector3 point in candidate.Points)
                {
                    float score = Vector3.Angle(forward, point - eye);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                        bestPoint = point;
                    }
                }
            }

            if (best == null)
            {
                Sfx.Play("thud");
                return false;
            }

            this.Place(best, bestPoint);
            return true;
        }

        private void Update()
        {
            if (!this.InUse)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.K))
            {
                this.PlaceAtGaze();
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                this.Clear();
                Sfx.Play("poof");
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                this.pose.Calibrate();
                this.keyYaw = 0f;
                Sfx.Play("pickup");
            }

            if (Input.GetKeyDown(KeyCode.A))
            {
                this.Turn(-1);
            }

            if (Input.GetKeyDown(KeyCode.D))
            {
                this.Turn(1);
            }

            this.CheckHand();
            this.FindPoints();
            this.ShowStatus();
        }

        /// <summary>
        /// 片手を頭より上に上げて少し待つと、物を置く。一度置いたら、手を下ろすまで次は受け付けない
        /// </summary>
        private void CheckHand()
        {
            if (this.Avatar == null || !this.Avatar.isHuman || this.pose.Head == null)
            {
                return;
            }

            Transform left = this.Avatar.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform right = this.Avatar.GetBoneTransform(HumanBodyBones.RightHand);
            float top = this.pose.Head.position.y + this.settings.handUp;
            bool up = (left != null && left.position.y > top) || (right != null && right.position.y > top);
            if (!up)
            {
                this.raised = 0f;
                this.armed = true;
                return;
            }

            this.raised += Time.deltaTime;
            if (this.armed && this.raised >= this.settings.hold)
            {
                this.armed = false;
                this.PlaceAtGaze();
            }
        }

        /// <summary>
        /// 視野の中のあちこちに線を飛ばし、当たった所を特徴点にする。点は当たった面ごとに数える
        /// </summary>
        private void FindPoints()
        {
            this.pending += Time.deltaTime * this.settings.pointsPerSecond;
            Vector3 eye = this.pose.Eye;
            Quaternion facing = this.Facing;
            float half = this.settings.view * 0.5f;
            while (this.pending >= 1f)
            {
                this.pending -= 1f;
                // 下向き寄りに散らす（床と、目の高さの壁が入るように）
                Vector3 direction = facing * Quaternion.Euler(Random.Range(-half * 0.6f, half * 1.1f), Random.Range(-half, half), 0f) * Vector3.forward;
                if (!Physics.Raycast(eye, direction, out RaycastHit hit, this.settings.range, ~0, QueryTriggerInteraction.Ignore)
                    || hit.collider.GetComponent<AvatarColliders.Part>() != null)
                {
                    continue;
                }

                bool floor = hit.normal.y > 0.8f;
                bool wall = Mathf.Abs(hit.normal.y) < 0.3f;
                if (!floor && !wall)
                {
                    continue;
                }

                this.FeaturePoints.Emit(new ParticleSystem.EmitParams { position = hit.point + hit.normal * 0.01f, startColor = new Color(1f, 0.85f, 0.2f) }, 1);
                this.PointCount++;

                if (!this.planes.TryGetValue(hit.collider, out Plane plane))
                {
                    plane = new Plane { Surface = hit.collider, Floor = floor, Normal = floor ? Vector3.up : hit.normal };
                    this.planes[hit.collider] = plane;
                }

                plane.Points.Add(hit.point);
                if (plane.Found)
                {
                    this.FitGrid(plane);
                }
                else if (plane.Points.Count >= this.settings.pointsForPlane)
                {
                    this.Found(plane);
                }
            }
        }

        private void Found(Plane plane)
        {
            plane.Found = true;
            if (plane.Floor)
            {
                this.FloorsFound++;
            }
            else
            {
                this.WallsFound++;
            }

            plane.Grid = Instantiate(this.GridTemplate, this.transform).transform;
            plane.Grid.gameObject.SetActive(true);
            this.FitGrid(plane);
            Sfx.Play("coin");
            this.Burst(plane.Grid.position, 60, new Color(0.1f, 0.85f, 1f));

            // 最初の1個は自動で置く。床なら顔の向きの 1.2m 先、壁なら点の中ほどに一番近い点
            Vector3 target = Vector3.zero;
            if (plane.Floor)
            {
                Vector3 forward = this.Facing * Vector3.forward;
                forward.y = 0f;
                target = this.pose.Eye + forward.normalized * 1.2f;
            }
            else
            {
                foreach (Vector3 point in plane.Points)
                {
                    target += point;
                }

                target /= plane.Points.Count;
            }

            Vector3 nearest = plane.Points[0];
            foreach (Vector3 point in plane.Points)
            {
                if (Vector3.Distance(point, target) < Vector3.Distance(nearest, target))
                {
                    nearest = point;
                }
            }

            this.Place(plane, nearest);
        }

        /// <summary>
        /// 格子を、面の上の点を囲む大きさに合わせる
        /// </summary>
        private void FitGrid(Plane plane)
        {
            Quaternion rotation = Quaternion.LookRotation(-plane.Normal, plane.Floor ? Vector3.forward : Vector3.up);
            Quaternion inverse = Quaternion.Inverse(rotation);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            float depth = 0f;
            foreach (Vector3 point in plane.Points)
            {
                Vector3 local = inverse * point;
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
                depth += local.z;
            }

            depth /= plane.Points.Count;
            Vector2 middle = (min + max) * 0.5f;
            Vector2 size = Vector2.Max(max - min, Vector2.one * 0.3f) + Vector2.one * 0.15f;
            plane.Grid.SetPositionAndRotation(rotation * new Vector3(middle.x, middle.y, depth) + plane.Normal * 0.015f, rotation);
            plane.Grid.localScale = new Vector3(size.x, size.y, 1f);
        }

        private void Place(Plane plane, Vector3 point)
        {
            GameObject[] items = plane.Floor ? this.FloorItems : this.WallItems;
            if (items.Length == 0)
            {
                return;
            }

            int index = plane.Floor ? this.floorIndex++ : this.wallIndex++;
            GameObject item = Instantiate(items[index % items.Length], this.transform);
            // 床の物は顔の方を向け、壁の物は壁に沿わせる
            Vector3 toEye = this.pose.Eye - point;
            toEye.y = 0f;
            Quaternion rotation = plane.Floor
                ? Quaternion.LookRotation(toEye.sqrMagnitude > 0.01f ? toEye.normalized : Vector3.forward, Vector3.up)
                : Quaternion.LookRotation(plane.Normal, Vector3.up);
            item.transform.SetPositionAndRotation(point, rotation);
            item.SetActive(true);

            GameObject pin = Instantiate(this.PinTemplate, item.transform);
            pin.SetActive(true);
            this.placed.Add(item);
            plane.Placed++;
            Sfx.Play("land", 1.2f);
            this.Burst(point, 40, new Color(1f, 0.25f, 0.7f));
            this.StartCoroutine(this.Pop(item.transform));
        }

        private IEnumerator Pop(Transform item)
        {
            for (float t = 0f; t < 0.35f && item != null; t += Time.deltaTime)
            {
                float k = t / 0.35f;
                item.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.35f) * Mathf.Min(1f, k * 3f);
                yield return null;
            }

            if (item != null)
            {
                item.localScale = Vector3.one;
            }
        }

        private void ShowStatus()
        {
            if (this.deck == null || this.deck.Body == null)
            {
                return;
            }

            string status = "特徴点 " + (this.PointCount / 10 * 10) + "　面：水平 " + this.FloorsFound + "・垂直 " + this.WallsFound + "　置いた物 " + this.placed.Count;
            if (status == this.lastStatus)
            {
                return;
            }

            this.lastStatus = status;
            this.deck.Body.text = string.IsNullOrEmpty(this.baseBody) ? status : this.baseBody + "\n" + status;
        }

        private void Burst(Vector3 position, int count, Color color)
        {
            if (this.Sparks != null)
            {
                this.Sparks.Emit(new ParticleSystem.EmitParams { position = position, startColor = color, applyShapeToPosition = true }, count);
            }
        }
    }
}
