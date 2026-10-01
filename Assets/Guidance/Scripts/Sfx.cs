using System.Collections.Generic;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 効果音。音源ファイルは使わず、矩形波・三角波・ノイズを起動時に計算して作る（レトロゲーム風の音）。
    /// 使う側は Sfx.Play("名前") と呼ぶだけ。S キーで音のオン／オフを切り替える。
    /// 音を足すときは Define() に1行足す。
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        private enum Wave
        {
            Square,
            Thin,
            Triangle,
            Noise,
        }

        /// <summary>
        /// 音の1区切り。高さが from から to へ変わりながら seconds 秒鳴る。fade が true なら鳴りながら小さくなる。
        /// </summary>
        private readonly struct Note
        {
            public readonly Wave Wave;
            public readonly float From;
            public readonly float To;
            public readonly float Seconds;
            public readonly bool Fade;

            public Note(Wave wave, float from, float to, float seconds, bool fade = true)
            {
                this.Wave = wave;
                this.From = from;
                this.To = to;
                this.Seconds = seconds;
                this.Fade = fade;
            }
        }

        private const int Rate = 44100;
        private static Sfx instance;

        [Range(0f, 1f)] public float Volume = 0.5f;

        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        private AudioSource[] sources;
        private int next;

        public static int ClipCount => instance != null ? instance.clips.Count : 0;

        /// <summary>
        /// 効果音を鳴らす。同じ音が短い間隔で重なるときは間引く（玉や積み木が一斉に当たってもうるさくならないように）。
        /// </summary>
        public static void Play(string name, float pitch = 1f, float volume = 1f, float minInterval = 0.04f)
        {
            if (instance == null || !instance.clips.TryGetValue(name, out AudioClip clip))
            {
                return;
            }

            if (instance.lastPlayed.TryGetValue(name, out float last) && Time.unscaledTime - last < minInterval)
            {
                return;
            }

            instance.lastPlayed[name] = Time.unscaledTime;
            AudioSource source = instance.sources[instance.next];
            instance.next = (instance.next + 1) % instance.sources.Length;
            source.pitch = pitch;
            source.volume = volume * instance.Volume;
            source.clip = clip;
            source.Play();
        }

        private void Awake()
        {
            instance = this;
            if (FindFirstObjectByType<AudioListener>() == null)
            {
                Camera.main.gameObject.AddComponent<AudioListener>();
            }

            this.sources = new AudioSource[16];
            for (int i = 0; i < this.sources.Length; i++)
            {
                this.sources[i] = this.gameObject.AddComponent<AudioSource>();
                this.sources[i].playOnAwake = false;
            }

            this.Define();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                AudioListener.volume = AudioListener.volume > 0f ? 0f : 1f;
            }
        }

        private void Define()
        {
            // 場面の切り替え
            this.Add("next", Tone(523f, 0.05f), Tone(659f, 0.05f), Tone(784f, 0.05f), new Note(Wave.Square, 1047f, 1047f, 0.16f));
            this.Add("prev", Tone(1047f, 0.05f), Tone(784f, 0.05f), Tone(659f, 0.05f), new Note(Wave.Square, 523f, 523f, 0.16f));
            this.Add("coin", Tone(988f, 0.07f), new Note(Wave.Square, 1319f, 1319f, 0.4f));

            // 積み木
            this.Add("jump", new Note(Wave.Square, 300f, 900f, 0.18f));
            this.Add("land", new Note(Wave.Noise, 900f, 300f, 0.05f), new Note(Wave.Square, 130f, 60f, 0.09f));
            this.Add("thud", new Note(Wave.Triangle, 170f, 70f, 0.08f));
            this.Add("kick", new Note(Wave.Square, 520f, 110f, 0.13f), new Note(Wave.Noise, 2000f, 600f, 0.05f));
            this.Add("poof", new Note(Wave.Noise, 4000f, 500f, 0.22f));

            // マーブルマシン
            this.Add("tick", new Note(Wave.Thin, 1250f, 1000f, 0.03f));
            this.Add("lift", new Note(Wave.Triangle, 220f, 660f, 0.25f));
            this.Add("blip", new Note(Wave.Square, 880f, 880f, 0.05f));
            this.Add("spill", new Note(Wave.Square, 620f, 140f, 0.3f));
            this.Add("pickup", Tone(988f, 0.06f), new Note(Wave.Square, 1319f, 1319f, 0.14f));
            this.Add("fanfare", Tone(392f, 0.09f), Tone(523f, 0.09f), Tone(659f, 0.09f), Tone(784f, 0.09f), Tone(1047f, 0.09f), Tone(1319f, 0.09f), new Note(Wave.Square, 1568f, 1568f, 0.45f));

            // エージェント
            this.Add("whip", new Note(Wave.Noise, 9000f, 2500f, 0.05f, false), new Note(Wave.Thin, 2200f, 380f, 0.09f));
            this.Add("yelp", new Note(Wave.Square, 700f, 1400f, 0.07f, false), new Note(Wave.Square, 1400f, 900f, 0.07f));
            this.Add("pew", new Note(Wave.Thin, 1700f, 280f, 0.12f));
            this.Add("boom", new Note(Wave.Noise, 1300f, 70f, 0.38f));
            this.Add("sleep", new Note(Wave.Triangle, 520f, 200f, 0.45f));
        }

        private static Note Tone(float frequency, float seconds)
        {
            return new Note(Wave.Square, frequency, frequency, seconds, false);
        }

        private void Add(string name, params Note[] notes)
        {
            var samples = new List<float>();
            var random = new System.Random(name.GetHashCode());
            foreach (Note note in notes)
            {
                int count = Mathf.RoundToInt(note.Seconds * Rate);
                float phase = 0f;
                float held = 1f;
                for (int i = 0; i < count; i++)
                {
                    float k = (float)i / count;
                    float before = phase;
                    phase += Mathf.Lerp(note.From, note.To, k) / Rate;
                    float cycle = phase % 1f;
                    float value;
                    switch (note.Wave)
                    {
                        case Wave.Square:
                            value = cycle < 0.5f ? 1f : -1f;
                            break;
                        case Wave.Thin:
                            value = cycle < 0.25f ? 1f : -1f;
                            break;
                        case Wave.Triangle:
                            value = 4f * Mathf.Abs(cycle - 0.5f) - 1f;
                            break;
                        default:
                            // ノイズは、1周期ごとにでたらめな値に切り替える（周波数が低いほど粗い音になる）
                            if (Mathf.Floor(phase) != Mathf.Floor(before))
                            {
                                held = (float)random.NextDouble() * 2f - 1f;
                            }

                            value = held;
                            break;
                    }

                    // 出だしと終わりの「プチッ」を防ぐ短いなだらかさ
                    float edge = Mathf.Min(1f, Mathf.Min(i, count - 1 - i) / (0.002f * Rate));
                    float level = (note.Fade ? 1f - k : 1f) * edge;
                    samples.Add(value * level * (note.Wave == Wave.Triangle ? 0.9f : 0.45f));
                }
            }

            AudioClip clip = AudioClip.Create(name, samples.Count, 1, Rate, false);
            clip.SetData(samples.ToArray(), 0);
            this.clips[name] = clip;
        }
    }
}
