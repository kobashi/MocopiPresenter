using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// VR アプリのアイコンを宙に並べる仕掛け。アイコンの絵（模型）は決まった種類から選び、名前は slides.json の apps で指定する。
    /// 手で触れたアイコンは、跳ねてくるっと回る。Y キーで全部を順に跳ねさせる。
    /// アイコンの種類：coaster（ジェットコースター）/ saber（光る剣で切るリズムゲーム）/ pithouse（竪穴式住居）/ zombie（ゾンビを撃つゲーム）
    /// </summary>
    public sealed class AppShowcase : Gimmick
    {
        [System.Serializable]
        private sealed class Settings
        {
            public App[] apps = new App[0];
        }

        [System.Serializable]
        public sealed class App
        {
            public string icon = "";
            public string label = "";
        }

        /// <summary>
        /// 板に付けて、手が触れたことを知らせる
        /// </summary>
        private sealed class Relay : MonoBehaviour
        {
            public AppShowcase Owner;
            public int Index;

            private void OnTriggerEnter(Collider other)
            {
                AvatarColliders.Part part = other.GetComponent<AvatarColliders.Part>();
                if (part != null && part.Hand)
                {
                    this.Owner.Bounce(this.Index);
                }
            }
        }

        public GameObject TileTemplate;
        public GameObject[] Icons = new GameObject[0];
        public ParticleSystem Sparks;
        // 並べる範囲（左端と右端。客席から見て左から順に並ぶ）
        public Vector3 From = new Vector3(1.95f, 1.55f, 0.3f);
        public Vector3 To = new Vector3(-0.25f, 1.55f, 0.3f);

        private readonly List<Transform> tiles = new List<Transform>();
        private readonly List<float> lastBounce = new List<float>();

        public int Count => this.tiles.Count;

        public int Bounced { get; private set; }

        protected override void OnEnter(string json, SlideDeck deck)
        {
            this.Clear();
            App[] apps = Read<Settings>(json).apps ?? new App[0];
            for (int i = 0; i < apps.Length; i++)
            {
                float k = apps.Length > 1 ? i / (apps.Length - 1f) : 0.5f;
                GameObject tile = Instantiate(this.TileTemplate, this.transform);
                tile.transform.localPosition = Vector3.Lerp(this.From, this.To, k) + Vector3.up * (i % 2 == 0 ? 0.08f : -0.08f);
                tile.transform.localRotation = Quaternion.identity;
                tile.SetActive(true);
                tile.AddComponent<Relay>().Owner = this;
                tile.GetComponent<Relay>().Index = i;

                Transform body = tile.transform.GetChild(0);
                foreach (GameObject icon in this.Icons)
                {
                    if (icon.name == apps[i].icon)
                    {
                        GameObject instance = Instantiate(icon, body);
                        instance.transform.localPosition = new Vector3(0f, 0.06f, 0.08f);
                        instance.SetActive(true);
                    }
                }

                var label = new GameObject("Label", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
                label.transform.SetParent(body, false);
                label.transform.localPosition = new Vector3(0f, -0.38f, 0.04f);
                label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                label.rectTransform.sizeDelta = new Vector2(0.62f, 0.14f);
                label.text = apps[i].label;
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = 0.2f;
                label.fontSizeMax = 0.9f;
                JapaneseFont.Style(label, "label", Color.white, new Color(0.1f, 0.85f, 1f));

                this.tiles.Add(tile.transform);
                this.lastBounce.Add(-10f);
                this.StartCoroutine(this.Arrive(body, i * 0.15f));
            }

            this.Bounced = 0;
        }

        protected override void OnExit(SlideDeck deck)
        {
            this.StopAllCoroutines();
            this.Clear();
        }

        private void Clear()
        {
            foreach (Transform tile in this.tiles)
            {
                Destroy(tile.gameObject);
            }

            this.tiles.Clear();
            this.lastBounce.Clear();
        }

        private void Update()
        {
            if (!this.InUse)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Y))
            {
                for (int i = 0; i < this.tiles.Count; i++)
                {
                    this.StartCoroutine(this.Later(i * 0.12f, i));
                }
            }

            // ゆらゆら浮かぶ
            for (int i = 0; i < this.tiles.Count; i++)
            {
                Transform body = this.tiles[i].GetChild(0);
                body.localPosition = new Vector3(body.localPosition.x, Mathf.Sin(Time.time * 1.3f + i) * 0.03f, body.localPosition.z);
            }
        }

        private IEnumerator Later(float delay, int index)
        {
            yield return new WaitForSeconds(delay);
            this.Bounce(index);
        }

        /// <summary>
        /// アイコンを跳ねさせて、くるっと1回転させる（手で触れたときと同じ）
        /// </summary>
        public void Bounce(int index)
        {
            if (index < 0 || index >= this.tiles.Count || Time.time - this.lastBounce[index] < 0.8f)
            {
                return;
            }

            this.lastBounce[index] = Time.time;
            this.Bounced++;
            Sfx.Play("jump", 1f + index * 0.1f);
            if (this.Sparks != null)
            {
                this.Sparks.Emit(new ParticleSystem.EmitParams { position = this.tiles[index].position, startColor = new Color(1f, 0.85f, 0.2f), applyShapeToPosition = true }, 50);
            }

            this.StartCoroutine(this.Spin(this.tiles[index]));
        }

        private IEnumerator Spin(Transform tile)
        {
            const float duration = 0.7f;
            Vector3 home = tile.localPosition;
            for (float t = 0f; t < duration && tile != null; t += Time.deltaTime)
            {
                float k = t / duration;
                tile.localPosition = home + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.25f;
                tile.localRotation = Quaternion.Euler(0f, 360f * (1f - Mathf.Pow(1f - k, 3f)), 0f);
                yield return null;
            }

            if (tile != null)
            {
                tile.localPosition = home;
                tile.localRotation = Quaternion.identity;
            }
        }

        private IEnumerator Arrive(Transform body, float delay)
        {
            body.localScale = Vector3.zero;
            yield return new WaitForSeconds(delay);
            for (float t = 0f; t < 0.4f && body != null; t += Time.deltaTime)
            {
                float k = t / 0.4f;
                body.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.25f) * k;
                yield return null;
            }

            if (body != null)
            {
                body.localScale = Vector3.one;
                Sfx.Play("blip", 1.3f, 0.6f);
            }
        }
    }
}
