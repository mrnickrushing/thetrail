namespace TheTrail.Core
{
    /// <summary>
    /// The game's colours. Canvas pixels are indices into this table. 0-5 are the interface ramp
    /// (dark walnut to cream, with 5 the gold highlight); the rest are named colours for the pictures.
    /// The host turns indices into RGB with <see cref="Rgb"/>.
    /// </summary>
    public static class Pal
    {
        // Interface ramp (same values as Canvas.Black..Hot).
        public const byte Ink = 0, Walnut = 1, Leather = 2, Tan = 3, Cream = 4, Gold = 5;

        public const byte Night = 6, SkyDeep = 7, Sky = 8, SkyPale = 9, Haze = 10, Sun = 11;
        public const byte GrassDeep = 12, Grass = 13, GrassLight = 14, DryGrass = 15;
        public const byte SandLight = 16, Sand = 17, Earth = 18, EarthDark = 19;
        public const byte RockLight = 20, Rock = 21, RockDark = 22, RedRock = 23, RedRockDark = 24;
        public const byte WaterDeep = 25, Water = 26, WaterLight = 27, Foam = 28;
        public const byte WoodDark = 29, Wood = 30, Cloth = 31, ClothShade = 32;
        public const byte GoldDark = 33, GoldMid = 34, GoldLight = 35;
        public const byte Red = 36, RedDark = 37, Blue = 38, Skin = 39, SkinDark = 40;
        public const byte Snow = 41, SnowShade = 42, PineDeep = 43, Pine = 44, Sage = 45, SageDark = 46;
        public const byte Black = 47, Far = 48, FarLight = 49, Dusk = 50, DuskPink = 51;
        public const byte Green = 52, RedBright = 53;
        public const int Count = 54;

        /// <summary>RGB of every index, 3 bytes each.</summary>
        public static readonly byte[] Rgb =
        {
            22, 15, 11,     // 0 ink: the page behind everything
            46, 32, 23,     // 1 walnut
            84, 60, 42,     // 2 leather
            150, 117, 82,   // 3 tan
            236, 218, 178,  // 4 cream: body text
            255, 206, 84,   // 5 gold: highlights
            20, 26, 50,     // 6 night
            58, 96, 156,    // 7 sky deep
            104, 152, 204,  // 8 sky
            170, 204, 228,  // 9 sky pale
            232, 220, 196,  // 10 haze
            255, 240, 186,  // 11 sun
            44, 72, 40,     // 12 grass deep
            80, 112, 52,    // 13 grass
            136, 152, 74,   // 14 grass light
            190, 172, 102,  // 15 dry grass
            226, 206, 158,  // 16 sand light
            194, 162, 112,  // 17 sand
            142, 100, 64,   // 18 earth
            92, 62, 42,     // 19 earth dark
            182, 168, 152,  // 20 rock light
            128, 116, 108,  // 21 rock
            76, 68, 68,     // 22 rock dark
            184, 98, 64,    // 23 red rock
            124, 60, 44,    // 24 red rock dark
            30, 62, 90,     // 25 water deep
            56, 104, 140,   // 26 water
            118, 170, 198,  // 27 water light
            226, 238, 242,  // 28 foam
            70, 44, 28,     // 29 wood dark
            122, 82, 48,    // 30 wood
            236, 228, 204,  // 31 cloth (wagon cover)
            184, 172, 148,  // 32 cloth shade
            170, 116, 28,   // 33 gold dark
            230, 176, 46,   // 34 gold
            255, 232, 132,  // 35 gold light
            168, 46, 40,    // 36 red
            98, 28, 28,     // 37 red dark
            58, 72, 122,    // 38 blue (shirts)
            216, 164, 124,  // 39 skin
            150, 100, 70,   // 40 skin dark
            242, 246, 250,  // 41 snow
            186, 200, 222,  // 42 snow shade
            24, 46, 40,     // 43 pine deep
            42, 74, 56,     // 44 pine
            134, 144, 112,  // 45 sage
            92, 102, 80,    // 46 sage dark
            12, 9, 9,       // 47 black: outlines
            104, 94, 128,   // 48 far mountains
            156, 146, 170,  // 49 far, lighter
            232, 136, 72,   // 50 dusk orange
            204, 114, 112,  // 51 dusk pink
            110, 190, 90,   // 52 green (good news)
            232, 84, 64,    // 53 bright red (danger)
        };

        // Ramps for dithered gradients, dark to light.
        public static readonly byte[] SkyRamp = { SkyDeep, Sky, SkyPale, Haze };
        public static readonly byte[] DuskRamp = { SkyDeep, Far, DuskPink, Dusk, Sun };
        public static readonly byte[] NightRamp = { Night, SkyDeep, Far };
        public static readonly byte[] GrassRamp = { GrassDeep, Grass, GrassLight, DryGrass };
        public static readonly byte[] DryRamp = { EarthDark, Earth, Sand, SandLight };
        public static readonly byte[] SageRamp = { SageDark, Sage, DryGrass, Sand };
        public static readonly byte[] RockRamp = { RockDark, Rock, RockLight };
        public static readonly byte[] RedRockRamp = { RedRockDark, RedRock, Sand };
        public static readonly byte[] WaterRamp = { WaterDeep, Water, WaterLight, Foam };
        public static readonly byte[] FarRamp = { Far, FarLight, Haze };
        public static readonly byte[] SnowRamp = { SnowShade, Snow };
        public static readonly byte[] PineRamp = { PineDeep, Pine, Grass };
        public static readonly byte[] GoldRamp = { GoldDark, GoldMid, GoldLight };
        public static readonly byte[] UiRamp = { Ink, Walnut, Leather, Tan };
    }
}
