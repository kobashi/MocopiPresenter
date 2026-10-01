using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 一定の速さで回り続け、ゆっくり上下に揺れる（柱の上に浮かぶ輪などの飾り用）。
    /// </summary>
    public sealed class Spinner : MonoBehaviour
    {
        public Vector3 DegreesPerSecond = new Vector3(0f, 60f, 0f);
        public float Bob = 0.03f;
        public float BobSpeed = 1.2f;

        private Vector3 home;

        private void Start()
        {
            this.home = this.transform.localPosition;
        }

        private void Update()
        {
            this.transform.Rotate(this.DegreesPerSecond * Time.deltaTime, Space.Self);
            this.transform.localPosition = this.home + Vector3.up * Mathf.Sin(Time.time * this.BobSpeed + this.home.x) * this.Bob;
        }
    }
}
