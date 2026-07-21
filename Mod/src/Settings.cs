using System;
using UnityModManagerNet;

namespace CheryFramework
{
    [Serializable]
    public sealed class Settings : UnityModManager.ModSettings
    {
        public float UiScale = 1.0f;

        public void Normalize()
        {
            if (float.IsNaN(UiScale) || float.IsInfinity(UiScale)) UiScale = 1.0f;
            UiScale = Math.Max(0.75f, Math.Min(1.75f, UiScale));
        }

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Normalize();
            Save(this, modEntry);
        }
    }
}
