using UnityEngine;
using UnityEngine.UI;

namespace Guidance
{
    /// <summary>
    /// 映像の出し先を決める。モニターが2台あれば、
    ///   ・2台目（プロジェクター）：観客向けの映像を全画面で出す
    ///   ・1台目（ノートPCの画面）：発表者が見る映像。F キーで左右反転（鏡合わせ）を切り替える
    /// とする。会場を向いて発表すると、手元の画面が鏡のように動いたほうが自分の動きと合わせやすいため。
    /// モニターが1台なら、その1台に観客向けの映像を出す（F キーの反転はその画面に効く。練習用）。
    /// 舞台のカメラは一度だけ描き、その絵を両方の画面に貼る（2回描かないので重くならない）。
    /// 発表者用の操作一覧（ConnectionHud）は1台目にだけ出る。
    /// </summary>
    public sealed class DisplayRouter : MonoBehaviour
    {
        private const string MirrorKey = "PresenterMirror";

        public Camera Source;

        /// <summary>
        /// 観客向けの映像を出すディスプレイの番号（0 が1台目）
        /// </summary>
        public static int ProjectorDisplay { get; private set; }

        /// <summary>
        /// プロジェクター用と発表者用に、2台のモニターを使っているか
        /// </summary>
        public static bool Dual { get; private set; }

        /// <summary>
        /// 発表者用の映像を左右反転しているか
        /// </summary>
        public static bool Mirror { get; private set; }

        /// <summary>
        /// 観客向けの画面のうち、舞台を描く幅の割合（右側に案内の板を出すときに狭める）。
        /// 舞台はこの幅に合わせて描くので、縦の画角はそのままで左右が狭くなる（引き伸ばされない）
        /// </summary>
        public static float StageWidth { get; set; } = 1f;

        /// <summary>
        /// 発表者用の映像の左右反転を切り替える（F キーと同じ。動作確認用）
        /// </summary>
        public static void SetMirror(bool mirror)
        {
            Mirror = mirror;
            Prefs.SetInt(MirrorKey, mirror ? 1 : 0);
        }

        private RenderTexture target;
        private RawImage projectorView;
        private RawImage presenterView;

        private void Awake()
        {
            Dual = Display.displays.Length > 1;
            ProjectorDisplay = Dual ? 1 : 0;
            if (Dual)
            {
                // 2台目を全画面で使い始める（エディタでは何もしない）
                Display.displays[1].Activate();
            }

            // 2台のときは鏡合わせから始める。切り替えた状態は次に起動したときも引き継ぐ
            Mirror = Prefs.GetInt(MirrorKey, Dual ? 1 : 0) == 1;

            Blank(ProjectorDisplay);
            this.projectorView = View(ProjectorDisplay, "ProjectorView", false);
            if (Dual)
            {
                Blank(0);
                this.presenterView = View(0, "PresenterView", true);
            }
        }

        private void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                SetMirror(!Mirror);
            }

            // 観客向けの画面の解像度（のうち舞台を描く幅）で描く。大きさが変わったら描く先を作り直す
            int width = Mathf.Max(16, Mathf.RoundToInt((Dual ? Display.displays[ProjectorDisplay].renderingWidth : Screen.width) * StageWidth));
            int height = Dual ? Display.displays[ProjectorDisplay].renderingHeight : Screen.height;
            if (this.target == null || this.target.width != width || this.target.height != height)
            {
                if (this.target != null)
                {
                    this.Source.targetTexture = null;
                    this.target.Release();
                }

                this.target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing),
                    name = "StageView",
                };
                this.Source.targetTexture = this.target;
            }

            var normal = new Rect(0f, 0f, 1f, 1f);
            var mirrored = new Rect(1f, 0f, -1f, 1f);
            this.projectorView.texture = this.target;
            this.projectorView.uvRect = Dual || !Mirror ? normal : mirrored;
            this.projectorView.rectTransform.anchorMax = new Vector2(StageWidth, 1f);
            if (this.presenterView != null)
            {
                this.presenterView.texture = this.target;
                this.presenterView.uvRect = Mirror ? mirrored : normal;
                this.presenterView.GetComponent<AspectRatioFitter>().aspectRatio = (float)this.target.width / this.target.height;
            }
        }

        /// <summary>
        /// 画面全体を黒で塗るだけのカメラ（絵を貼る前の下地）
        /// </summary>
        private static void Blank(int display)
        {
            var camera = new GameObject("Blank" + display).AddComponent<Camera>();
            camera.cullingMask = 0;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.depth = -100;
            camera.targetDisplay = display;
            camera.allowHDR = false;
            camera.allowMSAA = false;
        }

        /// <summary>
        /// 指定したディスプレイに絵を貼る板。fit が true なら縦横比を保って画面に収める
        /// </summary>
        private static RawImage View(int display, string name, bool fit)
        {
            var canvas = new GameObject(name, typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = display;
            canvas.sortingOrder = -100;

            var image = new GameObject("Image", typeof(RawImage)).GetComponent<RawImage>();
            image.rectTransform.SetParent(canvas.transform, false);
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.sizeDelta = Vector2.zero;
            image.raycastTarget = false;
            if (fit)
            {
                image.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            }

            return image;
        }

        /// <summary>
        /// 設定の保存。保存できない環境でも止まらないようにする
        /// </summary>
        private static class Prefs
        {
            public static int GetInt(string key, int fallback)
            {
                try
                {
                    return PlayerPrefs.GetInt(key, fallback);
                }
                catch (System.Exception)
                {
                    return fallback;
                }
            }

            public static void SetInt(string key, int value)
            {
                try
                {
                    PlayerPrefs.SetInt(key, value);
                    PlayerPrefs.Save();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("設定を保存できません: " + e.Message);
                }
            }
        }
    }
}
