using System.Collections.Generic;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 起動したままプレゼンを切り替える一覧。P キーで手元の画面に出す（観客向けの画面には出ない）。
    /// ↑↓ で選んで Enter（またはクリック）で読み込み、最初の場面から始める。Esc で閉じる。
    /// 選んだプレゼンは selected.txt に書き、次に起動したときもそのプレゼンで始まる（書けない環境では書かない）。
    /// </summary>
    public sealed class PresentationMenu : MonoBehaviour
    {
        public SlideDeck Deck;

        /// <summary>
        /// 一覧を開いているか（開いている間は、他のキー操作の一部を止める）
        /// </summary>
        public static bool IsOpen { get; private set; }

        private static int closedFrame = -1;

        private readonly List<(string folder, string name)> items = new List<(string, string)>();
        private int cursor;
        private GUIStyle box;
        private GUIStyle item;

        /// <summary>
        /// この frame に Esc を一覧が使ったか（Esc でアプリが終わらないように）
        /// </summary>
        public static bool UsedEscape => IsOpen || closedFrame == Time.frameCount;

        public void Open()
        {
            this.items.Clear();
            this.items.AddRange(Presentation.List());
            this.cursor = Mathf.Max(0, this.items.FindIndex(i => i.folder == this.Deck.PresentationName));
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
            closedFrame = Time.frameCount;
        }

        /// <summary>
        /// プレゼンを読み込み直して最初の場面を出す。
        /// </summary>
        public void Switch(string folder)
        {
            this.Close();
            this.Deck.LoadPresentation(folder);
            if (!Application.isEditor)
            {
                Presentation.Remember(folder);
            }
        }

        private void Update()
        {
            if (!IsOpen)
            {
                if (Input.GetKeyDown(KeyCode.P))
                {
                    this.Open();
                }

                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            {
                this.Close();
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                this.cursor = Mathf.Max(0, this.cursor - 1);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                this.cursor = Mathf.Min(this.items.Count - 1, this.cursor + 1);
            }
            else if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && this.items.Count > 0)
            {
                this.Switch(this.items[this.cursor].folder);
            }
        }

        private void OnGUI()
        {
            if (!IsOpen)
            {
                return;
            }

            if (this.box == null)
            {
                this.box = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, richText = true, wordWrap = true };
                this.box.normal.textColor = Color.white;
                this.item = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, richText = true, padding = new RectOffset(16, 16, 0, 0) };
                this.item.normal.textColor = Color.white;
            }

            int size = Mathf.Max(16, Screen.height / 40);
            this.box.fontSize = size;
            this.item.fontSize = size;

            float width = Mathf.Min(Screen.width - 80f, size * 34f);
            float rowHeight = size * 2.2f;
            float height = size * 6f + rowHeight * Mathf.Max(1, this.items.Count);
            var area = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);

            // 濃い紺の板と、シアンの枠
            Fill(new Rect(area.x - 3f, area.y - 3f, area.width + 6f, area.height + 6f), new Color(0.1f, 0.85f, 1f));
            Fill(area, new Color(0.03f, 0.05f, 0.12f));
            GUI.Label(new Rect(area.x + 28f, area.y + size, width - 56f, size * 3.5f),
                "<b><size=" + Mathf.RoundToInt(size * 1.3f) + ">プレゼンを切り替える</size></b>\n<color=#9aa4b8>↑↓ で選んで Enter（クリックでも可）　Esc で閉じる</color>", this.box);

            float y = area.y + size * 4.8f;
            if (this.items.Count == 0)
            {
                GUI.Label(new Rect(area.x + 28f, y, width - 56f, rowHeight), "StreamingAssets/Presentations にプレゼンがありません", this.item);
            }

            for (int i = 0; i < this.items.Count; i++)
            {
                (string folder, string name) = this.items[i];
                var row = new Rect(area.x + 20f, y, width - 40f, rowHeight - 6f);
                if (i == this.cursor)
                {
                    Fill(row, new Color(0.1f, 0.85f, 1f, 0.3f));
                }

                string mark = folder == this.Deck.PresentationName ? "　<color=#ffcc33>使用中</color>" : "";
                GUI.Label(row, (i == this.cursor ? "▶ " : "　 ") + "<b>" + name + "</b>　<color=#9aa4b8>" + folder + "</color>" + mark, this.item);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    this.Switch(folder);
                }

                y += rowHeight;
            }
        }

        private static void Fill(Rect rect, Color color)
        {
            Color before = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = before;
        }
    }
}
