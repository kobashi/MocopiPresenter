using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// VR のヘッドトラッキングの仕組みを見せる仕掛け。
    /// アバターの頭の向き → ゲームの世界のカメラの向き → 左右の目の2台のカメラの映像 → ゴーグルの左右の画面、と順につながる。
    /// 舞台の上には、ゲームの世界の模型（ミニチュア）と、その中のカメラ、ゴーグルの大きな模型を置く。
    /// 実際に映すのは、舞台から離れた場所に置いた原寸のゲームの世界（模型と同じ作り）。
    /// アバターが頭を回すと、模型のカメラも同じだけ回り、ゴーグルの左右の画面の景色が変わる。
    /// 頭の正面は場面に入ったときの向きで決める（C キーで今の向きを正面にし直す）。
    /// A / D キーでもカメラを左右に回せる（体の動きが届かないときの保険）。
    /// </summary>
    public sealed class HeadsetView : Gimmick
    {
        /// <summary>
        /// 場面の JSON から読む設定
        /// </summary>
        [System.Serializable]
        private sealed class Settings
        {
            public Headset headset = new Headset();
        }

        [System.Serializable]
        private sealed class Headset
        {
            // 左右の目の間隔（m）。人の目はおよそ 0.064。大きくすると左右の絵の違いが分かりやすくなる
            public float eyeGap = 0.064f;
            // 頭の向きを追いかける速さ（大きいほどすぐ追いつく。0 なら遅れなし）
            public float follow = 12f;
            // A / D キー1回で回す角度
            public float keyTurn = 30f;
        }

        public Animator Avatar;
        // 舞台の上の模型の中のカメラ（回る部分）
        public Transform MiniCamera;
        // 原寸のゲームの世界の中の、左右の目のカメラをまとめた部分（回る部分）
        public Transform EyeRig;
        public Camera LeftEye;
        public Camera RightEye;
        public Renderer LeftLens;
        public Renderer RightLens;
        // アバターの頭に付けるゴーグル
        public Transform HeadGoggle;
        // 信号の流れを示す光の粒のひな形
        public GameObject DotTemplate;
        public Transform[] LensLabels = new Transform[0];
        public ParticleSystem Sparks;
        public int TextureSize = 512;

        private readonly List<(Transform dot, int path, float offset)> dots = new List<(Transform, int, float)>();
        private RenderTexture leftTexture;
        private RenderTexture rightTexture;
        private HeadPose pose;
        private Quaternion baseMini;
        private Quaternion baseRig;
        private Quaternion current = Quaternion.identity;
        private float keyYaw;
        private Headset settings = new Headset();

        /// <summary>
        /// いまの頭の向き（正面からの回転）。確認用
        /// </summary>
        public Quaternion HeadTurn => this.current;

        public RenderTexture LeftTexture => this.leftTexture;

        public RenderTexture RightTexture => this.rightTexture;

        private void Awake()
        {
            this.baseMini = this.MiniCamera.localRotation;
            this.baseRig = this.EyeRig.localRotation;
        }

        protected override void OnEnter(string json, SlideDeck deck)
        {
            this.settings = Read<Settings>(json).headset ?? new Headset();
            this.pose = new HeadPose(this.Avatar);

            this.leftTexture = NewTexture(this.TextureSize);
            this.rightTexture = NewTexture(this.TextureSize);
            this.LeftEye.targetTexture = this.leftTexture;
            this.RightEye.targetTexture = this.rightTexture;
            this.LeftLens.material.mainTexture = this.leftTexture;
            this.RightLens.material.mainTexture = this.rightTexture;
            this.SetEyeGap(this.settings.eyeGap);

            foreach (Transform label in this.LensLabels)
            {
                TMP_Text text = label.GetComponent<TMP_Text>();
                JapaneseFont.Style(text, "label", Color.white, new Color(0.1f, 0.85f, 1f));
            }

            this.Calibrate();
            this.current = Quaternion.identity;
            this.WearGoggle(true);
            this.MakeDots();
        }

        protected override void OnExit(SlideDeck deck)
        {
            this.WearGoggle(false);
            this.LeftEye.targetTexture = null;
            this.RightEye.targetTexture = null;
            Release(ref this.leftTexture);
            Release(ref this.rightTexture);
            foreach ((Transform dot, int _, float _) in this.dots)
            {
                if (dot != null)
                {
                    Destroy(dot.gameObject);
                }
            }

            this.dots.Clear();
        }

        /// <summary>
        /// 今の頭の向きを正面にする（C キーと同じ）
        /// </summary>
        public void Calibrate()
        {
            this.pose?.Calibrate();
            this.keyYaw = 0f;
        }

        /// <summary>
        /// カメラを左右に回す（A / D キーと同じ）。direction が 1 なら右、-1 なら左（ゴーグルを付けた人から見て）
        /// </summary>
        public void Turn(int direction)
        {
            this.keyYaw += direction * this.settings.keyTurn;
            Sfx.Play("blip", direction > 0 ? 1.2f : 0.9f);
        }

        public void SetEyeGap(float gap)
        {
            this.LeftEye.transform.localPosition = new Vector3(-gap * 0.5f, 0f, 0f);
            this.RightEye.transform.localPosition = new Vector3(gap * 0.5f, 0f, 0f);
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

            if (Input.GetKeyDown(KeyCode.A))
            {
                this.Turn(-1);
            }

            if (Input.GetKeyDown(KeyCode.D))
            {
                this.Turn(1);
            }

            // 正面を向いていたときからの頭の回り具合（世界の向きで測る）に、キーで回した分を足す
            Quaternion turn = Quaternion.Euler(0f, this.keyYaw, 0f) * this.pose.Turn;
            float k = this.settings.follow > 0f ? 1f - Mathf.Exp(-this.settings.follow * Time.deltaTime) : 1f;
            this.current = Quaternion.Slerp(this.current, turn, k);

            // 模型の世界も原寸の世界も回転させずに置いてあるので、頭と同じ回転をそのまま掛ければよい
            this.MiniCamera.localRotation = this.current * this.baseMini;
            this.EyeRig.localRotation = this.current * this.baseRig;

            this.MoveDots();
        }

        private void LateUpdate()
        {
            if (!this.InUse)
            {
                return;
            }

            // 頭に付けたゴーグルの位置から、模型のカメラへの線の始まりを決める
            this.UpdatePaths();
        }

        private void WearGoggle(bool wear)
        {
            this.pose?.Wear(this.HeadGoggle, wear, this.transform);
        }

        // 信号の通り道：0 は頭 → 模型のカメラ、1 と 2 は模型のカメラ → 左右の画面
        private readonly Vector3[][] paths = { new Vector3[2], new Vector3[2], new Vector3[2] };

        private void UpdatePaths()
        {
            Vector3 headPoint = this.HeadGoggle != null && this.HeadGoggle.gameObject.activeSelf ? this.HeadGoggle.position : this.MiniCamera.position + Vector3.right * -1f;
            this.paths[0][0] = headPoint;
            this.paths[0][1] = this.MiniCamera.position;
            this.paths[1][0] = this.MiniCamera.position;
            this.paths[1][1] = this.LeftLens.transform.position;
            this.paths[2][0] = this.MiniCamera.position;
            this.paths[2][1] = this.RightLens.transform.position;
        }

        private void MakeDots()
        {
            this.UpdatePaths();
            for (int path = 0; path < 3; path++)
            {
                for (int i = 0; i < 6; i++)
                {
                    GameObject dot = Instantiate(this.DotTemplate, this.transform);
                    dot.SetActive(true);
                    // 頭からの信号は小さく、映像の信号は少し大きく
                    dot.transform.localScale = Vector3.one * (path == 0 ? 0.035f : 0.045f);
                    this.dots.Add((dot.transform, path, i / 6f));
                }
            }
        }

        private void MoveDots()
        {
            float speed = 0.6f;
            foreach ((Transform dot, int path, float offset) in this.dots)
            {
                float t = Mathf.Repeat(Time.time * speed + offset, 1f);
                Vector3 a = this.paths[path][0];
                Vector3 b = this.paths[path][1];
                // 少し上に弧を描いて飛ぶ
                dot.position = Vector3.Lerp(a, b, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.12f;
            }
        }

        private static RenderTexture NewTexture(int size)
        {
            var texture = new RenderTexture(size, size, 24) { name = "HeadsetEye", antiAliasing = 2 };
            texture.Create();
            return texture;
        }

        private static void Release(ref RenderTexture texture)
        {
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
                texture = null;
            }
        }
    }
}
