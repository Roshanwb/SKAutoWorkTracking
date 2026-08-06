using System;

namespace SKAuto.Core.Enums
{
    public enum OrderType
    {
        PSA_Sur_Site,
        PSA_Exterieur,
        Direct_Sur_Site,
        Direct_Exterieur
    }

    public static class OrderTypeExtensions
    {
        public static string GetDisplayName(this OrderType value)
        {
            return value switch
            {
                OrderType.PSA_Sur_Site => "PSA Sur Site",
                OrderType.PSA_Exterieur => "PSA Exterieur",
                OrderType.Direct_Sur_Site => "Direct Sur Site",
                OrderType.Direct_Exterieur => "Direct Exterieur",
                _ => value.ToString().Replace('_', ' ')
            };
        }
    }
}