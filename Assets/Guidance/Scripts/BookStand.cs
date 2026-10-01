using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 回転式のスタンド。面ごとに項目（アイコンと名前）が1つずつ付いていて、アバターが手で回すと次の面が正面に来る。
    /// 正面に来た項目は、順にスクリーンにも書き足されていく。全部の面を見せ終わると締めの一言が出る。
    /// 手が面に触れるたびに、カチッと1面ぶん回る。
    /// T キーでも1面ずつ回せる（Shift+T で逆回り）。項目は slides.json の stand で指定する。
    /// </summary>
    public sealed class BookStand : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Item
        {
            // アイコンの種類：sensor / receiver / pc / avatar / projector
            public string icon = "";
            public string label = "";
        }

        /// <summary>
        /// 回る部分に付けて、当たったことを知らせる
        /// </summary>
        private sealed class Relay : MonoBehaviour
        {
            public BookStand Owner;

            private void OnTriggerEnter(Collider other)
            {
                this.Owner.Hit(other);
            }
        }

        public Rigidbody Drum;
        public GameObject PanelTemplate;
        public GameObject[] Icons = new GameObject[0];
        public TMP_Text Body;
        public TMP_Text Note;
        public ParticleSystem Sparks;
        public float PanelWidth = 0.55f;
        // 続けて触れても、この秒数が過ぎるまでは次へ進まない（1回払っただけで何面も進まないように）
        public float HitInterval = 0.8f;
        public Color Highlight = new Color(0.1f, 0.85f, 1f);

        private readonly List<GameObject> panels = new List<GameObject>();
        private Item[] items = new Item[0];
        private bool[] seen = new bool[0];
        private string note = "";
        private bool turning;
        private bool finished;
        private float lastHit = -10f;

        /// <summary>
        /// いま正面を向いている面の番号（0〜）
        /// </summary>
        public int Index { get; private set; }

        public int SeenCount
        {
            get
            {
                int count = 0;
                foreach (bool value in this.seen)
                {
                    count += value ? 1 : 0;
                }

                return count;
            }
        }

        /// <summary>
        /// 場面の切り替えで呼ばれる。項目が無ければ隠す。
        /// </summary>
        public void Set(Item[] newItems, string newNote)
        {
            this.items = newItems ?? new Item[0];
            this.note = newNote ?? "";
            this.gameObject.SetActive(this.items.Length > 0);
            if (!Application.isPlaying || this.items.Length == 0)
            {
                return;
            }

            if (this.Drum.GetComponent<Relay>() == null)
            {
                this.Drum.gameObject.AddComponent<Relay>().Owner = this;
            }

            this.StopAllCoroutines();
            this.turning = false;
            foreach (GameObject panel in this.panels)
            {
                Destroy(panel);
            }

            this.panels.Clear();
            this.seen = new bool[this.items.Length];
            this.finished = false;
            this.Drum.transform.localRotation = Quaternion.identity;

            // 面を正多角形に並べる
            int count = this.items.Length;
            float step = 360f / count;
            float radius = count >= 3 ? this.PanelWidth / (2f * Mathf.Tan(Mathf.PI / count)) : 0.02f;
            for (int i = 0; i < count; i++)
            {
                Quaternion turn = Quaternion.Euler(0f, i * step, 0f);
                GameObject panel = Instantiate(this.PanelTemplate, this.Drum.transform);
                panel.transform.localRotation = turn;
                panel.transform.localPosition = turn * new Vector3(0f, 0f, radius);
                panel.SetActive(true);
                this.panels.Add(panel);

                foreach (GameObject icon in this.Icons)
                {
                    if (icon.name == this.items[i].icon)
                    {
                        GameObject instance = Instantiate(icon, panel.transform);
                        instance.transform.localPosition = new Vector3(0f, 0.1f, 0.03f);
                        instance.SetActive(true);
                    }
                }

                this.Label(panel.transform, this.items[i].label, new Vector3(0f, -0.25f, 0.02f), new Vector2(0.5f, 0.22f), 1.1f, Color.white);
                this.Label(panel.transform, (i + 1) + " / " + count, new Vector3(0f, 0.34f, 0.02f), new Vector2(0.5f, 0.08f), 0.5f, this.Highlight);
            }

            this.Index = -1;
            this.lastHit = Time.time;
            this.Reveal(0);
        }

        /// <summary>
        /// 1面ぶん回す（T キーと同じ）。direction が 1 なら次の項目、-1 なら前の項目が正面に来る。
        /// </summary>
        public void Turn(int direction)
        {
            if (!this.turning && this.items.Length > 0)
            {
                this.StartCoroutine(this.TurnTo(this.Index + direction));
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                this.Turn(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);
            }
        }

        private IEnumerator TurnTo(int target)
        {
            this.turning = true;
            float step = 360f / this.items.Length;
            Quaternion from = this.Drum.transform.localRotation;
            Quaternion to = Quaternion.Euler(0f, -target * step, 0f);
            const float duration = 0.55f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                // 勢いよく回って少し行き過ぎ、戻って止まる
                float k = t / duration;
                float eased = 1f - Mathf.Pow(1f - k, 3f) + Mathf.Sin(k * Mathf.PI) * 0.12f * (1f - k);
                this.Drum.transform.localRotation = Quaternion.SlerpUnclamped(from, to, eased);
                yield return null;
            }

            this.Drum.transform.localRotation = to;
            this.turning = false;
            this.Reveal(((target % this.items.Length) + this.items.Length) % this.items.Length);
        }

        /// <summary>
        /// 正面に来た面を光らせ、その項目をスクリーンに書き足す。
        /// </summary>
        private void Reveal(int index)
        {
            if (index == this.Index)
            {
                return;
            }

            this.Index = index;
            this.seen[index] = true;
            Sfx.Play("blip", 1f + index * 0.12f);

            for (int i = 0; i < this.panels.Count; i++)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_EmissionColor", this.Highlight * (i == index ? 0.45f : 0.04f));
                this.panels[i].transform.GetChild(0).GetComponent<Renderer>().SetPropertyBlock(block);
            }

            var lines = new List<string>();
            for (int i = 0; i < this.items.Length; i++)
            {
                if (this.seen[i])
                {
                    lines.Add((i + 1) + ". " + this.items[i].label);
                }
            }

            if (this.Body != null)
            {
                this.Body.text = string.Join("\n", lines);
            }

            bool all = this.SeenCount == this.items.Length;
            if (this.Note != null)
            {
                this.Note.text = all ? this.note : "";
            }

            if (all && !this.finished)
            {
                this.finished = true;
                Sfx.Play("coin");
                this.Burst(this.Drum.position, 120);
            }
        }

        /// <summary>
        /// 手が面に触れたら、次の項目へ1面ぶん回す。どちら向きに払っても必ず次へ進む（順番が崩れないように）。
        /// </summary>
        private void Hit(Collider other)
        {
            AvatarColliders.Part part = other.GetComponent<AvatarColliders.Part>();
            if (part == null || !part.Hand || this.turning || Time.time - this.lastHit < this.HitInterval)
            {
                return;
            }

            this.lastHit = Time.time;
            Sfx.Play("thud", 1.3f, 0.7f);
            this.Burst(other.transform.position, 20);
            this.Turn(1);
        }

        private void Label(Transform parent, string text, Vector3 position, Vector2 size, float fontSize, Color color)
        {
            var label = new GameObject("Label", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.transform.localPosition = position;
            // TextMeshPro の文字は自分の +Z の向きに見たときに読めるので、面の外側から読めるよう裏返す
            label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            label.rectTransform.sizeDelta = size;
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.2f;
            label.fontSizeMax = fontSize;
            JapaneseFont.Style(label, "label", Color.white, color);
        }

        private void Burst(Vector3 position, int count)
        {
            if (this.Sparks != null)
            {
                this.Sparks.Emit(new ParticleSystem.EmitParams { position = position, startColor = this.Highlight, applyShapeToPosition = true }, count);
            }
        }
    }
}
