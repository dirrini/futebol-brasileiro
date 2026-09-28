using System;
using System.Collections.Generic;
using FStudio.Data;
using FStudio.Database;
using FStudio.FootballWorld.DataContracts;

namespace FStudio.FootballWorld.Infrastructure.LegacyMatch
{
    // Portable preset IDs are independent from legacy enum names and numeric values.
    // Only this visual adapter knows how the built-in Unity character represents them.
    public static class BuiltinAppearanceMapper
    {
        private static readonly IReadOnlyDictionary<string, SkinColor> SkinTones =
            new Dictionary<string, SkinColor>(StringComparer.Ordinal)
            {
                { "tone-1", SkinColor.SuperBright }, { "tone-2", SkinColor.Bright },
                { "tone-3", SkinColor.LightBronze }, { "tone-4", SkinColor.Bronze },
                { "tone-5", SkinColor.Dark }, { "tone-6", SkinColor.SuperDark }
            };

        private static readonly IReadOnlyDictionary<string, HairStyles> HairStylesById =
            new Dictionary<string, HairStyles>(StringComparer.Ordinal)
            {
                { "none", HairStyles.None }, { "short", HairStyles.ShortHair },
                { "styled", HairStyles.StylishHair }, { "styled-alt", HairStyles.StylishUnordinaryHair },
                { "mohawk", HairStyles.Mohawk }, { "locs", HairStyles.BobMarley },
                { "short-parted", HairStyles.ShortItalian }
            };

        private static readonly IReadOnlyDictionary<string, FacialHairStyles> BeardStyles =
            new Dictionary<string, FacialHairStyles>(StringComparer.Ordinal)
            {
                { "none", FacialHairStyles.None }, { "mustache", FacialHairStyles.Mustache },
                { "goatee", FacialHairStyles.Goatee }, { "full", FacialHairStyles.LongBeard }
            };

        private static readonly IReadOnlyDictionary<string, HairColors> HairColorsById =
            new Dictionary<string, HairColors>(StringComparer.Ordinal)
            {
                { "brown", HairColors.Brown }, { "dark-brown", HairColors.DarkBrown },
                { "black", HairColors.Black }, { "light-yellow", HairColors.LightYellow },
                { "yellow", HairColors.Yellow }, { "gray", HairColors.Gray }, { "white", HairColors.White },
                { "green", HairColors.Green }, { "dark-green", HairColors.DarkGreen },
                { "blue", HairColors.Blue }, { "dark-blue", HairColors.DarkBlue },
                { "light-red", HairColors.LightRed }, { "red", HairColors.Red },
                { "light-orange", HairColors.LightOrange }, { "orange", HairColors.Orange }
            };

        private static readonly IReadOnlyDictionary<string, BootColor> BootsColors =
            new Dictionary<string, BootColor>(StringComparer.Ordinal)
            {
                { "black", BootColor.Black }, { "red", BootColor.Red }, { "orange", BootColor.Orange },
                { "purple", BootColor.Purple }, { "cyan", BootColor.Cyan },
                { "gray", BootColor.Gray }, { "white", BootColor.White }
            };

        private static readonly IReadOnlyDictionary<string, SockAccessoryColor> SockColors =
            new Dictionary<string, SockAccessoryColor>(StringComparer.Ordinal)
            {
                { "none", SockAccessoryColor.None }, { "black", SockAccessoryColor.Black },
                { "gray", SockAccessoryColor.Gray }, { "white", SockAccessoryColor.White }
            };

        public static void Apply(PlayerEntry target, BuiltinAppearanceData appearance)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (appearance == null) throw new ArgumentNullException(nameof(appearance));
            target.SkinColor = Resolve(SkinTones, appearance.SkinTone, nameof(appearance.SkinTone));
            target.HairStyles = Resolve(HairStylesById, appearance.HairStyle, nameof(appearance.HairStyle));
            target.HairColor = Resolve(HairColorsById, appearance.HairColor, nameof(appearance.HairColor));
            target.FacialHairStyles = Resolve(BeardStyles, appearance.BeardStyle, nameof(appearance.BeardStyle));
            target.FacialHairColor = Resolve(HairColorsById, appearance.BeardColor, nameof(appearance.BeardColor));
            target.BootColor = Resolve(BootsColors, appearance.BootsColor, nameof(appearance.BootsColor));
            target.SockAccessoryColor = Resolve(SockColors, appearance.SockAccessoryColor, nameof(appearance.SockAccessoryColor));
        }

        private static T Resolve<T>(IReadOnlyDictionary<string, T> presets, string id, string field)
        {
            if (id != null && presets.TryGetValue(id, out var value)) return value;
            throw new ArgumentException("Unsupported built-in appearance preset: " + id, field);
        }
    }
}
