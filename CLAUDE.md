# CLAUDE.md — MocopiPresenter

mocopi で動くアバターが、舞台の上で物理演算の仕掛けを操作しながらプレゼンする Unity アプリ。
1つのアプリで複数のプレゼンを扱い、プレゼンごとに「場面の並び」と「使う仕掛け」を差し替える。
最初のプレゼンは名古屋文理大学 情報メディア学科「情報システムコース」のガイダンス（`guidance2026`）。

公開リポジトリ：https://github.com/kobashi/MocopiPresenter（MIT）。`main` に入れたものは公開される。

---

## 作業の約束

- **ユーザーが指示するまでビルドしない**（macOS / Windows とも。zip 作成も含む）。変更の確認は、下の「ビルドせずに確かめる」で行う。
- 文言・題材・プレゼンの内容は推測で埋めない。分からないものは質問するか、「仮」と明記して置く。
- コミットメッセージは日本語で「何を・なぜ」を書く。
- 外部のライブラリ・素材（モデル、画像、音源、フォント）は追加しない。必要なら理由を添えて質問する。形・質感・効果音は計算で作る方針。
- **RAYNOS（mocopi 公式アバター）は再配布禁止**。`Assets/Guidance/Avatars/RAYNOS/` は Git の管理対象外で、公開リポジトリにもリリースにも入れない（下の「RAYNOS とシーンファイル」を参照）。
- 説明文やコメントは、専門用語を避けた平易な日本語で書く（既存コードに合わせる）。

## 動かす環境

- Unity 6000.3.19f1（Unity 6.3 LTS、ビルトインレンダーパイプライン）。Windows Build Support 入り。
- 開発は macOS、本番は Windows ノート PC ＋ プロジェクター（2画面）。
- mocopi：センサー6個 → USB レシーバー QM-PR1 → Windows の mocopi PC アプリ → UDP（127.0.0.1:12351）→ このアプリ。
- Mac では Windows 版の動作確認ができない。Windows での確認はユーザーに依頼する。

## 全体の作り

```
Assets/
├── Guidance/
│   ├── Scripts/          実行時のスクリプト（名前空間 Guidance）
│   │   ├── SlideDeck.cs        場面の切り替え。場面ごとに仕掛けを出し入れする
│   │   ├── Presentation.cs     プレゼンの選択と読み込み
│   │   ├── Gimmick.cs          仕掛けの土台（すべての仕掛けはこれを継承）
│   │   ├── TitleBlocks.cs ほか 仕掛け（下の表）
│   │   ├── DisplayRouter.cs    プロジェクターと手元の2画面出力、手元の左右反転
│   │   ├── CameraDirector.cs   カメラの画角（数字キー）
│   │   ├── AvatarColliders.cs  アバターの手足の当たり判定（仕掛けを押す・蹴る・叩く）
│   │   ├── ConnectionHud.cs    発表者用の操作一覧（手元の画面だけに出る）
│   │   ├── Sfx.cs              効果音（起動時に合成）
│   │   ├── JapaneseFont.cs     OS の日本語フォントと文字の飾り（TextMeshPro）
│   │   ├── MeshKit.cs          面取りした箱・多角柱・輪を計算で作る
│   │   └── BloomEffect.cs      光のにじみ
│   ├── Editor/           シーンを組み立てるスクリプト（名前空間 Guidance.EditorTools）
│   │   ├── GuidanceSetup.cs    シーン作成・ビルド・プレビュー画像
│   │   ├── StageBuilder.cs     舞台（床・スクリーン・柱・照明・カメラ）と仕掛けの組み立て
│   │   ├── GimmickBuilder.cs   仕掛けを組み立てるメソッドの目印と、渡す部品
│   │   ├── *Builder.cs         仕掛けごとの組み立て
│   │   └── PlayTest.cs         ビルドせずに動きを確かめる
│   ├── Shaders/          光る素材・柱のイルミネーション・ブルーム
│   └── Scenes/Guidance.unity   スクリプトで作るシーン（手で編集しない）
└── StreamingAssets/Presentations/
    ├── selected.txt            使うプレゼンの名前
    └── guidance2026/           プレゼン1つ分（slides.json と素材）
```

- シーンは手で配置せず、`Editor/` のスクリプトで組み立てる。舞台や仕掛けを変えたら、シーンを作り直して確かめる。
- 名前空間とフォルダ名が `Guidance` なのは最初のプレゼンの名残り。変えるとシーンの参照が壊れるので、そのままにする。

## プレゼンを作る・切り替える

1. `Assets/StreamingAssets/Presentations/〈名前〉/slides.json` を作る（`guidance2026` をまねる）。
2. 画像などの素材は同じフォルダに置く（例：QR コードは `swift Tools/make-qr.swift <URL> <出力.png>`）。
3. 起動中は P キーの一覧（`PresentationMenu.cs`、手元の画面だけに出る）で切り替える。選んだものは `Presentations/selected.txt` に記録され、次回の起動に使われる（エディタでは記録しない）。起動時の引数 `-presentation 名前` でも選べる。
4. `slides.json` の先頭に `"name"` を書くと、一覧に出る名前になる。新しいプレゼンは `sample`（ひな形）をまねるとよい。

場面（`slides` の1要素）の共通項目：

| 項目 | 内容 |
|---|---|
| `title` / `big` / `lines` / `note` | スクリーンの見出し・大きな一言・本文（行の配列）・締めの一言 |
| `camera` | 場面に入ったときのカメラ（1〜4。0 なら変えない） |
| `gimmicks` | この場面で使う仕掛けの Id の配列 |

仕掛けごとの項目は、同じ場面の中に並べて書く（各仕掛けが自分の項目だけを読む）。

