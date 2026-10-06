using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 家具の目印（空間アンカーの場面で、家具として見つけて箱で囲む物）。子の当たり判定をまとめて1つの家具として扱う
    /// </summary>
    public sealed class Furniture : MonoBehaviour
    {
        // 見つけたときに出す名前
        public string Label = "";

        /// <summary>
        /// 子の当たり判定を全部囲む箱
        /// </summary>
        public Bounds Bounds
        {
            get
            {
                Collider[] colliders = this.GetComponentsInChildren<Collider>();
                if (colliders.Length == 0)
                {
                    return new Bounds(this.transform.position, Vector3.zero);
                }

                Bounds bounds = colliders[0].bounds;
                foreach (Collider part in colliders)
                {
                    bounds.Encapsulate(part.bounds);
                }

                return bounds;
            }
        }
    }
}
