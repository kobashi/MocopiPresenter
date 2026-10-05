using System.Collections;
using TMPro;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// VR と MR の違いを見せる仕掛け。アバターの顔にゴーグルを付け、ゴーグルを付けた人に見えている景色を大きな画面に映す。
    /// MR：ゴーグルのカメラで撮った現実の舞台に、仮想の物（浮かぶ立方体や小さな相棒など）が重なって見える。
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
        }

        /// <summary>
        /// 仮想の物を置く層。舞台を写すカメラ（Camera.main）からは見えないようにする
        /// </summary>
        public const int VirtualLayer = 31;

        public Animator Avatar;
        public Transform HeadGoggle;
        // 現実の舞台を写すカメラ（ゴーグルのカメラ）と、仮想の世界を写すカメラ
        public Camera RealEye;
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
            this.VirtualEye.targetTexture = this.virtualTexture;
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
            this.RealEye.targetTexture = null;
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

        private void ShowMode()
        {
            this.ModeLabel.text = this.IsVr ? "VR" : "MR";
            this.ModeCaption.text = this.IsVr ? "見えるのは仮想の世界だけ" : "現実の舞台 ＋ 仮想の物";
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

            // 見えていない方のカメラは、切り替えの途中以外は止めておく
            this.RealEye.enabled = this.mix < 0.999f;
            this.VirtualEye.enabled = this.mix > 0.001f;
            this.display.SetFloat("_Mix", this.mix);
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