## 仕掛け

| Id | クラス | 場面に書く項目 | キー |
|---|---|---|---|
| `blocks` | TitleBlocks | `blocks`（積む文字の行）、`blocksOnJump`、`jumpHeight`、`rain`（降らせる言葉） | J / B |
| `marble` | MarbleMachine | なし | M |
| `stand` | BookStand | `stand`（`icon` と `label` の配列）、`note` | T / Shift+T |
| `agents` | AgentScene | `agents`（数）、`troubles`、`crackSpeed` | W |
| `qr` | QrGuide | `qr`（`title` `caption` `heading` `steps` `image`） | Q |

### 仕掛けを足す

1. `Scripts/` に `Gimmick` を継承したクラスを作る。
   - `OnEnter(json, deck)`：場面に入ったとき。設定は `Read<設定クラス>(json)` で読む（設定クラスは `[Serializable]` で、読みたい項目だけ並べる）。
   - `OnExit(deck)`：場面から出たとき。作ったものを片付ける。
   - スクリーンの文字を使うなら `deck.Body` / `deck.Note` に書く。
   - 効果音は `Sfx.Play("名前")`、火花は `Sparks.Emit(...)`。足りない効果音は `Sfx.Define()` に1行足す。
   - アバターの手足の当たりは `AvatarColliders.Part`（`Hand` なら手・前腕）で見分ける。
   - 体の動きへの反応しやすさ（しきい値）は、場面の項目で上書きできるようにしておく（実機で調整するため）。
   - キー操作による代わりの操作を必ず用意する（体の動きで反応しなかったときの保険）。
2. `Editor/` に組み立てメソッドを作り、`[GimmickBuilder]` を付ける。形は `StageBuilder.Box` / `Column` / `Shape`（面取り済み）と `Glow` / `Lit` / `Metal` の素材を使う。`Id` を必ず設定する。
3. 必要なら `PlayTest.cs` に確認手順を足す。
4. `ConnectionHud.cs` の操作一覧と、README のキー一覧・仕掛けの表に追記する。

`SlideDeck` や `StageBuilder` 本体を書き換える必要はない（組み立てメソッドは自動で呼ばれる）。

## ビルドせずに確かめる

Unity は `/Applications/Unity/Hub/Editor/6000.3.19f1/Unity.app/Contents/MacOS/Unity`。エディタを開いたまま実行するとプロジェクトがロックされて失敗する。

```bash
# シーンを作り直す（-quit を付けない。VRM の取り込みを待ってから自分で終了する）
Unity -batchmode -projectPath . -executeMethod Guidance.EditorTools.GuidanceSetup.ImportAvatarAndCreateScene -logFile <ログ>

# 再生して決めた時刻に操作と撮影を行う（Build/test-*.png に出る。結果はログの "PLAYTEST" 行）
Unity -batchmode -projectPath . -executeMethod Guidance.EditorTools.PlayTest.Marbles -logFile <ログ>

# 場面ごとの静止画（Build/slide-*.png、Build/preview-*.png）
Unity -batchmode -quit -projectPath . -executeMethod Guidance.EditorTools.GuidanceSetup.RenderPreview -logFile <ログ>
```

- `PlayTest` は `Run(場面の番号)` で再生を始め、`At(秒, 操作)` で手順を並べる。手や足の代わりの球は `Kicker(位置, 手かどうか)`。
- 画面に重ねる表示（QR 案内、操作一覧、プレゼンの一覧、2画面出力）は再生テストの画像には写らない。確かめるときは、ユーザーの了解を得て Mac 版をビルドし、`-guide-shot 出力.png`（案内を出して撮影して終了）に、必要なら `-guide-mirror on|off`、`-open-menu`（プレゼンの一覧を開く）を付けて起動する。
- プレゼンの切り替えは `PlayTest.Switch` で確かめられる。
- 仕掛けの設定値（public な欄）を変えたら、シーンを作り直さないと反映されない（シーンに古い値が保存されているため）。
- 計算で作る形の向き（表裏）を間違えると、箱が中から見えて透けたようになる。近くから撮って確かめる。

## ビルド（指示があったときだけ）

```bash
Unity -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod Guidance.EditorTools.GuidanceSetup.BuildWindows -logFile <ログ>
```

出力は `Build/Windows/MocopiPresenter/`。渡すときは `Build/Windows/MocopiPresenter-Windows.zip` にまとめる（`*DoNotShip*` は除く）。

## RAYNOS とシーンファイル

- 手元では `Assets/Guidance/Avatars/RAYNOS/RAYNOS-chan_1.0.6.vrm` を置いており、シーンを作り直すと RAYNOS に差し替わる。無ければプラグイン同梱のサンプルアバターになる。
- 手元のシーン（RAYNOS 入り）を誤って公開しないよう、`Assets/Guidance/Scenes/Guidance.unity` に `git update-index --skip-worktree` を設定してある。
- 公開側のシーンを更新するとき（シーンの作りを変えたとき）：
  1. `Assets/Guidance/Avatars/RAYNOS` と `RAYNOS.meta` をリポジトリの外へ一時退避する。
  2. シーンを作り直す（サンプルアバターになる）。
  3. `git update-index --no-skip-worktree Assets/Guidance/Scenes/Guidance.unity` してからコミットする。
  4. RAYNOS を戻してシーンを作り直し、`--skip-worktree` を設定し直す。
- リリース用の zip は、RAYNOS を退避した状態でビルドし、`LICENSE`、`THIRD_PARTY_NOTICES.md`、`Assets/MocopiReceiver/LICENSE`（`LICENSE-mocopi-receiver-plugin.txt` として）を同梱する。
