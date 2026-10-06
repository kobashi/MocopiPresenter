using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

namespace Guidance
{
    /// <summary>
    /// VR と MR の違いを見せる仕掛け。アバターの顔にゴーグルを付け、ゴーグルを付けた人に見えている景色を大きな画面に映す。
    /// MR：ゴーグルのカメラで撮った現実の景色（教室）に、仮想の物（浮かぶ立方体や小さな相棒など）が重なって見える。
    ///     現実の景色は、プレゼンのフォルダに置いた動画（mixed.video）をくり返し流す。動画が無ければ、計算で作った教室を写す。
    ///     360度動画（横:縦が 2:1）なら、まわりの景色として貼り、頭の向きに合わせて見回せる。
    ///     仮想の物はゴーグルの中にしか無いので、客席からは見えない。
    /// VR：現実は見えなくなり、仮想の世界（ゲームの世界）だけが見える。
    /// 両手を頭より上に上げると切り替わる（X キーでも切り替えられる）。どちらでも景色は頭の向きと連動する。
    /// C キーで今の頭の向きを正面にし直す。
    /// </summary>
    public sealed class VrMrSwitch : Gimmick
    {
        [System.Serializable]
        private sealed class Settings
        {
            public Mixed mixed = new Mixed();
        }

        [System.Serializable]
        private sealed class Mixed
        {
            // 最初に見せる方（"MR" か "VR"）
            public string start = "MR";
            // 両手が頭よりどれだけ上に来たら「上げた」とみなすか（m）と、その姿勢を続ける秒数
            public float handsUp = 0.05f;
            public float hold = 0.4f;
            // 切り替えにかける秒数
            public float wipe = 0.9f;
            // MR の現実の景色にする動画（プレゼンのフォルダに置く。mp4 など）。空なら計算で作った教室を写す
            public string video = "";
            // 動画が 360度（正距円筒、横:縦が 2:1）なら true
            public bool video360;
            // 360度動画を回す角度（正面に来る向きを合わせる）
            public float videoYaw;
        }

        /// <summary>
        /// 仮想の物を置く層。舞台を写すカメラ（Camera.main）からは見えないようにする
        /// </summary>
        public const int VirtualLayer = 31;

        public Animator Avatar;
        public Transform HeadGoggle;
        // 現実の舞台を写すカメラ（ゴーグルのカメラ）と、仮想の世界を写すカメラ
        // 仮想の物だけを写すカメラ（現実の景色の上に重ねる）
        public Camera RealEye;
        // 現実の景色（教室）を写すカメラ。動画があるときは、動画をこのカメラの奥いっぱいに流す
        public Camera PassthroughEye;
        // 360度動画を貼る、景色のカメラの背景（Skybox/Panoramic の素材）
        public Skybox Panorama;
        // 教室の、アバターの立ち位置にあたる点
        public Transform ClassroomOrigin;
        public Camera VirtualEye;
        // 仮想の世界の、アバターの立ち位置にあたる点
        public Transform VirtualOrigin;
        // MR で重ねる仮想の物（まとめた親）
        public GameObject Overlay;
        public Renderer Display;
        public TMP_Text ModeLabel;
        public TMP_Text ModeCaption;
        public ParticleSystem Sparks;
        public int Width = 1024;
        public int Height = 576;

        private Mixed settings = new Mixed();
        private HeadPose pose;
        private RenderTexture realTexture;
        private RenderTexture virtualTexture;
        private Material display;
        private float mix;
        private float raised;
        private bool armed = true;
        private int savedMask = -1;
        private Camera stageCamera;
        private VideoPlayer player;
        private int passthroughMask;
        private bool panoramaCopied;

        /// <summary>
        /// いま VR を見せているか（false なら MR）
        /// </summary>
        public bool IsVr { get; private set; }

        public RenderTexture RealTexture => this.realTexture;

        public RenderTexture VirtualTexture => this.virtualTexture;

