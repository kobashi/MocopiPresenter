using System;
using UnityEngine;

namespace Guidance.EditorTools
{
    /// <summary>
    /// 仕掛けを組み立てるメソッドに付ける目印。舞台を作るとき（StageBuilder）に、この目印の付いたメソッドが全部呼ばれる。
    /// メソッドは「public static Gimmick 名前(GimmickContext context)」の形にする。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class GimmickBuilderAttribute : Attribute
    {
    }

    /// <summary>
    /// 仕掛けを組み立てるときに渡す、舞台の共通の部品
    /// </summary>
    public sealed class GimmickContext
    {
        // 仕掛けを置く親（舞台）
        public Transform Stage;
        // 火花のパーティクル（Emit で位置と色を指定して出す）
        public ParticleSystem Sparks;
        // 加算合成の光の粒の素材（軌跡などに使う）
        public Material Particle;
        // アバター（手や腰の位置を見るのに使う）
        public Animator Avatar;
    }
}
