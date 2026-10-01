using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 玉がレールや壁に当たったときの音。当たりが強いほど高く大きく鳴る。
    /// </summary>
    public sealed class MarbleSound : MonoBehaviour
    {
        private void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed > 0.7f)
            {
                Sfx.Play("tick", Mathf.Lerp(0.8f, 1.5f, speed / 4f), Mathf.Clamp01(speed / 3f), 0.05f);
            }
        }
    }
}
