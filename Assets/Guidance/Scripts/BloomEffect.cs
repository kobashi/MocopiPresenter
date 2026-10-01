using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 明るい部分をにじませて光って見せる（ブルーム）。カメラに付ける。
    /// 光る素材は 1 を超える明るさで描いてあり、Threshold を超えた分だけがにじむ（文字やアバターはにじまない）。
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public sealed class BloomEffect : MonoBehaviour
    {
        public Shader Shader;
        [Range(0f, 3f)] public float Threshold = 1.15f;
        [Range(0f, 3f)] public float Intensity = 0.8f;
        [Range(1, 8)] public int Iterations = 6;

        private readonly RenderTexture[] textures = new RenderTexture[8];
        private Material material;

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (this.Shader == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (this.material == null)
            {
                this.material = new Material(this.Shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            this.material.SetFloat("_Threshold", this.Threshold);
            this.material.SetFloat("_Intensity", this.Intensity);

            // 半分ずつ小さくしながらぼかし、逆順に足し戻す
            int width = source.width / 2;
            int height = source.height / 2;
            RenderTexture current = this.textures[0] = RenderTexture.GetTemporary(width, height, 0, source.format);
            Graphics.Blit(source, current, this.material, 0);

            int count = 1;
            for (; count < this.Iterations; count++)
            {
                width /= 2;
                height /= 2;
                if (height < 2 || width < 2)
                {
                    break;
                }

                RenderTexture next = this.textures[count] = RenderTexture.GetTemporary(width, height, 0, source.format);
                Graphics.Blit(current, next, this.material, 1);
                current = next;
            }

            for (int i = count - 2; i >= 0; i--)
            {
                Graphics.Blit(current, this.textures[i], this.material, 2);
                RenderTexture.ReleaseTemporary(current);
                current = this.textures[i];
            }

            this.material.SetTexture("_BloomTex", current);
            Graphics.Blit(source, destination, this.material, 3);
            RenderTexture.ReleaseTemporary(current);
        }
    }
}
