using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// プレゼン（場面の並びと、その素材）を読み込む。プレゼンは StreamingAssets/Presentations/〈名前〉/ に1つずつ置く。
    ///   slides.json … 場面の並び（必須）
    ///   その他 …… 仕掛けが使う素材（QR コードの画像など）
    /// どのプレゼンを使うかは、次の順で決める。
    ///   1. 起動時の引数「-presentation 名前」
    ///   2. StreamingAssets/Presentations/selected.txt に書いた名前
    ///   3. フォルダ名の順で最初のもの
    /// </summary>
    public static class Presentation
    {
        public const string FileName = "slides.json";

        public static string Root => Path.Combine(Application.streamingAssetsPath, "Presentations");

        /// <summary>
        /// 使うプレゼンの名前を決める。
        /// </summary>
        public static string Choose()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-presentation");
            if (index >= 0 && index + 1 < args.Length)
            {
                return args[index + 1];
            }

            string selected = Path.Combine(Root, "selected.txt");
            if (File.Exists(selected))
            {
                string name = File.ReadAllText(selected).Trim();
                if (name.Length > 0)
                {
                    return name;
                }
            }

            if (Directory.Exists(Root))
            {
                string[] folders = Directory.GetDirectories(Root);
                Array.Sort(folders, StringComparer.Ordinal);
                if (folders.Length > 0)
                {
                    return Path.GetFileName(folders[0]);
                }
            }

            return "";
        }

        [Serializable]
        private sealed class Header
        {
            public string name = "";
        }

        /// <summary>
        /// 使えるプレゼンの一覧（slides.json のあるフォルダ）。name は slides.json の "name"（無ければフォルダ名）
        /// </summary>
        public static List<(string folder, string name)> List()
        {
            var list = new List<(string, string)>();
            if (!Directory.Exists(Root))
            {
                return list;
            }

            string[] folders = Directory.GetDirectories(Root);
            Array.Sort(folders, StringComparer.Ordinal);
            foreach (string folder in folders)
            {
                string file = Path.Combine(folder, FileName);
                if (!File.Exists(file))
                {
                    continue;
                }

                string name = Path.GetFileName(folder);
                try
                {
                    string title = JsonUtility.FromJson<Header>(File.ReadAllText(file)).name;
                    list.Add((name, string.IsNullOrEmpty(title) ? name : title));
                }
                catch (Exception)
                {
                    list.Add((name, name + "（slides.json を読めません）"));
                }
            }

            return list;
        }

        /// <summary>
        /// 次に起動したときに使うプレゼンとして selected.txt に書く。書けなければ何もしない
        /// </summary>
        public static void Remember(string presentation)
        {
            try
            {
                File.WriteAllText(Path.Combine(Root, "selected.txt"), presentation + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning("selected.txt に書けません: " + e.Message);
            }
        }

        /// <summary>
        /// プレゼンの中の素材ファイルの場所
        /// </summary>
        public static string PathOf(string presentation, string file)
        {
            return Path.Combine(Root, presentation, file);
        }

        /// <summary>
        /// slides.json の "slides" の中身を、場面ごとの JSON 文字列に分ける
        /// （場面ごとに仕掛けが自分の設定を読めるように、JSON のまま渡す）。
        /// </summary>
        public static string[] SplitSlides(string json)
        {
            int key = json.IndexOf("\"slides\"", StringComparison.Ordinal);
            int start = key < 0 ? -1 : json.IndexOf('[', key);
            if (start < 0)
            {
                throw new InvalidDataException("\"slides\": [ ... ] が見つかりません");
            }

            var slides = new List<string>();
            int depth = 0;
            int begin = -1;
            bool inString = false;
            for (int i = start + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (c == '\\')
                    {
                        i++;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                }
                else if (c == '{' || c == '[')
                {
                    if (depth == 0 && c == '{')
                    {
                        begin = i;
                    }

                    depth++;
                }
                else if (c == '}' || c == ']')
                {
                    if (depth == 0)
                    {
                        // "slides" の配列の終わり
                        break;
                    }

                    depth--;
                    if (depth == 0 && c == '}')
                    {
                        slides.Add(json.Substring(begin, i - begin + 1));
                    }
                }
            }

            return slides.ToArray();
        }

        /// <summary>
        /// プレゼンのフォルダにある画像を読み込む。無ければ null
        /// </summary>
        public static Texture2D LoadImage(string presentation, string file, FilterMode filter = FilterMode.Bilinear)
        {
            string path = PathOf(presentation, file);
            if (string.IsNullOrEmpty(file) || !File.Exists(path))
            {
                return null;
            }

            var texture = new Texture2D(2, 2) { filterMode = filter, wrapMode = TextureWrapMode.Clamp, name = file };
            return texture.LoadImage(File.ReadAllBytes(path)) ? texture : null;
        }
    }
}
