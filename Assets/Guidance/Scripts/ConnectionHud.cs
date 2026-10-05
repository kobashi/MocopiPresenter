using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Mocopi.Receiver;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// mocopi アプリに入力する「送信先 IP アドレスとポート」と、受信状況を画面左上に表示する。
    /// H キーで表示を切り替える（本番中は隠せるように）。
    /// </summary>
    public sealed class ConnectionHud : MonoBehaviour
    {
        public MocopiSimpleReceiver Receiver;
        public bool Visible = true;

        private readonly List<string> addresses = new List<string>();
        private GUIStyle style;
        private SlideDeck deck;

        private void Start()
        {
            this.RefreshAddresses();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                this.Visible = !this.Visible;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                this.RefreshAddresses();
            }

            if (Input.GetKeyDown(KeyCode.Escape) && !PresentationMenu.UsedEscape)
            {
                Application.Quit();
            }
        }

        private void RefreshAddresses()
        {
            this.addresses.Clear();
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                foreach (UnicastIPAddressInformation info in nic.GetIPProperties().UnicastAddresses)
                {
                    if (info.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        this.addresses.Add(info.Address.ToString());
                    }
                }
            }
        }

        private static string Name(SlideDeck.Slide slide)
        {
            return !string.IsNullOrEmpty(slide.title) ? slide.title : slide.big;
        }

        private void OnGUI()
        {
            // プレゼンの一覧を開いている間は、重ならないよう隠す
            if (!this.Visible || PresentationMenu.IsOpen)
            {
                return;
            }

            if (this.style == null)
            {
                this.style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true };
                this.style.padding = new RectOffset(16, 16, 12, 12);
            }

            this.style.fontSize = Mathf.Max(14, Screen.height / 50);

            var text = new System.Text.StringBuilder();
            text.AppendLine("<b>mocopi アプリの送信先</b>");
            text.AppendLine("IP: " + (this.addresses.Count > 0 ? string.Join(" / ", this.addresses) : "ネットワーク未接続"));

            if (this.Receiver != null)
            {
                foreach (MocopiSimpleReceiver.MocopiSimpleReceiverAvatarSettings settings in this.Receiver.AvatarSettings)
                {
                    float rate = settings.MocopiAvatar != null ? settings.MocopiAvatar.FrameArrivalRate : 0f;
                    string status = rate > 0f
                        ? "<color=#66ff66>受信中 " + rate.ToString("0") + " fps</color>"
                        : "<color=#ffcc33>待機中</color>";
                    text.AppendLine("ポート: " + settings.Port + "（UDP） " + status);
                }
            }

            text.AppendLine(DisplayRouter.Dual
                ? "<b>表示</b>　プロジェクター：2台目　手元の画面：鏡合わせ " + (DisplayRouter.Mirror ? "<color=#66ff66>ON</color>" : "OFF")
                : "<b>表示</b>　<color=#ffcc33>モニター1台</color>（2台目をつないでから起動すると、プロジェクターに全画面で出ます）　左右反転 " + (DisplayRouter.Mirror ? "ON" : "OFF"));

            if (this.deck == null)
            {
                this.deck = FindFirstObjectByType<SlideDeck>();
            }

            if (this.deck != null && this.deck.Slides.Length > 0)
            {
                int now = this.deck.Current;
                string next = now + 1 < this.deck.Slides.Length ? Name(this.deck.Slides[now + 1]) : "（最後）";
                text.AppendLine("<b>プレゼン</b>　" + this.deck.PresentationName);
                text.AppendLine("<b>場面</b>　" + (now + 1) + " / " + this.deck.Slides.Length + "　" + Name(this.deck.Slides[now]) + "　→ 次：" + next);
            }

            text.AppendLine("<b>操作</b>");
            text.AppendLine("→ / Space / PageDown: 次の場面　　← / PageUp: 前の場面　　Home: 最初の場面");
            text.AppendLine("1〜4: カメラ（全体 / 全身 / 上半身 / スクリーン）");
            text.AppendLine("J / B: 積み木を降らせる・積み直す（場面1）");
            text.AppendLine("M: 部品をはめる・戻す（場面2）");
            text.AppendLine("T: スタンドを1面回す　Shift+T: 逆回り（場面3）");
            text.AppendLine("W: エージェントを働かせる（場面5）");
            text.AppendLine("VR体験会　C: 頭の正面合わせ　A / D: 向きを左右に回す　G: カメラの台数　L: 点を消す　X: VR ⇔ MR　K: 物を置く　Y: アイコンを跳ねさせる");
            text.AppendLine("Q: ゲーム案内（QR）　　S: 音のオン・オフ　　F: 手元の画面の鏡合わせ　　P: プレゼンの切り替え");
            text.Append("H: この表示を隠す・出す　　R: IP再取得　　Esc: 終了");

            var content = new GUIContent(text.ToString());
            Vector2 size = this.style.CalcSize(content);
            GUI.Box(new Rect(16, 16, size.x, size.y), content, this.style);
        }
    }
}