        protected override void OnEnter(string json, SlideDeck deck)
        {
            this.settings = Read<Settings>(json).mixed ?? new Mixed();
            this.pose = new HeadPose(this.Avatar);
            this.pose.Wear(this.HeadGoggle, true, this.transform);

            this.realTexture = NewTexture(this.Width, this.Height);
            this.virtualTexture = NewTexture(this.Width, this.Height);
            this.RealEye.targetTexture = this.realTexture;
            this.PassthroughEye.targetTexture = this.realTexture;
            this.VirtualEye.targetTexture = this.virtualTexture;
            this.PlayVideo(deck.PresentationName, this.settings.video);
            this.display = this.Display.material;
            this.display.SetTexture("_MainTex", this.realTexture);
            this.display.SetTexture("_SubTex", this.virtualTexture);
            this.display.SetFloat("_Aspect", (float)this.Width / this.Height);

            // 仮想の物は、舞台を写すカメラには写さない
            SetLayer(this.Overlay.transform, VirtualLayer);
            this.Overlay.SetActive(true);
            this.stageCamera = Camera.main;
            if (this.stageCamera != null)
            {
                this.savedMask = this.stageCamera.cullingMask;
                this.stageCamera.cullingMask &= ~(1 << VirtualLayer);
            }

            JapaneseFont.Style(this.ModeLabel, "big", Color.white, new Color(0.3f, 0.8f, 1f));
            JapaneseFont.Style(this.ModeCaption, "label", Color.white, new Color(0.7f, 0.9f, 1f));

            this.IsVr = string.Equals(this.settings.start, "VR", System.StringComparison.OrdinalIgnoreCase);
            this.mix = this.IsVr ? 1f : 0f;
            this.raised = 0f;
            this.armed = true;
            this.ShowMode();
        }

        protected override void OnExit(SlideDeck deck)
        {
            this.StopAllCoroutines();
            this.pose?.Wear(this.HeadGoggle, false, this.transform);
            if (this.stageCamera != null && this.savedMask != -1)
            {
                this.stageCamera.cullingMask = this.savedMask;
            }

            this.savedMask = -1;
            if (this.player != null)
            {
                this.player.Stop();
                this.player.enabled = false;
            }

            this.RealEye.targetTexture = null;
            this.PassthroughEye.targetTexture = null;
            this.PassthroughEye.cullingMask = this.passthroughMask != 0 ? this.passthroughMask : this.PassthroughEye.cullingMask;
            this.PassthroughEye.clearFlags = CameraClearFlags.SolidColor;
            this.Panorama.enabled = false;
            this.VirtualEye.targetTexture = null;
            Release(ref this.realTexture);
            Release(ref this.virtualTexture);
        }

        /// <summary>
        /// VR と MR を切り替える（X キー、両手を上げるのと同じ）
        /// </summary>
        public void Toggle()
        {
            this.IsVr = !this.IsVr;
            this.StopAllCoroutines();
            this.StartCoroutine(this.Wipe(this.IsVr ? 1f : 0f));
            Sfx.Play(this.IsVr ? "lift" : "spill", 1.2f);
            if (this.Sparks != null)
            {
                this.Sparks.Emit(new ParticleSystem.EmitParams { position = this.Display.transform.position, startColor = new Color(0.1f, 0.85f, 1f), applyShapeToPosition = true }, 80);
            }

            this.ShowMode();
        }

        private IEnumerator Wipe(float target)
        {
            float from = this.mix;
            float duration = Mathf.Max(0.05f, this.settings.wipe);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                this.mix = Mathf.Lerp(from, target, k * k * (3f - 2f * k));
                yield return null;
            }

            this.mix = target;
        }

