using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 背面スクリーンに映す場面を切り替え、場面ごとに仕掛け（Gimmick）を出し入れする。
    /// 場面の並びは StreamingAssets/Presentations/〈プレゼン名〉/slides.json に書く（Presentation.cs を参照）。
    /// ビルド後も、アプリのフォルダ内の slides.json を書き換えれば内容を変えられる。
    /// 次へ：→ / Space / PageDown、前へ：← / PageUp、最初へ：Home
    /// </summary>
    public sealed class SlideDeck : MonoBehaviour
    {
        /// <summary>
        /// どの場面にも共通の項目。仕掛けごとの項目は、各仕掛けが場面の JSON から自分で読む
        /// </summary>
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
            // この場面で使う仕掛けの名前（Gimmick.Id）
            public string[] gimmicks = new string[0];
        }

        public TMP_Text Title;
        public TMP_Text Big;
        public TMP_Text Body;
        public TMP_Text Note;
        public TMP_Text Page;
        public CameraDirector Director;

        /// <summary>
        /// 読み込んだプレゼンの名前（StreamingAssets/Presentations の下のフォルダ名）
        /// </summary>
        public string PresentationName { get; private set; } = "";

        public Slide[] Slides = new Slide[0];
        public int Current;

        private readonly List<Gimmick> gimmicks = new List<Gimmick>();
        private string[] rawSlides = new string[0];
        private bool decorated;

        /// <summary>
        /// 場面の JSON（仕掛けが自分の設定を探すときに使う）
        /// </summary>
        public IReadOnlyList<string> RawSlides => this.rawSlides;

        private void Start()
        {
            this.Load();
            this.Show(0);
        }

        private void Update()
        {
            // プレゼンの一覧を開いている間は、場面を送らない
            if (PresentationMenu.IsOpen)
            {
                return;
            }

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
        /// プレゼンを読み込む。読めないときは、その理由をスクリーンに出す場面を1枚だけ用意する。
        /// </summary>
        public void Load()
        {
            this.Load(Presentation.Choose());
        }

        /// <summary>
        /// 起動したままプレゼンを切り替える。いまの場面の仕掛けを片付けてから読み込み、最初の場面を出す。
        /// </summary>
        public void LoadPresentation(string presentation)
        {
            foreach (Gimmick gimmick in this.gimmicks)
            {
                gimmick.Exit(this);
            }

            this.Load(presentation);
            this.Current = 0;
            this.Show(0);
        }

        private void Load(string presentation)
        {
            this.gimmicks.Clear();
            this.gimmicks.AddRange(FindObjectsByType<Gimmick>(FindObjectsInactive.Include, FindObjectsSortMode.None));

            this.PresentationName = presentation;
            string path = Presentation.PathOf(this.PresentationName, Presentation.FileName);
            try
            {
                this.rawSlides = Presentation.SplitSlides(File.ReadAllText(path));
                if (this.rawSlides.Length == 0)
                {
                    throw new InvalidDataException("slides が空です");
                }

                this.Slides = new Slide[this.rawSlides.Length];
                for (int i = 0; i < this.rawSlides.Length; i++)
                {
                    this.Slides[i] = JsonUtility.FromJson<Slide>(this.rawSlides[i]);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                this.rawSlides = new[] { "{}" };
                this.Slides = new[] { new Slide { title = path + " を読めません", lines = new[] { e.Message } } };
            }

            foreach (Gimmick gimmick in this.gimmicks)
            {
                gimmick.OnPresentationLoaded(this);
            }
        }

        /// <summary>
        /// その仕掛けを使う最初の場面の JSON（無ければ null）
        /// </summary>
        public string FirstSlideUsing(string id)
        {
            for (int i = 0; i < this.Slides.Length; i++)
            {
                if (Array.IndexOf(this.Slides[i].gimmicks ?? new string[0], id) >= 0)
                {
                    return this.rawSlides[i];
                }
            }

            return null;
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

            // 使わない仕掛けを片付けてから、使う仕掛けを出す（同じ仕掛けを続けて使う場面でも、いったん片付けて出し直す）
            string[] used = slide.gimmicks ?? new string[0];
            foreach (Gimmick gimmick in this.gimmicks)
            {
                gimmick.Exit(this);
            }

            foreach (string id in used)
            {
                Gimmick gimmick = this.gimmicks.Find(g => g.Id == id);
                if (gimmick == null)
                {
                    Debug.LogWarning("仕掛け「" + id + "」が見つかりません（場面 " + (this.Current + 1) + "）");
                    continue;
                }

                gimmick.Enter(this.rawSlides[this.Current], this);
            }
        }
    }
}
