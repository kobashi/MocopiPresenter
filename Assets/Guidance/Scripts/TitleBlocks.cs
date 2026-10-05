using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 文字の積み木を空から降らせて積み上げる。アバターが蹴散らせる。
    /// 積む文字は slides.json の blocks（上の段から順に1行ずつ）で指定する。
    /// blocksOnJump が true の場面では、アバターがジャンプしたときに降ってくる（J キーでも降る）。B キーで積み直す。
    /// rain に言葉を並べると、その言葉の積み木が空から降り続けて舞台を埋める。
    /// </summary>
    public sealed class TitleBlocks : Gimmick
    {
        /// <summary>
        /// 場面の JSON から読む設定
        /// </summary>
        [System.Serializable]
        private sealed class Settings
        {
            // 積む文字（上の段から順に1行ずつ）
            public string[] blocks = new string[0];
            // true なら、アバターがジャンプしたときに降ってくる
            public bool blocksOnJump;
            // ジャンプとみなす腰の上がり幅（m）。0 なら既定値
            public float jumpHeight;
            // 空から降らせ続ける言葉
            public string[] rain = new string[0];
        }

        public GameObject Template;
        public ParticleSystem Sparks;
        public Animator Avatar;
        // 積み木の壁の中央（床の上）
        public Vector3 Center = new Vector3(-0.95f, 0f, 0.9f);
        public float Size = 0.27f;
        public float KickPower = 2.2f;
        // これより遅い接触は「蹴った」とみなさない（立っているだけで飛ばないように）
        public float KickThreshold = 0.8f;
        // 手がこの速さ（m/秒）より速く当たると、積み木が爆発する
        public float PunchThreshold = 0.6f;
        // 腰がふだんの高さからこれだけ（m）上がったらジャンプとみなす
        public float JumpHeight = 0.12f;
        // 降らせる積み木の数と、降らせる範囲（舞台の中心からの幅と奥行き）
        public int RainCount = 70;
        public Vector3 RainCenter = new Vector3(0.2f, 6f, -0.5f);
        public Vector2 RainArea = new Vector2(5.6f, 3.6f);
        public Color[] Colors =
        {
            new Color(0.1f, 0.85f, 1f),
            new Color(1f, 0.3f, 0.7f),
            new Color(1f, 0.85f, 0.2f),
            new Color(0.45f, 1f, 0.5f),
            new Color(1f, 0.55f, 0.2f),
        };

        private readonly List<LetterBlock> blocks = new List<LetterBlock>();
        private string[] rows = new string[0];
        private string[] rain = new string[0];
        private bool onJump;
        private float standing = float.NaN;
        private float lastJump = -10f;

        /// <summary>
        /// これまでに検出したジャンプの回数（動作確認用）
        /// </summary>
        public int Jumps { get; private set; }

        public int BlockCount => this.blocks.Count;

        protected override void OnEnter(string json, SlideDeck deck)
        {
            Settings settings = Read<Settings>(json);
            this.Set(settings.blocks, settings.rain, settings.blocksOnJump, settings.jumpHeight, Application.isPlaying);
        }

        protected override void OnExit(SlideDeck deck)
        {
            this.Clear(Application.isPlaying);
            this.rows = new string[0];
            this.rain = new string[0];
            this.onJump = false;
        }

        private void Update()
        {
            bool jumped = this.DetectJump();
            if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.J) || (jumped && this.onJump))
            {
                this.Clear(true);
                this.Build(true);
            }

            // 遠くへ飛んでいった積み木は片付ける
            for (int i = this.blocks.Count - 1; i >= 0; i--)
            {
                Vector3 position = this.blocks[i].transform.position;
                if (position.y < -3f || new Vector2(position.x, position.z).magnitude > 8f)
                {
                    Destroy(this.blocks[i].gameObject);
                    this.blocks.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 場面の切り替えで呼ばれる。今ある積み木を消して、新しい文字に入れ替える。
        /// animate が false なら積み上がった状態をすぐ作る（エディタでの見た目確認用）。
        /// </summary>
        public void Set(string[] newRows, string[] newRain, bool waitForJump, float jumpHeight, bool animate)
        {
            this.Clear(animate);
            this.rows = newRows ?? new string[0];
            this.rain = newRain ?? new string[0];
            this.onJump = waitForJump;
            if (jumpHeight > 0f)
            {
                this.JumpHeight = jumpHeight;
            }

            if (!animate || !waitForJump)
            {
                this.Build(animate);
            }
        }

        /// <summary>
        /// 積み木を爆発させる。破片が物理で飛び散り、近くの積み木も吹き飛ぶ。
        /// </summary>
        public void Explode(LetterBlock block)
        {
            if (!this.blocks.Remove(block))
            {
                return;
            }

            Vector3 center = block.transform.position;
            this.Burst(center, block.Color, 90);
            this.Burst(center, Color.white, 30);
            Sfx.Play("boom", Random.Range(0.9f, 1.3f), 1f, 0.05f);
            Destroy(block.gameObject);

            for (int i = 0; i < 10; i++)
            {
                GameObject chip = Instantiate(this.Template, center + Random.insideUnitSphere * 0.1f, Random.rotation, this.transform);
                chip.hideFlags = HideFlags.DontSave;
                chip.SetActive(true);
                LetterBlock piece = chip.GetComponent<LetterBlock>();
                piece.SetupChip(block.Color, Random.Range(0.05f, 0.1f));
                piece.Body.linearVelocity = Random.onUnitSphere * Random.Range(2f, 5f) + Vector3.up * 3f;
                piece.Body.angularVelocity = Random.onUnitSphere * 15f;
                Destroy(chip, 2.5f);
            }

            foreach (LetterBlock other in this.blocks)
            {
                if (other != null && !other.Body.isKinematic)
                {
                    other.Body.AddExplosionForce(2.5f * other.Body.mass, center, 1.2f, 0.4f, ForceMode.Impulse);
                }
            }
        }

        public void Burst(Vector3 position, Color color, int count)
        {
            if (this.Sparks == null)
            {
                return;
            }

            var emit = new ParticleSystem.EmitParams { position = position, startColor = color, applyShapeToPosition = true };
            this.Sparks.Emit(emit, count);
        }

        /// <summary>
        /// 腰の高さを見てジャンプを見つける。「ふだんの高さ」は立っている間だけゆっくり追いかける
        /// （しゃがんでから立ち上がってもジャンプと間違えないように）。
        /// </summary>
        private bool DetectJump()
        {
            if (this.Avatar == null || !this.Avatar.isHuman)
            {
                return false;
            }

            Transform hips = this.Avatar.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null)
            {
                return false;
            }

            float height = hips.position.y;
            if (float.IsNaN(this.standing))
            {
                this.standing = height;
            }

            if (Mathf.Abs(height - this.standing) < this.JumpHeight * 0.5f)
            {
                this.standing = Mathf.Lerp(this.standing, height, Time.deltaTime * 0.5f);
            }

            if (height - this.standing > this.JumpHeight && Time.time - this.lastJump > 1.5f)
            {
                this.lastJump = Time.time;
                this.Jumps++;
                Sfx.Play("jump");
                return true;
            }

            return false;
        }

        private void Clear(bool animate)
        {
            this.StopAllCoroutines();
            foreach (LetterBlock block in this.blocks)
            {
                if (block == null)
                {
                    continue;
                }

                if (animate)
                {
                    this.Burst(block.transform.position, block.Color, 25);
                    Destroy(block.gameObject);
                }
                else
                {
                    DestroyImmediate(block.gameObject);
                }
            }

            if (animate && this.blocks.Count > 0)
            {
                Sfx.Play("poof");
            }

            this.blocks.Clear();
        }

        private void Build(bool animate)
        {
            float pitch = this.Size + 0.006f;
            int count = 0;

            // 下の段から順に置く
            for (int row = this.rows.Length - 1; row >= 0; row--)
            {
                string letters = this.rows[row];
                for (int i = 0; i < letters.Length; i++)
                {
                    if (char.IsWhiteSpace(letters[i]))
                    {
                        continue;
                    }

                    // カメラは -Z 向きなので、客席から見て左が +X
                    float x = this.Center.x + ((letters.Length - 1) * 0.5f - i) * pitch;
                    float y = this.Center.y + (this.rows.Length - 1 - row + 0.5f) * pitch;
                    var home = new Vector3(x, y, this.Center.z);

                    LetterBlock block = this.NewBlock(letters[i].ToString(), home, Quaternion.identity, Vector3.one * this.Size, count, true);
                    if (animate)
                    {
                        this.StartCoroutine(this.Drop(block, home, count * 0.09f));
                    }

                    count++;
                }
            }

            if (animate && this.rain.Length > 0)
            {
                this.StartCoroutine(this.Rain());
            }
        }

        private LetterBlock NewBlock(string text, Vector3 position, Quaternion rotation, Vector3 size, int index, bool allFaces)
        {
            GameObject instance = Instantiate(this.Template, position, rotation, this.transform);
            instance.hideFlags = HideFlags.DontSave;
            instance.name = "Block " + text;
            instance.SetActive(true);
            LetterBlock block = instance.GetComponent<LetterBlock>();
            block.Owner = this;
            block.Setup(text, this.Colors[index % this.Colors.Length], JapaneseFont.Get(), size, allFaces);
            this.blocks.Add(block);
            return block;
        }

        /// <summary>
        /// 言葉の積み木を空から次々に降らせる。こちらは最初から物理で落ち、転がって積み重なる。
        /// </summary>
        private IEnumerator Rain()
        {
            for (int i = 0; i < this.RainCount; i++)
            {
                string word = this.rain[i % this.rain.Length];
                // 半角文字は全角の6割の幅として数える
                float units = 0f;
                foreach (char c in word)
                {
                    units += c < 128 ? 0.6f : 1f;
                }

                var size = new Vector3(0.1f + units * 0.19f, 0.26f, 0.26f);
                var position = this.RainCenter + new Vector3(Random.Range(-0.5f, 0.5f) * this.RainArea.x, Random.Range(0f, 1.5f), Random.Range(-0.5f, 0.5f) * this.RainArea.y);
                var rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(-25f, 25f), Random.Range(-15f, 15f));
                this.NewBlock(word, position, rotation, size, i, false);
                yield return new WaitForSeconds(0.12f);
            }
        }

        /// <summary>
        /// 空から落として所定の位置に着地させる。着地するまでは物理を切っておき、きれいに積み上がるようにする。
        /// </summary>
        private IEnumerator Drop(LetterBlock block, Vector3 home, float delay)
        {
            block.Body.isKinematic = true;
            block.gameObject.SetActive(false);
            yield return new WaitForSeconds(delay);

            block.gameObject.SetActive(true);
            Vector3 start = home + Vector3.up * 5f;
            Quaternion spin = Random.rotation;
            const float duration = 0.45f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                block.transform.SetPositionAndRotation(Vector3.Lerp(start, home, k * k), Quaternion.Slerp(spin, Quaternion.identity, k));
                yield return null;
            }

            block.transform.SetPositionAndRotation(home, Quaternion.identity);
            this.Burst(home + Vector3.down * this.Size * 0.5f, block.Color, 18);
            Sfx.Play("land", Random.Range(0.9f, 1.15f));
            block.Body.isKinematic = false;
        }
    }
}
