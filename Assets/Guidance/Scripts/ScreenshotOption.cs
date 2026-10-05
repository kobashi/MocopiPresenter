using System;
using System.Collections;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 表示確認用。起動引数に「-guide-shot 出力.png」を付けると、案内を表示した画面を画像に保存して終了する。
    /// </summary>
    public sealed class ScreenshotOption : MonoBehaviour
    {
        public QrGuide Guide;

        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-guide-shot");
            if (index < 0 || index + 1 >= args.Length)
            {
                yield break;
            }

            // 最初の場面が出て案内が閉じられた後に開く
            yield return null;
            this.Guide.Visible = true;
            // 「-guide-mirror on」「-guide-mirror off」で左右反転を指定できる（指定しなければ保存されている設定のまま）
            int mirror = Array.IndexOf(args, "-guide-mirror");
            if (mirror >= 0 && mirror + 1 < args.Length)
            {
                DisplayRouter.SetMirror(args[mirror + 1] == "on");
            }

            if (Array.IndexOf(args, "-open-menu") >= 0)
            {
                FindFirstObjectByType<PresentationMenu>().Open();
            }

            yield return new WaitForSeconds(1f);
            ScreenCapture.CaptureScreenshot(args[index + 1]);
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }
    }
}