        /// <summary>
        /// 動画があれば、景色のカメラの奥いっぱいにくり返し流す（そのときは教室は写さない）
        /// </summary>
        private void PlayVideo(string presentation, string file)
        {
            if (this.passthroughMask == 0)
            {
                this.passthroughMask = this.PassthroughEye.cullingMask;
            }

            this.PassthroughEye.cullingMask = this.passthroughMask;
            string path = string.IsNullOrEmpty(file) ? "" : Presentation.PathOf(presentation, file);
            if (path.Length == 0 || !File.Exists(path))
            {
                if (path.Length > 0)
                {
                    Debug.LogWarning("MR の動画が見つかりません: " + path);
                }

                return;
            }

            if (this.player == null)
            {
                this.player = this.PassthroughEye.gameObject.AddComponent<VideoPlayer>();
            }

            this.player.enabled = true;
            this.player.playOnAwake = false;
            this.player.source = VideoSource.Url;
            this.player.url = new System.Uri(path).AbsoluteUri;
            this.player.isLooping = true;
            this.player.audioOutputMode = VideoAudioOutputMode.None;
            if (this.settings.video360)
            {
                // 360度動画：絵を受け取り、景色のカメラの背景（まわり一面）に貼る
                this.player.renderMode = VideoRenderMode.APIOnly;
                if (!this.panoramaCopied)
                {
                    // 素材のファイルを書き換えないよう、写しを使う
                    this.Panorama.material = new Material(this.Panorama.material);
                    this.panoramaCopied = true;
                }

                this.Panorama.enabled = true;
                this.Panorama.material.SetFloat("_Rotation", this.settings.videoYaw);
                this.PassthroughEye.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                this.player.renderMode = VideoRenderMode.CameraFarPlane;
                this.player.targetCamera = this.PassthroughEye;
                this.player.aspectRatio = VideoAspectRatio.FitOutside;
            }

            this.player.Play();
            this.PassthroughEye.cullingMask = 0;
        }

        private void ShowMode()
        {
            this.ModeLabel.text = this.IsVr ? "VR" : "MR";
            this.ModeCaption.text = this.IsVr ? "見えるのは仮想の世界だけ" : "現実の景色 ＋ 仮想の物";
        }

        private void Update()
        {
            if (!this.InUse)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                this.Toggle();
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                this.pose.Calibrate();
                Sfx.Play("pickup");
            }

            this.CheckHands();
        }

        /// <summary>
        /// 両手を頭より上に上げて少し待つと切り替える。一度切り替えたら、手を下ろすまで次は受け付けない
        /// </summary>
        private void CheckHands()
        {
            if (this.Avatar == null || !this.Avatar.isHuman || this.pose.Head == null)
            {
                return;
            }

            Transform left = this.Avatar.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform right = this.Avatar.GetBoneTransform(HumanBodyBones.RightHand);
            float top = this.pose.Head.position.y + this.settings.handsUp;
            bool up = left != null && right != null && left.position.y > top && right.position.y > top;
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
                this.Toggle();
            }
        }

        private void LateUpdate()
        {
            if (!this.InUse)
            {
                return;
            }

            // どちらのカメラも、ゴーグルを付けた人の目の位置と頭の向きに合わせる
            Vector3 eye = this.pose.Eye;
            Quaternion facing = this.pose.Turn * this.pose.Body;
            this.RealEye.transform.SetPositionAndRotation(eye, facing);
            Vector3 fromFeet = eye - (this.Avatar != null ? this.Avatar.transform.position : Vector3.zero);
            this.VirtualEye.transform.SetPositionAndRotation(this.VirtualOrigin.position + fromFeet, facing);
            // 教室も頭の向きに合わせて見回せる（少しだけ手持ちのような揺れを足す）
            // （動画を流しているときは揺らさない。撮った映像がもともと揺れているので）
            bool still = this.player == null || !this.player.enabled;
            Quaternion sway = still ? Quaternion.Euler(Mathf.Sin(Time.time * 0.7f) * 0.4f, Mathf.Sin(Time.time * 0.45f) * 0.6f, 0f) : Quaternion.identity;
            this.PassthroughEye.transform.SetPositionAndRotation(this.ClassroomOrigin.position + fromFeet, facing * sway);

            // 見えていない方のカメラは、切り替えの途中以外は止めておく
            this.RealEye.enabled = this.mix < 0.999f;
            this.PassthroughEye.enabled = this.RealEye.enabled;
            this.VirtualEye.enabled = this.mix > 0.001f;
            this.display.SetFloat("_Mix", this.mix);
            if (this.Panorama.enabled && this.player != null && this.player.texture != null)
            {
                this.Panorama.material.mainTexture = this.player.texture;
            }
        }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
            {
                SetLayer(child, layer);
            }
        }

        private static RenderTexture NewTexture(int width, int height)
        {
            var texture = new RenderTexture(width, height, 24) { name = "GoggleView", antiAliasing = 2 };
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
