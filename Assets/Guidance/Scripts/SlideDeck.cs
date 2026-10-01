using System;
using System.IO;
using TMPro;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 背面スクリーンに映す場面を切り替える。内容は StreamingAssets/slides.json に書く
    /// （ビルド後も、アプリのフォルダ内の slides.json を書き換えれば内容を変えられる）。
    /// 次へ：→ / Space / PageDown、前へ：← / PageUp、最初へ：Home
    /// </summary>
    public sealed class SlideDeck : MonoBehaviour
    {
        [Serializable]
        public sealed class Slide
        {
            public string title = "";
            // 中央に大きく出す一言
            public string big = "";
            public string[] lines = new string[0];
            // 下に出す締めの一言
            public string note = "";
            // この場面に入ったときのカメラ（1〜。0 なら変えない）
            public int camera;
            // true なら学生用ゲームの案内（QR コード）を出す
            public bool guide;
            // 文字の積み木を積む（上の段から順に1行ずつ）
            public string[] blocks = new string[0];
            // true なら、積み木はアバターがジャンプしたときに降ってくる
            public bool blocksOnJump;
            // ジャンプとみなす腰の上がり幅（m）。0 なら既定値 0.12。反応しにくければ下げる
            public float jumpHeight;
            // 言葉の積み木を空から降らせる
            public string[] rain = new string[0];
            // true ならマーブルマシンを出す
            public bool machine;
            // コーディングエージェントの数（0 なら出さない）と、押し寄せるトラブルの名前
            public int agents;
            public string[] troubles = new string[0];
            // 鞭の先がこの速さ（m/秒）を超えるとエージェントが働き出す（0 なら既定値 8）。反応しにくければ下げる
            public float crackSpeed;
            // 回転式スタンドに載せる項目（アイコンと名前）。全部見せ終わると note が出る
            public BookStand.Item[] stand = new BookStand.Item[0];
        }

        [Serializable]
        private sealed class SlideFile
        {
            public Slide[] slides = new Slide[0];
        }

        public const string FileName = "slides.json";

        public TMP_Text Title;
        public TMP_Text Big;
        public TMP_Text Body;
        public TMP_Text Note;
        public TMP_Text Page;
        public CameraDirector Director;
        public QrGuide Guide;
        public TitleBlocks Blocks;
        public MarbleMachine Machine;
        public AgentScene Agents;
        public BookStand Stand;

        public Slide[] Slides = new Slide[0];
        public int Current;

        private bool decorated;

        private void Start()
        {
            this.Load();
            this.Show(0);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.PageDown))
            {
                this.Show(this.Current + 1);
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.PageUp))
            {
                this.Show(this.Current - 1);
            }
            else if (Input.GetKeyDown(KeyCode.Home))
            {
                this.Show(0);
            }
        }

        /// <summary>
        /// slides.json を読み込む。読めないときは、その理由をスクリーンに出す場面を1枚だけ用意する。
        /// </summary>
        public void Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, FileName);
            try
            {
                this.Slides = JsonUtility.FromJson<SlideFile>(File.ReadAllText(path)).slides;
                if (this.Slides == null || this.Slides.Length == 0)
                {
                    throw new InvalidDataException("slides が空です");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                this.Slides = new[] { new Slide { title = FileName + " を読めません", lines = new[] { e.Message } } };
            }
        }

        /// <summary>
        /// スクリーンの文字に日本語フォントと飾り（縁取り・発光・影・グラデーション）を付ける。最初の1回だけ行う。
        /// </summary>
        private void Decorate()
        {
            if (this.decorated)
            {
                return;
            }

            this.decorated = true;
            JapaneseFont.Style(this.Title, "title", new Color(0.75f, 0.97f, 1f), new Color(0.1f, 0.75f, 1f));
            JapaneseFont.Style(this.Big, "big", Color.white, new Color(0.45f, 0.85f, 1f));
            JapaneseFont.Style(this.Body, "body", Color.white, new Color(0.82f, 0.9f, 1f));
            JapaneseFont.Style(this.Note, "note", new Color(1f, 0.97f, 0.6f), new Color(1f, 0.7f, 0.15f));
            JapaneseFont.Style(this.Page, "body", new Color(0.6f, 0.65f, 0.75f), new Color(0.6f, 0.65f, 0.75f));
        }

        public void Show(int index)
        {
            if (this.Slides.Length == 0)
            {
                return;
            }

            int before = this.Current;
            this.Current = Mathf.Clamp(index, 0, this.Slides.Length - 1);
            if (Application.isPlaying)
            {
                Sfx.Play(this.Current < before ? "prev" : "next");
            }

            this.Decorate();
            Slide slide = this.Slides[this.Current];
            bool hasBig = !string.IsNullOrEmpty(slide.big);

            this.Title.text = slide.title ?? "";
            this.Big.text = slide.big ?? "";
            this.Big.gameObject.SetActive(hasBig);
            this.Body.text = string.Join("\n", slide.lines ?? new string[0]);
            this.Note.text = slide.note ?? "";
            this.Page.text = (this.Current + 1) + " / " + this.Slides.Length;

            // 大きな一言があるときは、本文をその下に詰める
            RectTransform body = this.Body.rectTransform;
            body.offsetMax = new Vector2(body.offsetMax.x, hasBig ? -500f : -200f);
            this.Body.alignment = hasBig ? TextAlignmentOptions.Top : TextAlignmentOptions.TopLeft;

            if (slide.camera > 0 && this.Director != null)
            {
                this.Director.Current = Mathf.Min(slide.camera, this.Director.Shots.Length) - 1;
            }

            if (this.Guide != null)
            {
                this.Guide.Visible = slide.guide;
            }

            if (this.Stand != null)
            {
                this.Stand.Set(slide.stand, slide.note);
            }

            if (this.Agents != null)
            {
                this.Agents.Set(slide.agents, slide.troubles, slide.crackSpeed);
            }

            if (this.Machine != null)
            {
                this.Machine.gameObject.SetActive(slide.machine);
            }

            if (this.Blocks != null)
            {
                this.Blocks.Set(slide.blocks, slide.rain, slide.blocksOnJump, slide.jumpHeight, Application.isPlaying);
            }
        }
    }
}
