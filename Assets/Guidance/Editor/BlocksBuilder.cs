using UnityEngine;

namespace Guidance.EditorTools
{
    /// <summary>
    /// 文字の積み木の仕掛け（Id は "blocks"）を作る。
    /// </summary>
    public static class BlocksBuilder
    {
        [GimmickBuilder]
        public static Gimmick Build(GimmickContext context)
        {
            // 積み木のひな形。最初の子が見た目（大きさは実行時に積み木ごとに決める）
            var template = new GameObject("BlockTemplate");
            template.transform.SetParent(context.Stage, false);
            StageBuilder.Shape(template.transform, "Body", Vector3.zero, MeshKit.ChamferBox(Vector3.one * 0.27f, 0.028f), StageBuilder.Lit("Block", Color.white, Color.white, 0.65f));
            template.AddComponent<BoxCollider>();
            Rigidbody body = template.AddComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            template.AddComponent<LetterBlock>();
            template.SetActive(false);

            var blocks = new GameObject("TitleBlocks").AddComponent<TitleBlocks>();
            blocks.transform.SetParent(context.Stage, false);
            blocks.Id = "blocks";
            blocks.Template = template;
            blocks.Sparks = context.Sparks;
            blocks.Avatar = context.Avatar;
            return blocks;
        }
    }
}
