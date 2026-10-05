using System;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 場面に出す仕掛けの土台。仕掛けはこのクラスを継承して作る。
    /// 場面ファイル（slides.json）の "gimmicks" に Id を書いた場面に入ると OnEnter が、その場面から出ると OnExit が呼ばれる。
    /// 仕掛けごとの設定は、その場面の JSON から自分で読む（Read&lt;T&gt;）。知らない項目は無視されるので、
    /// 仕掛けを足しても他の仕掛けや場面ファイルの書き方には影響しない。
    /// 新しい仕掛けの足し方は CLAUDE.md の「仕掛けを足す」を参照。
    /// </summary>
    public abstract class Gimmick : MonoBehaviour
    {
        /// <summary>
        /// 場面ファイルの "gimmicks" に書く名前（例："marble"）
        /// </summary>
        public string Id = "";

        /// <summary>
        /// いまの場面で使われているか
        /// </summary>
        public bool InUse { get; private set; }

        /// <summary>
        /// 使っていない場面では隠すか。キー操作をいつでも受け付けたい仕掛けは false にする
        /// </summary>
        protected virtual bool HideWhenUnused => true;

        /// <summary>
        /// 場面に入ったときに SlideDeck が呼ぶ。json はその場面の JSON 全体
        /// </summary>
        public void Enter(string json, SlideDeck deck)
        {
            this.InUse = true;
            if (!this.gameObject.activeSelf)
            {
                this.gameObject.SetActive(true);
            }

            this.OnEnter(string.IsNullOrEmpty(json) ? "{}" : json, deck);
        }

        /// <summary>
        /// この仕掛けを使わない場面に入ったときに SlideDeck が呼ぶ
        /// </summary>
        public void Exit(SlideDeck deck)
        {
            this.InUse = false;
            this.OnExit(deck);
            if (this.HideWhenUnused && this.gameObject.activeSelf)
            {
                this.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// プレゼンを読み込んだ直後に1回呼ばれる（場面に入る前から準備が要る仕掛け用）
        /// </summary>
        public virtual void OnPresentationLoaded(SlideDeck deck)
        {
        }

        protected abstract void OnEnter(string json, SlideDeck deck);

        protected virtual void OnExit(SlideDeck deck)
        {
        }

        /// <summary>
        /// 場面の JSON から、この仕掛けの設定を読む。T には読みたい項目だけを並べたクラスを渡す
        /// </summary>
        protected static T Read<T>(string json) where T : new()
        {
            var settings = new T();
            try
            {
                JsonUtility.FromJsonOverwrite(json, settings);
            }
            catch (Exception e)
            {
                Debug.LogWarning("場面の設定を読めません: " + e.Message);
            }

            return settings;
        }
    }
}
