using System.Collections.Generic;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// アバターの手足や体に当たり判定を付け、物理で動く物を押したり蹴ったりできるようにする。
    /// 骨に直接付けず、骨を追いかける別の剛体にしている（動きの速さが物理に伝わり、蹴った勢いで物が飛ぶ）。
    /// </summary>
    public sealed class AvatarColliders : MonoBehaviour
    {
        /// <summary>
        /// アバターの体の一部であることを示す目印
        /// </summary>
        public sealed class Part : MonoBehaviour
        {
            // 手と前腕なら true（叩く側。足や体は蹴る・押す側）
            public bool Hand;
        }

        // 手と前腕の当たりの半径（m）。実際の手より大きめにして、仕掛けに触れやすくしている
        public float HandRadius = 0.16f;
        public float ForearmRadius = 0.1f;

        private readonly List<(Rigidbody body, Transform a, Transform b)> parts = new List<(Rigidbody, Transform, Transform)>();

        private void Start()
        {
            Animator animator = this.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            Transform root = new GameObject("AvatarColliders").transform;

            void Add(HumanBodyBones from, HumanBodyBones to, float radius)
            {
                Transform a = animator.GetBoneTransform(from);
                Transform b = animator.GetBoneTransform(to);
                if (a == null)
                {
                    return;
                }

                var part = new GameObject(from.ToString());
                part.AddComponent<Part>().Hand = from == HumanBodyBones.LeftHand || from == HumanBodyBones.RightHand
                    || from == HumanBodyBones.LeftLowerArm || from == HumanBodyBones.RightLowerArm;
                part.transform.SetParent(root, false);
                part.AddComponent<SphereCollider>().radius = radius;
                Rigidbody body = part.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                this.parts.Add((body, a, b != null ? b : a));
                body.position = this.Target(this.parts.Count - 1);
            }

            // 2つの骨の中間に置く（同じ骨を指定するとその骨の位置）
            Add(HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, 0.11f);
            Add(HumanBodyBones.RightFoot, HumanBodyBones.RightToes, 0.11f);
            Add(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.09f);
            Add(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.09f);
            Add(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.11f);
            Add(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.11f);
            Add(HumanBodyBones.LeftHand, HumanBodyBones.LeftHand, this.HandRadius);
            Add(HumanBodyBones.RightHand, HumanBodyBones.RightHand, this.HandRadius);
            Add(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, this.ForearmRadius);
            Add(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, this.ForearmRadius);
            Add(HumanBodyBones.Hips, HumanBodyBones.Hips, 0.17f);
            Add(HumanBodyBones.Chest, HumanBodyBones.Chest, 0.16f);
            Add(HumanBodyBones.Head, HumanBodyBones.Head, 0.14f);
        }

        private void FixedUpdate()
        {
            for (int i = 0; i < this.parts.Count; i++)
            {
                this.parts[i].body.MovePosition(this.Target(i));
            }
        }

        private Vector3 Target(int index)
        {
            return (this.parts[index].a.position + this.parts[index].b.position) * 0.5f;
        }
    }
}
