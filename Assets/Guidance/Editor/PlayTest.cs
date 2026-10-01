using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Guidance.EditorTools
{
    /// <summary>
    /// ビルドせずに動きを確かめるための仕組み。バッチモードで再生を始め、決めた時刻に操作と画像の書き出しを行って終了する。
    /// 実行例: Unity -batchmode -projectPath . -executeMethod Guidance.EditorTools.PlayTest.Blocks
    /// 画像は Build/test-*.png に出る。
    /// </summary>
    public static class PlayTest
    {
        private static readonly List<(float time, Action action)> Steps = new List<(float, Action)>();
        private static int next;
        private static float started = -1f;
        private static RenderTexture target;

        /// <summary>
        /// 場面1：積み木が降って積み上がり、蹴ると飛ぶことを確かめる。
        /// </summary>
        public static void Blocks()
        {
            Rigidbody foot = null;
            TitleBlocks blocks = null;
            Transform avatar = null;
            void Report(string name)
            {
                blocks = UnityEngine.Object.FindFirstObjectByType<TitleBlocks>();
                Console.WriteLine("PLAYTEST " + name + ": blocks=" + blocks.BlockCount + " jumps=" + blocks.Jumps + " sounds=" + Sfx.ClipCount);
                Shot(name);
            }

            // ジャンプするまでは降ってこない
            At(1.5f, () => Report("blocks-0-waiting"));
            // アバターごと持ち上げて、ジャンプの代わりにする
            At(1.6f, () =>
            {
                avatar = blocks.Avatar.transform;
                avatar.position += Vector3.up * 0.3f;
            });
            At(1.9f, () => avatar.position += Vector3.down * 0.3f);
            At(2.3f, () => Report("blocks-1-dropping"));
            At(4.5f, () => Report("blocks-2-stacked"));
            // 近くから見た絵（質感の確認用）
            At(4.52f, () =>
            {
                CameraDirector director = Camera.main.GetComponent<CameraDirector>();
                director.enabled = false;
                Camera.main.transform.SetPositionAndRotation(new Vector3(-0.6f, 0.75f, 2.0f), Quaternion.LookRotation(new Vector3(-0.35f, -0.4f, -1.1f)));
                Shot("blocks-2b-close");
                director.enabled = true;
            });
            // 手の代わりの球を、上の段の積み木に上から振り下ろす
            Rigidbody hand = null;
            At(4.6f, () => hand = Kicker(new Vector3(-0.95f, 1.2f, 0.9f), true));
            for (int i = 1; i <= 10; i++)
            {
                float y = 1.2f - i * 0.07f;
                At(4.6f + i * 0.02f, () => hand.MovePosition(new Vector3(-0.95f, y, 0.9f)));
            }

            At(4.9f, () => Report("blocks-3-punched"));
            At(4.95f, () => UnityEngine.Object.Destroy(hand.gameObject));
            At(6.0f, () => foot = Kicker(new Vector3(-0.95f, 0.2f, 0.3f), false));
            for (int i = 1; i <= 20; i++)
            {
                float z = 0.3f + i * 0.06f;
                At(6.0f + i * 0.02f, () => foot.MovePosition(new Vector3(-0.95f, 0.2f, z)));
            }

            At(6.6f, () => Report("blocks-4-kicked"));
            At(9f, () => Report("blocks-5-after"));
            Run(0);
        }

        /// <summary>
        /// 場面2：未完成のうちは玉がこぼれ、部品をはめると回り続けることを確かめる。
        /// </summary>
        public static void Marbles()
        {
            MarbleMachine machine = null;
            void Report(string name)
            {
                machine = UnityEngine.Object.FindFirstObjectByType<MarbleMachine>();
                int inside = 0;
                foreach (Rigidbody body in machine.GetComponentsInChildren<Rigidbody>())
                {
                    Vector3 local = machine.transform.InverseTransformPoint(body.position);
                    if (body.name.StartsWith("MarbleTemplate") && local.y > 0.06f && local.x < 2.5f)
                    {
                        inside++;
                    }
                }

                Console.WriteLine("PLAYTEST " + name + ": installed=" + machine.Installed + " laps=" + machine.Laps + " insideCourse=" + inside);
                Shot(name);
            }

            At(3f, () => Report("marbles-1-spilling"));
            At(9f, () => Report("marbles-2-all-spilled"));
            At(9.2f, () => machine.Install());
            At(14f, () => Report("marbles-3-installed"));
            At(30f, () => Report("marbles-4-looping"));
            At(60f, () => Report("marbles-5-still-looping"));
            Run(1);
        }

        /// <summary>
        /// 場面3：回転式スタンドが手で押すと回り、面が正面に来るたびに項目が書き足されることを確かめる。
        /// </summary>
        public static void Stand()
        {
            BookStand stand = null;
            Rigidbody hand = null;
            void Report(string name)
            {
                stand = UnityEngine.Object.FindFirstObjectByType<BookStand>();
                Console.WriteLine("PLAYTEST " + name + ": index=" + stand.Index + " seen=" + stand.SeenCount
                    + " yaw=" + stand.Drum.transform.localEulerAngles.y.ToString("0")
                    + " body=[" + stand.Body.text.Replace("\n", " | ") + "] note=[" + stand.Note.text + "]");
                Shot(name);
            }

            At(1.5f, () => Report("stand-1-start"));
            // 手の代わりの球で、正面の面を横に払う。1回目は右から左へ、2回目は逆向きに払う（どちらでも次へ進むはず）
            for (int swipe = 0; swipe < 2; swipe++)
            {
                float begin = 1.6f + swipe * 1.6f;
                float from = swipe == 0 ? -0.7f : 0.5f;
                float to = swipe == 0 ? 0.5f : -0.7f;
                At(begin, () => hand = Kicker(new Vector3(from, 1.3f, 0.67f), true));
                for (int i = 1; i <= 15; i++)
                {
                    float x = Mathf.Lerp(from, to, i / 15f);
                    At(begin + i * 0.02f, () => hand.MovePosition(new Vector3(x, 1.3f, 0.67f)));
                }

                At(begin + 0.35f, () => UnityEngine.Object.Destroy(hand.gameObject));
                string name = "stand-" + (swipe + 2) + "-after-swipe";
                At(begin + 1.3f, () => Report(name));
            }

            for (int i = 0; i < 3; i++)
            {
                At(5.2f + i * 0.8f, () => stand.Turn(1));
            }

            At(8.2f, () => Report("stand-4-all-shown"));
            Run(2);
        }

        /// <summary>
        /// 場面4：言葉の積み木が降って舞台を埋めることを確かめる。
        /// </summary>
        public static void Rain()
        {
            At(3f, () => Shot("rain-1"));
            At(11f, () => Shot("rain-2"));
            Run(3);
        }

        /// <summary>
        /// 場面5：バグが押し寄せ、鞭でエージェントが働いて片付けることを確かめる。
        /// </summary>
        public static void Agents()
        {
            AgentScene scene = null;
            Transform hand = null;
            void Report(string name)
            {
                scene = UnityEngine.Object.FindFirstObjectByType<AgentScene>();
                Console.WriteLine("PLAYTEST " + name + ": bugs=" + scene.BugCount + " killed=" + scene.Killed + " cracks=" + scene.Cracks + " maxTipSinceLast=" + scene.MaxTipSpeed.ToString("0.0"));
                scene.MaxTipSpeed = 0f;
                Shot(name);
            }

            At(8f, () => Report("agents-1-swarm"));
            // 手の代わりの点を、振りかぶった位置からエージェントのほうへ素早く振り下ろす
            var rest = new Vector3(-0.65f, 1.2f, 0f);
            var raised = new Vector3(-1.2f, 1.9f, -0.3f);
            var struck = new Vector3(-0.3f, 1.0f, -0.3f);
            At(8.1f, () =>
            {
                hand = new GameObject("TestHand").transform;
                hand.position = rest;
                scene.HandOverride = hand;
            });
            for (int i = 1; i <= 40; i++)
            {
                float k = i / 40f;
                At(8.2f + k * 1.0f, () =>
                {
                    hand.position = Vector3.Lerp(rest, raised, k);
                    hand.rotation = Quaternion.LookRotation(Vector3.Slerp(Vector3.down, new Vector3(-0.4f, 1f, 0f).normalized, k));
                });
            }

            At(9.5f, () => Report("agents-2-whip-raised"));
            for (int i = 1; i <= 12; i++)
            {
                float k = i / 12f;
                At(9.5f + k * 0.25f, () =>
                {
                    // 腕を振り下ろしながら、柄の向きも「後ろ上」から「前」へ返す
                    hand.position = Vector3.Lerp(raised, struck, k);
                    hand.rotation = Quaternion.LookRotation(Vector3.Slerp(new Vector3(-0.4f, 1f, 0f).normalized, new Vector3(1f, -0.2f, 0f).normalized, k));
                });
            }

            At(9.72f, () => Report("agents-3-whip-striking"));
            At(10.1f, () => Report("agents-3b-after-whip"));
            At(11.5f, () => Report("agents-4-shooting"));
            At(16f, () => Report("agents-5-cleared"));
            At(24f, () => Report("agents-6-asleep-again"));
            Run(4);
        }

        /// <summary>
        /// アバターの体の一部と同じ扱いの球（蹴りの代わり）
        /// </summary>
        public static Rigidbody Kicker(Vector3 position, bool hand)
        {
            var part = new GameObject("TestKicker");
            part.AddComponent<AvatarColliders.Part>().Hand = hand;
            part.transform.position = position;
            part.AddComponent<SphereCollider>().radius = 0.12f;
            Rigidbody body = part.AddComponent<Rigidbody>();
            body.isKinematic = true;
            return body;
        }

        public static void At(float time, Action action)
        {
            Steps.Add((time, action));
        }

        public static void Shot(string name)
        {
            Camera camera = Camera.main;
            if (target == null)
            {
                target = new RenderTexture(1280, 720, 24);
            }

            Rect rect = camera.rect;
            RenderTexture before = camera.targetTexture;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            camera.targetTexture = before;
            camera.rect = rect;
            RenderTexture.active = null;
            Directory.CreateDirectory("Build");
            File.WriteAllBytes("Build/test-" + name + ".png", image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }

        /// <summary>
        /// 再生を始め、指定の場面を出してから手順を順に実行する。
        /// </summary>
        public static void Run(int slide)
        {
            EditorSceneManager.OpenScene("Assets/Guidance/Scenes/Guidance.unity");
            // 再生開始時にスクリプトを読み直さない（この手順の一覧が消えないように）
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            Steps.Sort((a, b) => a.time.CompareTo(b.time));
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (type == LogType.Exception)
                {
                    Console.WriteLine("PLAYTEST EXCEPTION: " + message + "\n" + stack);
                }
            };

            EditorApplication.update += () =>
            {
                try
                {
                    if (!EditorApplication.isPlaying)
                    {
                        return;
                    }

                    if (started < 0f)
                    {
                        // 最初のフレームで各スクリプトの Start が済んでから場面を出す
                        if (Time.frameCount < 3)
                        {
                            return;
                        }

                        started = Time.time;
                        UnityEngine.Object.FindFirstObjectByType<SlideDeck>().Show(slide);
                    }

                    while (next < Steps.Count && Time.time - started >= Steps[next].time)
                    {
                        Steps[next++].action();
                    }

                    if (next >= Steps.Count)
                    {
                        EditorSettings.enterPlayModeOptionsEnabled = false;
                        EditorApplication.Exit(0);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("PLAYTEST FAILED: " + e);
                    EditorSettings.enterPlayModeOptionsEnabled = false;
                    EditorApplication.Exit(1);
                }
            };
            EditorApplication.EnterPlaymode();
        }
    }
}
