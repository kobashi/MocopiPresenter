using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// アバターの頭の向きを読む道具（VR の仕掛けで共通に使う）。
    /// 頭の骨の向きは体のモデルごとにまちまちなので、「正面を向いていたときの頭の向き」を覚えておき、
    /// そこからどれだけ回ったか（Turn）で扱う。正面は Calibrate() で決め直せる。
    /// ゴーグルの模型をアバターの顔の前に付ける・外すこともここで行う。
    /// </summary>
    public sealed class HeadPose
    {
        private readonly Animator avatar;
        private Quaternion reference = Quaternion.identity;

        public HeadPose(Animator avatar)
        {
            this.avatar = avatar;
            this.Head = avatar != null && avatar.isHuman ? avatar.GetBoneTransform(HumanBodyBones.Head) : null;
            this.Calibrate();
        }

        /// <summary>
        /// 頭の骨（アバターが無ければ null）
        /// </summary>
        public Transform Head { get; }

        /// <summary>
        /// 正面を向いていたときから、頭がどれだけ回ったか（世界の向きで測る）
        /// </summary>
        public Quaternion Turn => this.Head != null ? this.Head.rotation * Quaternion.Inverse(this.reference) : Quaternion.identity;

        /// <summary>
        /// 正面を決めたときの体の向き
        /// </summary>
        public Quaternion Body { get; private set; } = Quaternion.identity;

        /// <summary>
        /// 顔が向いている方向
        /// </summary>
        public Vector3 Forward => this.Turn * (this.Body * Vector3.forward);

        /// <summary>
        /// 目の位置（頭の骨から少し前・上）
        /// </summary>
        public Vector3 Eye => this.Head != null ? this.Head.position + this.Turn * (this.Body * new Vector3(0f, 0.04f, 0.1f)) : Vector3.up * 1.5f;

        /// <summary>
        /// 今の頭の向きを正面にする
        /// </summary>
        public void Calibrate()
        {
            if (this.Head != null)
            {
                this.reference = this.Head.rotation;
                this.Body = this.avatar.transform.rotation;
            }
        }

        /// <summary>
        /// ゴーグルの模型を顔の前に付ける（正面を向いているとして付け、あとは頭と一緒に動く）。wear が false なら外して owner の下へ戻す
        /// </summary>
        public void Wear(Transform goggle, bool wear, Transform owner)
        {
            if (goggle == null)
            {
                return;
            }

            if (wear && this.Head != null)
            {
                goggle.SetParent(this.Head, true);
                goggle.SetPositionAndRotation(this.Eye, this.Turn * this.Body);
                goggle.gameObject.SetActive(true);
            }
            else
            {
                goggle.SetParent(owner, false);
                goggle.gameObject.SetActive(false);
            }
        }
    }
}
