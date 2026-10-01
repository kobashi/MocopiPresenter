using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Guidance
{
    /// <summary>
    /// TextMeshPro 用の日本語フォントを、OS に入っているフォントから実行時に作る（フォントファイルを同梱しないため）。
    /// あわせて、縁取り・影・発光・グラデーションを付けた文字の飾り（スタイル）を用意する。
    /// </summary>
    public static class JapaneseFont
    {
        // フォントファイル名の候補。先にあるものを優先する。前半が Windows、後半が macOS
        private static readonly string[] Files =
        {
            "yugothb.ttc", "meiryob.ttc", "yugothm.ttc", "meiryo.ttc", "bizudgothic-bold.ttc", "msgothic.ttc",
            "ヒラギノ角ゴシック w6.ttc", "ヒラギノ角ゴシック w5.ttc", "ヒラギノ角ゴシック w3.ttc",
        };

        // ファイルから作れなかったときの候補（フォント名と太さ）
        private static readonly string[,] Families =
        {
            { "Yu Gothic", "Bold" },
            { "Meiryo", "Bold" },
            { "Yu Gothic UI", "Bold" },
            { "MS Gothic", "Regular" },
            { "Hiragino Sans", "W6" },
            { "Hiragino Sans", "W3" },
        };

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static TMP_FontAsset cached;

        public static TMP_FontAsset Get()
        {
            if (cached != null)
            {
                return cached;
            }

            // 縁取りや発光を太く付けられるよう、文字のまわりの余白を広めに取って作る
            string best = null;
            int bestRank = int.MaxValue;
            foreach (string path in Font.GetPathsToOSFonts())
            {
                string file = Path.GetFileName(path).Normalize(NormalizationForm.FormC).ToLowerInvariant();
                int rank = System.Array.IndexOf(Files, file);
                if (rank >= 0 && rank < bestRank)
                {
                    best = path;
                    bestRank = rank;
                }
            }

            if (best != null)
            {
                cached = TMP_FontAsset.CreateFontAsset(best, 0, 90, 14, GlyphRenderMode.SDFAA, 2048, 2048);
                if (cached != null)
                {
                    Debug.Log("日本語フォント: " + best);
                }
            }

            for (int i = 0; cached == null && i < Families.GetLength(0); i++)
            {
                cached = TMP_FontAsset.CreateFontAsset(Families[i, 0], Families[i, 1]);
                if (cached != null)
                {
                    Debug.Log("日本語フォント: " + Families[i, 0] + " " + Families[i, 1]);
                }
            }

            if (cached == null)
            {
                Debug.LogWarning("日本語フォントが見つかりません。文字が表示されない可能性があります");
                cached = TMP_Settings.defaultFontAsset;
            }
            else
            {
                cached.hideFlags = HideFlags.DontSave;
            }

            return cached;
        }

        /// <summary>
        /// 文字に日本語フォントと飾りを付ける。
        /// style: title（見出し）/ big（大きな一言）/ body（本文）/ note（締めの一言）/ label（立体物の名札）/ block（積み木の文字）
        /// top と bottom を変えると、上から下へのグラデーションになる。
        /// </summary>
        public static void Style(TMP_Text text, string style, Color top, Color bottom)
        {
            text.font = Get();
            text.fontSharedMaterial = Material(style);
            text.color = Color.white;
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(top, top, bottom, bottom);
        }

        private static Material Material(string style)
        {
            if (Materials.TryGetValue(style, out Material existing) && existing != null)
            {
                return existing;
            }

            var material = new Material(Get().material) { name = "Text " + style, hideFlags = HideFlags.DontSave };
            var navy = new Color(0.02f, 0.03f, 0.1f, 1f);
            switch (style)
            {
                case "title":
                    Outline(material, navy, 0.12f);
                    Glow(material, new Color(0.1f, 0.7f, 1f, 0.55f), 0.55f);
                    Shadow(material, new Color(0f, 0f, 0f, 0.8f), 0.6f);
                    break;
                case "big":
                    Outline(material, new Color(0.05f, 0.2f, 0.45f, 1f), 0.16f);
                    Glow(material, new Color(0.3f, 0.8f, 1f, 0.6f), 0.7f);
                    Shadow(material, new Color(0f, 0f, 0f, 0.85f), 0.8f);
                    break;
                case "note":
                    Outline(material, new Color(0.35f, 0.12f, 0f, 1f), 0.14f);
                    Glow(material, new Color(1f, 0.55f, 0.1f, 0.6f), 0.6f);
                    Shadow(material, new Color(0f, 0f, 0f, 0.8f), 0.6f);
                    break;
                case "label":
                    Outline(material, navy, 0.2f);
                    Shadow(material, new Color(0f, 0f, 0f, 0.9f), 0.7f);
                    break;
                case "block":
                    Outline(material, new Color(1f, 1f, 1f, 0.85f), 0.06f);
                    break;
                case "plain":
                    // 白い板の上の文字など、飾りを付けないもの
                    break;
                default:
                    Shadow(material, new Color(0f, 0f, 0f, 0.85f), 0.5f);
                    break;
            }

            return Materials[style] = material;
        }

        private static void Outline(Material material, Color color, float width)
        {
            material.EnableKeyword("OUTLINE_ON");
            material.SetColor(ShaderUtilities.ID_OutlineColor, color);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            // 縁取りの分だけ文字を少し太らせて、細い線がつぶれないようにする
            material.SetFloat(ShaderUtilities.ID_FaceDilate, width * 0.5f);
        }

        private static void Glow(Material material, Color color, float outer)
        {
            material.EnableKeyword("GLOW_ON");
            material.SetColor(ShaderUtilities.ID_GlowColor, color);
            material.SetFloat(ShaderUtilities.ID_GlowOffset, 0f);
            material.SetFloat(ShaderUtilities.ID_GlowInner, 0.05f);
            material.SetFloat(ShaderUtilities.ID_GlowOuter, outer);
            material.SetFloat(ShaderUtilities.ID_GlowPower, 0.8f);
        }

        private static void Shadow(Material material, Color color, float offset)
        {
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor(ShaderUtilities.ID_UnderlayColor, color);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, offset);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -offset);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.1f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
        }
    }
}
