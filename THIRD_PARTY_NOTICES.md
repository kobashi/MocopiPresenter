# 同梱しているサードパーティ製ソフトウェア

このリポジトリと配布するアプリには、次のソフトウェアが含まれています。
それぞれのライセンスは、このリポジトリの MIT ライセンスではなく、各ソフトウェアのライセンスに従います。

## mocopi Receiver Plugin for Unity（v1.1.0）

- 場所：`Assets/MocopiReceiver/`
- 入手元：https://github.com/sony/mocopi-receiver-plugin-unity
- ライセンス：Apache License 2.0（全文は `Assets/MocopiReceiver/LICENSE` を参照）
- 同梱のサンプルアバター `MocopiAvatar.fbx` も、このプラグインの一部として含まれています。
- mocopi のロゴとアプリアイコンは含まれていません（プラグインの配布物にも含まれていません）。

NOTICE（原文のまま）：

```
mocopi Receiver Plugin for Unity
Copyright 2025 Sony Corporation

This product includes software developed at
Sony Corporation　(https://github.com/sony/mocopi-receiver-plugin-unity).
```

## UniVRM（v0.131.2）

- 場所：`Packages/com.vrmc.univrm/`、`Packages/com.vrmc.gltf/`
- 入手元：https://github.com/vrm-c/UniVRM
- ライセンス：MIT License（各フォルダの `LICENSE.md` を参照）
- VRM 形式のアバターを読み込むために使います。

## TextMesh Pro Essential Resources

- 場所：`Assets/TextMesh Pro/`
- Unity の uGUI パッケージに含まれるリソースです（Unity Companion License）。
- 含まれるフォント LiberationSans は SIL Open Font License 1.1 です（`Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`）。

## 含まれていないもの

- **RAYNOSちゃん（mocopi 公式アバター）**：Sony の「キャラクター使用許諾契約書」で再配布が禁止されているため、このリポジトリにも配布するアプリにも含めていません。使う場合は各自で入手してください（README の「アバターを RAYNOS に差し替える」を参照）。
- **日本語フォント**：アプリは実行時に OS に入っているフォント（Windows は游ゴシック・メイリオなど、macOS はヒラギノ）を使います。フォントファイルは同梱していません。
