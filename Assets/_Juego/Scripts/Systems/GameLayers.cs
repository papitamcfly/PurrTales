using UnityEngine;

namespace PurrTales
{
    /// <summary>Nombres de capas usadas por Purr-tales y utilidades de máscaras.</summary>
    public static class GameLayers
    {
        public const string Ground = "Ground";
        public const string Wall = "Wall";
        public const string PortalSurface = "PortalSurface";
        public const string Hazard = "Hazard";

        public static readonly string[] All = { Ground, Wall, PortalSurface, Hazard };

        public static bool InMask(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        /// <summary>Máscara por nombres (ignora los que no existan).</summary>
        public static LayerMask MaskOf(params string[] layerNames)
        {
            int mask = 0;
            for (int i = 0; i < layerNames.Length; i++)
            {
                int layer = LayerMask.NameToLayer(layerNames[i]);
                if (layer >= 0) mask |= 1 << layer;
            }
            return mask;
        }
    }
}
