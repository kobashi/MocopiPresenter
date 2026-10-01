using TMPro;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 文字の書かれた積み木1個。アバターに蹴られると勢いよく飛び、火花を出す。
    /// </summary>
    public sealed class LetterBlock : MonoBehaviour
    {
        private static readonly Vector3[] Faces = { Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.left, Vector3.right };

        public TitleBlocks Owner;
        public Color Color;

        private Rigidbody body;
        private float lastKick = -10f;

        public Rigidbody Body => this.body != null ? this.body : this.body = this.GetComponent<Rigidbody>();

        /// <summary>
        /// 大きさと色を決め、面に文字を貼る。allFaces が false なら左右の面には貼らない（横長の積み木用）。
        /// </summary>
        public void Setup(string text, Color color, TMP_FontAsset font, Vector3 size, bool allFaces)
        {
            this.Color = color;
            this.GetComponent<BoxCollider>().size = size;
            this.Body.mass = Mathf.Max(0.1f, size.x * size.y * size.z * 15f);

            // 角を面取りした箱にして、かどに光が乗るようにする
            Transform visual = this.transform.GetChild(0);
            visual.localScale = Vector3.one;
            visual.GetComponent<MeshFilter>().sharedMesh = MeshKit.ChamferBox(size, 0.028f);
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", color * 0.18f);
            visual.GetComponent<Renderer>().SetPropertyBlock(block);

            for (int i = 0; i < (allFaces ? 6 : 4); i++)
            {
                Vector3 face = Faces[i];
                // その面の横幅・縦幅と、中心から面までの距離
                Vector2 rect = face.z != 0f ? new Vector2(size.x, size.y) : face.y != 0f ? new Vector2(size.x, size.z) : new Vector2(size.z, size.y);
                float offset = face.z != 0f ? size.z : face.y != 0f ? size.y : size.x;

                var label = new GameObject("Letter", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
                label.gameObject.hideFlags = HideFlags.DontSave;
                label.transform.SetParent(this.transform, false);
                label.transform.localPosition = face * (offset * 0.5f + 0.002f);
                // TextMeshPro の文字は、自分の +Z の向きに見たときに読める
                label.transform.localRotation = Quaternion.LookRotation(-face, face.y != 0f ? Vector3.back * face.y : Vector3.up);
                label.rectTransform.sizeDelta = rect * 0.92f;
                label.text = text;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.enableAutoSizing = true;
                label.fontSizeMin = 0.1f;
                label.fontSizeMax = rect.y * 8.5f;
                JapaneseFont.Style(label, "block", new Color(0.1f, 0.12f, 0.3f), new Color(0.02f, 0.02f, 0.08f));
                label.ForceMeshUpdate();
            }
        }

        /// <summary>
        /// 爆発の破片用。文字は貼らず、大きさと色だけ決める。
        /// </summary>
        public void SetupChip(Color color, float size)
        {
            this.Color = color;
            this.GetComponent<BoxCollider>().size = Vector3.one * size;
            this.Body.mass = 0.02f;
            Transform visual = this.transform.GetChild(0);
            visual.localScale = Vector3.one;
            visual.GetComponent<MeshFilter>().sharedMesh = MeshKit.ChamferBox(Vector3.one * size, size * 0.15f);
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", color * 0.8f);
            visual.GetComponent<Renderer>().SetPropertyBlock(block);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (this.Owner == null || collision.contactCount == 0)
            {
                return;
            }

            Vector3 point = collision.GetContact(0).point;
            float speed = collision.relativeVelocity.magnitude;
            AvatarColliders.Part part = collision.collider.GetComponent<AvatarColliders.Part>();
            bool avatar = part != null;

            if (avatar && part.Hand && speed > this.Owner.PunchThreshold)
            {
                // 手で叩かれたら爆発する
                this.Owner.Explode(this);
            }
            else if (avatar && speed > this.Owner.KickThreshold && Time.time - this.lastKick > 0.25f)
            {
                // 蹴られた向きに、実際より勢いを足して飛ばす
                this.lastKick = Time.time;
                Vector3 direction = (this.transform.position - point).normalized + Vector3.up * 0.5f;
                float power = Mathf.Clamp(speed, 1.5f, 6f) * this.Owner.KickPower;
                this.Body.AddForce(direction.normalized * power * this.Body.mass, ForceMode.Impulse);
                this.Body.AddTorque(Random.onUnitSphere * power * 0.05f * this.Body.mass, ForceMode.Impulse);
                this.Owner.Burst(point, this.Color, 40);
                Sfx.Play("kick", Random.Range(0.85f, 1.2f), 1f, 0.08f);
            }
            else if (speed > 3.5f)
            {
                this.Owner.Burst(point, this.Color, 5);
                Sfx.Play("thud", Random.Range(0.8f, 1.3f), 0.6f, 0.07f);
            }
        }
    }
}
