using System;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 数字キー（1, 2, 3, ...）でカメラの画角を切り替える。切り替えは滑らかに移動する。
    /// FollowAvatar が有効な画角は、アバターが歩いても追いかける。
    /// </summary>
    public sealed class CameraDirector : MonoBehaviour
    {
        [Serializable]
        public sealed class Shot
        {
            public string Name;
            // FollowAvatar が有効なときは、アバターの足元からの相対位置
            public Vector3 Position;
            public Vector3 LookAt;
            public float FieldOfView = 45f;
            public bool FollowAvatar;
        }

        public Camera Camera;
        public Animator Avatar;
        public Shot[] Shots = new Shot[0];
        public int Current;
        // 大きいほど素早く切り替わる
        public float Speed = 4f;

        private Vector3 anchor;
        private Vector3 anchorVelocity;

        private void Start()
        {
            this.anchor = this.AvatarGround();
            this.Snap(this.Current);
        }

        private void Update()
        {
            for (int i = 0; i < this.Shots.Length && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    this.Current = i;
                }
            }
        }

        private void LateUpdate()
        {
            if (this.Shots.Length == 0)
            {
                return;
            }

            this.anchor = Vector3.SmoothDamp(this.anchor, this.AvatarGround(), ref this.anchorVelocity, 0.4f);
            this.Pose(this.Shots[this.Current], out Vector3 position, out Quaternion rotation);

            float t = 1f - Mathf.Exp(-this.Speed * Time.deltaTime);
            Transform target = this.Camera.transform;
            target.SetPositionAndRotation(Vector3.Lerp(target.position, position, t), Quaternion.Slerp(target.rotation, rotation, t));
            this.Camera.fieldOfView = Mathf.Lerp(this.Camera.fieldOfView, this.Shots[this.Current].FieldOfView, t);
        }

        /// <summary>
        /// 移動の途中を省いて、その画角にすぐ切り替える。
        /// </summary>
        public void Snap(int index)
        {
            if (index < 0 || index >= this.Shots.Length)
            {
                return;
            }

            this.Current = index;
            this.anchor = this.AvatarGround();
            this.Pose(this.Shots[index], out Vector3 position, out Quaternion rotation);
            this.Camera.transform.SetPositionAndRotation(position, rotation);
            this.Camera.fieldOfView = this.Shots[index].FieldOfView;
        }

        private void Pose(Shot shot, out Vector3 position, out Quaternion rotation)
        {
            Vector3 origin = shot.FollowAvatar ? this.anchor : Vector3.zero;
            position = origin + shot.Position;
            rotation = Quaternion.LookRotation(origin + shot.LookAt - position);
        }

        /// <summary>
        /// アバターの腰の真下（床の高さ）。腰の上下動でカメラが揺れないよう、高さは使わない。
        /// </summary>
        private Vector3 AvatarGround()
        {
            if (this.Avatar == null)
            {
                return Vector3.zero;
            }

            Transform hips = this.Avatar.isHuman ? this.Avatar.GetBoneTransform(HumanBodyBones.Hips) : null;
            Vector3 position = hips != null ? hips.position : this.Avatar.transform.position;
            return new Vector3(position.x, this.Avatar.transform.position.y, position.z);
        }
    }
}
