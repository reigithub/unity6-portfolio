using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace Game.Shared.Localization
{
    [AddComponentMenu("Localization/Asset/Localize TmpFont Event")]
    public class LocalizeTmpFontEvent : LocalizedAssetEvent<TMP_FontAsset, LocalizedTmpFont, UnityEventTmpFont>
    {
    }

    [Serializable]
    public class UnityEventTmpFont : UnityEvent<TMP_FontAsset>
    {
    }
}
