using System;
using System.Collections.Generic;
using System.Text;

namespace QuakePatches;

public enum GameVersion
{
    Unknown,
    Steam,
    GOG,
    EGS
}

public static class GameVersionDetector {

    private static readonly Dictionary<GameVersion,byte[]> Signatures = new()
    {
        { GameVersion.Steam, Encoding.ASCII.GetBytes("bastet_Shipping_Playfab_Steam_x64") },
        { GameVersion.GOG, Encoding.ASCII.GetBytes("bastet_Shipping_Playfab_GOG_x64") },
        { GameVersion.EGS, Encoding.ASCII.GetBytes("bastet_Shipping_Playfab_EGS_x64") }
    };

    public static GameVersion Detect(PatchedBinary binary)
    {
        var span = binary.FullBinary.AsSpan();
        foreach(var kv in Signatures)
        {
            if(MemoryExtensions.IndexOf(span,kv.Value) >= 0)
                return kv.Key;
        }

        return GameVersion.Unknown;
    }
}