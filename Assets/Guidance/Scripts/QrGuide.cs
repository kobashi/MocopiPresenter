using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Guidance
{
    /// <summary>
    /// 学生用ゲームへの案内（QR コードと検索手順）を、観客向けの画面の右側に表示する。Q キーで表示を切り替える。
    /// 文言は Inspector で編集できる。QR 画像は Tools/make-qr.swift で作り直す。
    /// </summary>
    public sealed class QrGuide : MonoBehaviour
    {
        public Texture2D QrCode;
        public bool Visible;
        public string Title = "学生用ゲームはこちら";
        public string QrCaption = "カメラで QR コードを読み取る";
        public string SearchHeading = "または Web で検索";
        public string[] SearchSteps =
        {
            "「こばし講義」で検索",
            "コースガイダンス",
            "情報システム2026",
        };

        // 画面の高さを 1080 とみなしたときの、案内の板の幅
        private const float PanelWidth = 820f;

        private GameObject panel;
        private bool wasVisible;

        private void Start()
        {
            this.Build();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                this.Visible = !this.Visible;
            }

            if (this.Visible && !this.wasVisible)
            {
                Sfx.Play("coin");
            }

            this.wasVisible = this.Visible;
            if (this.panel != null)
            {
                this.panel.SetActive(this.Visible);
            }

            // 案内を出している間は、舞台を左の空きに描く
            int display = DisplayRouter.ProjectorDisplay;
            float screenWidth = 1080f * (DisplayRouter.Dual ? (float)Display.displays[display].renderingWidth / Display.displays[display].renderingHeight : (float)Screen.width / Screen.height);
            DisplayRouter.StageWidth = this.Visible ? 1f - PanelWidth / screenWidth : 1f;
        }

        /// <summary>
        /// 観客向けの画面に重ねる案内の板を作る。
        /// </summary>
        private void Build()
        {
            var canvas = new GameObject("QrGuideCanvas", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            canvas.transform.SetParent(this.transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = DisplayRouter.ProjectorDisplay;
            canvas.sortingOrder = 10;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var board = new GameObject("Panel", typeof(Image)).GetComponent<Image>();
            board.rectTransform.SetParent(canvas.transform, false);
            board.rectTransform.anchorMin = new Vector2(1f, 0f);
            board.rectTransform.anchorMax = new Vector2(1f, 1f);
            board.rectTransform.pivot = new Vector2(1f, 0.5f);
            board.rectTransform.sizeDelta = new Vector2(PanelWidth, 0f);
            board.color = Color.white;
            board.raycastTarget = false;
            this.panel = board.gameObject;

            float y = -60f;
            this.Text(board.transform, this.Title, ref y, 64f, 90f, FontStyles.Bold);
            y -= 10f;

            var qr = new GameObject("Qr", typeof(RawImage)).GetComponent<RawImage>();
            qr.rectTransform.SetParent(board.transform, false);
            qr.rectTransform.anchorMin = qr.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            qr.rectTransform.pivot = new Vector2(0.5f, 1f);
            qr.rectTransform.anchoredPosition = new Vector2(0f, y);
            qr.rectTransform.sizeDelta = new Vector2(500f, 500f);
            qr.texture = this.QrCode;
            qr.raycastTarget = false;
            y -= 510f;

            this.Text(board.transform, this.QrCaption, ref y, 40f, 60f, FontStyles.Normal);
            y -= 20f;
            this.Text(board.transform, this.SearchHeading, ref y, 40f, 60f, FontStyles.Normal);
            for (int i = 0; i < this.SearchSteps.Length; i++)
            {
                this.Text(board.transform, (i + 1) + ". " + this.SearchSteps[i], ref y, 48f, 66f, FontStyles.Bold, TextAlignmentOptions.Left);
            }

            this.panel.SetActive(this.Visible);
        }

        private void Text(Transform parent, string content, ref float y, float size, float height, FontStyles style, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var text = new GameObject("Text", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(parent, false);
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.pivot = new Vector2(0.5f, 1f);
            text.rectTransform.anchoredPosition = new Vector2(0f, y);
            text.rectTransform.sizeDelta = new Vector2(alignment == TextAlignmentOptions.Left ? -140f : -60f, height);
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.raycastTarget = false;
            var ink = new Color(0.05f, 0.06f, 0.12f);
            JapaneseFont.Style(text, "plain", ink, ink);
            y -= height;
        }
    }
}
