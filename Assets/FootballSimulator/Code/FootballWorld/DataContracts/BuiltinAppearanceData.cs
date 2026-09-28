using System.Collections.Generic;

namespace FStudio.FootballWorld.DataContracts
{
    // Portable visual choices. The Unity adapter resolves these IDs into local resources.
    // None of these choices is part of sporting attributes or player identity.
    public sealed class BuiltinAppearanceData
    {
        public string SkinTone { get; }
        public string HairStyle { get; }
        public string HairColor { get; }
        public string BeardStyle { get; }
        public string BeardColor { get; }
        public string BootsColor { get; }
        public string SockAccessoryColor { get; }

        public BuiltinAppearanceData(string skinTone, string hairStyle, string hairColor,
            string beardStyle, string beardColor, string bootsColor, string sockAccessoryColor)
        {
            SkinTone = skinTone;
            HairStyle = hairStyle;
            HairColor = hairColor;
            BeardStyle = beardStyle;
            BeardColor = beardColor;
            BootsColor = bootsColor;
            SockAccessoryColor = sockAccessoryColor;
        }
    }

    // Stable, case-sensitive IDs supported by the schema v2 built-in appearance profile.
    // Adding a new resource requires an explicit contract and visual-adapter update.
    public static class BuiltinAppearancePresets
    {
        public static IReadOnlyList<string> SkinTones { get; } = DataSnapshot.Copy(new[] {
            "tone-1", "tone-2", "tone-3", "tone-4", "tone-5", "tone-6"
        });

        public static IReadOnlyList<string> HairStyles { get; } = DataSnapshot.Copy(new[] {
            "none", "short", "styled", "styled-alt", "mohawk", "locs", "short-parted"
        });

        public static IReadOnlyList<string> HairColors { get; } = DataSnapshot.Copy(new[] {
            "brown", "dark-brown", "black", "light-yellow", "yellow", "gray", "white",
            "green", "dark-green", "blue", "dark-blue", "light-red", "red", "light-orange", "orange"
        });

        public static IReadOnlyList<string> BeardStyles { get; } = DataSnapshot.Copy(new[] {
            "none", "mustache", "goatee", "full"
        });

        public static IReadOnlyList<string> BootsColors { get; } = DataSnapshot.Copy(new[] {
            "black", "red", "orange", "purple", "cyan", "gray", "white"
        });

        public static IReadOnlyList<string> SockAccessoryColors { get; } = DataSnapshot.Copy(new[] {
            "none", "black", "gray", "white"
        });
    }
}
