using UnityEngine;

namespace Game.Shared.Shaders
{
    /// <summary>
    /// シェーダープロパティIDの事前計算キャッシュ
    /// Shader.PropertyToID()の実行時コストを削減
    /// </summary>
    public static class ShaderPropertyIds
    {
        #region Base Properties

        /// <summary>ベースマップ (_BaseMap)</summary>
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        /// <summary>ベースカラー (_BaseColor)</summary>
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        #endregion

        #region ToonLit Properties

        /// <summary>シェードカラー (_ShadeColor)</summary>
        public static readonly int ShadeColor = Shader.PropertyToID("_ShadeColor");

        /// <summary>シェード閾値 (_ShadeThreshold)</summary>
        public static readonly int ShadeThreshold = Shader.PropertyToID("_ShadeThreshold");

        /// <summary>シェードスムースネス (_ShadeSmoothness)</summary>
        public static readonly int ShadeSmoothness = Shader.PropertyToID("_ShadeSmoothness");

        /// <summary>シャドウ減衰 (_ShadowAttenuation)</summary>
        public static readonly int ShadowAttenuation = Shader.PropertyToID("_ShadowAttenuation");

        /// <summary>ランプマップ (_RampMap)</summary>
        public static readonly int RampMap = Shader.PropertyToID("_RampMap");

        /// <summary>リムカラー (_RimColor)</summary>
        public static readonly int RimColor = Shader.PropertyToID("_RimColor");

        /// <summary>リムパワー (_RimPower)</summary>
        public static readonly int RimPower = Shader.PropertyToID("_RimPower");

        /// <summary>リム閾値 (_RimThreshold)</summary>
        public static readonly int RimThreshold = Shader.PropertyToID("_RimThreshold");

        /// <summary>リムスムースネス (_RimSmoothness)</summary>
        public static readonly int RimSmoothness = Shader.PropertyToID("_RimSmoothness");

        /// <summary>アウトラインカラー (_OutlineColor)</summary>
        public static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");

        /// <summary>アウトライン幅 (_OutlineWidth)</summary>
        public static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");

        /// <summary>エミッションカラー (_EmissionColor)</summary>
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        #endregion

        #region Dissolve Properties

        /// <summary>ディゾルブマップ (_DissolveMap)</summary>
        public static readonly int DissolveMap = Shader.PropertyToID("_DissolveMap");

        /// <summary>ディゾルブ量 (_DissolveAmount)</summary>
        public static readonly int DissolveAmount = Shader.PropertyToID("_DissolveAmount");

        /// <summary>エッジカラー (_EdgeColor)</summary>
        public static readonly int EdgeColor = Shader.PropertyToID("_EdgeColor");

        /// <summary>エッジ幅 (_EdgeWidth)</summary>
        public static readonly int EdgeWidth = Shader.PropertyToID("_EdgeWidth");

        /// <summary>エッジカラー強度 (_EdgeColorIntensity)</summary>
        public static readonly int EdgeColorIntensity = Shader.PropertyToID("_EdgeColorIntensity");

        /// <summary>ディゾルブ方向 (_DissolveDirection)</summary>
        public static readonly int DissolveDirection = Shader.PropertyToID("_DissolveDirection");

        /// <summary>方向影響度 (_DirectionalInfluence)</summary>
        public static readonly int DirectionalInfluence = Shader.PropertyToID("_DirectionalInfluence");

        #endregion

        #region HitFlash Properties

        /// <summary>フラッシュカラー (_FlashColor)</summary>
        public static readonly int FlashColor = Shader.PropertyToID("_FlashColor");

        /// <summary>フラッシュ量 (_FlashAmount)</summary>
        public static readonly int FlashAmount = Shader.PropertyToID("_FlashAmount");

        #endregion

        #region Common Properties

        /// <summary>メインテクスチャ (_MainTex) - レガシー互換</summary>
        public static readonly int MainTex = Shader.PropertyToID("_MainTex");

        /// <summary>カラー (_Color) - レガシー互換</summary>
        public static readonly int Color = Shader.PropertyToID("_Color");

        /// <summary>カットオフ (_Cutoff)</summary>
        public static readonly int Cutoff = Shader.PropertyToID("_Cutoff");

        /// <summary>アルファ (_Alpha)</summary>
        public static readonly int Alpha = Shader.PropertyToID("_Alpha");

        /// <summary>時間 (_Time)</summary>
        public static readonly int Time = Shader.PropertyToID("_Time");

        #endregion
    }
}
