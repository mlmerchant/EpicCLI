﻿#region GPL statement

/*Epic Edit is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, version 3 of the License.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.*/

#endregion

using EpicLib.Tracks;
using EpicLib.Utility;

namespace EpicLib;

public class Offsets
{
    private readonly int[] _offsets;

    /// <summary>
    ///     Loads all the needed offsets depending on the ROM region.
    /// </summary>
    public Offsets(byte[] romBuffer, Region region)
    {
        _offsets = new int[Enum.GetValues(typeof(Offset)).Length];

        switch (region)
        {
            case Region.Jap:
                this[Offset.ModeNames] = 0x58B19;
                this[Offset.GPCupSelectTexts] = 0x4F6D7;
                this[Offset.GPResultsCupTexts] = 0x5BE62;
                this[Offset.GPPodiumCupTexts] = 0x5A092;
                this[Offset.DriverNamesGPResults] = 0x5C1EC;
                this[Offset.DriverNamesGPPodium] = 0x5A0E0;
                this[Offset.DriverNamesTimeTrial] = 0x1DDCA;
                this[Offset.BattleTrackOrder] = 0x1C022;
                this[Offset.FirstBattleTrack] = 0x1BF0A;
                this[Offset.TrackMaps] = Utilities.BytesToOffset(romBuffer, 0x1E74D);
                this[Offset.TrackAIDataFirstAddressByte] = 0x1FBC4;
                this[Offset.TrackAIAreas] = 0x1FF8C;
                this[Offset.BattleTrackStartPositions] = 0x18B5F;
                this[Offset.TrackPreviewLapLines] = 0x1C886;
                this[Offset.ItemIconTileLayout] = 0x1B1DC;
                this[Offset.TrackOverlayPatterns] = 0x4F0B5;
                this[Offset.TrackObjectHack1] = 0x18EF3;
                this[Offset.TrackObjectHack2] = 0x19155;
                this[Offset.TrackObjectHack3] = 0x19E8E;
                this[Offset.TrackObjectHack4] = 0x1E996;
                this[Offset.TrackObjectPalHack1] = 0xBD33;
                this[Offset.JumpBarCheck] = 0xB795;
                this[Offset.CommonTilesetGraphicsUpperByte] = 0x1E6C1;
                this[Offset.RoadTilesetHack1] = 0x1E695;
                this[Offset.GhostValleyBackgroundAnimationGraphics] = 0x3F04B;
                this[Offset.RankPoints] = 0x5BE52;
                this[Offset.MakeDataReset1] = 0x1E769;
                this[Offset.MakeDataReset2] = 0x1FBBE;
                break;

            case Region.Us:
                this[Offset.ModeNames] = 0x58B00;
                this[Offset.GPCupSelectTexts] = 0x4F85F;
                this[Offset.GPResultsCupTexts] = 0x5BEC4;
                this[Offset.GPPodiumCupTexts] = 0x5A0EE;
                this[Offset.DriverNamesGPResults] = 0x5C25B;
                this[Offset.DriverNamesGPPodium] = 0x5A148;
                this[Offset.DriverNamesTimeTrial] = 0x1DDD3;
                this[Offset.BattleTrackOrder] = 0x1C15C;
                this[Offset.FirstBattleTrack] = 0x1C04C;
                this[Offset.TrackMaps] = Utilities.BytesToOffset(romBuffer, 0x1E749);
                this[Offset.TrackAIDataFirstAddressByte] = 0x1FBD3;
                this[Offset.TrackAIAreas] = 0x1FF9B;
                this[Offset.BattleTrackStartPositions] = 0x18B4B;
                this[Offset.TrackPreviewLapLines] = 0x1C915;
                this[Offset.ItemIconTileLayout] = 0x1B320;
                this[Offset.TrackOverlayPatterns] = 0x4F23D;
                this[Offset.TrackObjectHack1] = 0x18EDF;
                this[Offset.TrackObjectHack2] = 0x19141;
                this[Offset.TrackObjectHack3] = 0x19E2B;
                this[Offset.TrackObjectHack4] = 0x1E992;
                this[Offset.TrackObjectPalHack1] = 0xBD0E;
                this[Offset.JumpBarCheck] = 0xB79E;
                this[Offset.CommonTilesetGraphicsUpperByte] = 0x1E6BD;
                this[Offset.RoadTilesetHack1] = 0x1E691;
                this[Offset.GhostValleyBackgroundAnimationGraphics] = 0x3F058;
                this[Offset.RankPoints] = 0x5BEB4;
                this[Offset.MakeDataReset1] = 0x1E765;
                this[Offset.MakeDataReset2] = 0x1FBCD;
                break;

            case Region.Euro:
                this[Offset.ModeNames] = 0x58AF2;
                this[Offset.GPCupSelectTexts] = 0x4F778;
                this[Offset.GPResultsCupTexts] = 0x5BECC;
                this[Offset.GPPodiumCupTexts] = 0x5A0F8;
                this[Offset.DriverNamesGPResults] = 0x5C263;
                this[Offset.DriverNamesGPPodium] = 0x5A152;
                this[Offset.DriverNamesTimeTrial] = 0x1DC81;
                this[Offset.BattleTrackOrder] = 0x1BFF8;
                this[Offset.FirstBattleTrack] = 0x1BEE8;
                this[Offset.TrackMaps] = Utilities.BytesToOffset(romBuffer, 0x1E738);
                this[Offset.TrackAIDataFirstAddressByte] = 0x1FB9D;
                this[Offset.TrackAIAreas] = 0x1FF6D;
                this[Offset.BattleTrackStartPositions] = 0x18B64;
                this[Offset.TrackPreviewLapLines] = 0x1C7B1;
                this[Offset.ItemIconTileLayout] = 0x1B1BC;
                this[Offset.TrackOverlayPatterns] = 0x4F159;
                this[Offset.TrackObjectHack1] = 0x18EF8;
                this[Offset.TrackObjectHack2] = 0x1915A;
                this[Offset.TrackObjectHack3] = 0x19E68;
                this[Offset.TrackObjectHack4] = 0x1E981;
                this[Offset.TrackObjectPalHack1] = 0xBD33;
                this[Offset.JumpBarCheck] = 0xB7A3;
                this[Offset.CommonTilesetGraphicsUpperByte] = 0x1E6AC;
                this[Offset.RoadTilesetHack1] = 0x1E680;
                this[Offset.GhostValleyBackgroundAnimationGraphics] = 0x3F058;
                this[Offset.RankPoints] = 0x5BEBC;
                this[Offset.MakeDataReset1] = 0x1EB0A;
                this[Offset.MakeDataReset2] = 0x1FB97;
                break;
        }

        this[Offset.TileGenres] = 0x7FDBA;
        this[Offset.TileGenresRelocated] = Utilities.BytesToOffset(romBuffer, this[Offset.JumpBarCheck] + 1) + 0x12;
        this[Offset.ItemIconGraphics] = 0x112F8;
        this[Offset.TrackObjects] = 0x5C800;
        this[Offset.TrackObjectAreas] = 0x4DB93;
        this[Offset.TrackOverlayItems] = 0x5D000;
        this[Offset.TrackLapLines] = 0x180D4;
        this[Offset.MatchRaceObjectGraphics] = 0x60000;
        this[Offset.ItemGraphics] = 0x40594;
        this[Offset.TrackObjectHack5] = 0x4DABC;
        this[Offset.TrackObjectHack6] = 0x4DCA9;
        this[Offset.TrackObjectHack7] = 0x4DCBD;
        this[Offset.TrackObjectHack8] = 0x4DCC2;
        this[Offset.TrackObjectProperties] = 0x80062;
        this[Offset.TrackObjectAreasRelocated] =
            Utilities.BytesToOffset(romBuffer, this[Offset.TrackObjectHack5] + 1) + 0x6E;
        this[Offset.TrackObjectPaletteIndexes] =
            Utilities.BytesToOffset(romBuffer, this[Offset.TrackObjectHack3] + 1) + 0x13;
        this[Offset.TrackObjectFlashing] = this[Offset.TrackObjectPaletteIndexes] + Track.Count * 4;
        this[Offset.CommonTilesetGraphicsLowerBytes] = this[Offset.CommonTilesetGraphicsUpperByte] + 3;
        this[Offset.TileGenreLoad] = this[Offset.CommonTilesetGraphicsUpperByte] + 0x454;
        this[Offset.RoadTilesetHack2] = this[Offset.RoadTilesetHack1] + 0x28;
        this[Offset.RoadTilesetHack3] = this[Offset.RoadTilesetHack2] + 0x38;
        this[Offset.RoadTilesetHack4] = this[Offset.RoadTilesetHack3] + 0x92;
        this[Offset.NewBattleStart] = 0x80000;
        this[Offset.MakeAIArea] = 0x80080;
        this[Offset.MakeAITarget] = this[Offset.MakeAIArea] + 128;
        this[Offset.MakeTrackMap] = this[Offset.MakeAITarget] + 128;

        this[Offset.TopBorderTileLayout] = this[Offset.ItemIconTileLayout] + 0x507;
        this[Offset.TrackItemProbabilityIndexes] = this[Offset.BattleTrackStartPositions] + 0x28;
        this[Offset.GPTrackStartPositions] = this[Offset.BattleTrackStartPositions] + 0xC8;
        this[Offset.BattleTrackStartPositionsIndex] = this[Offset.BattleTrackStartPositions] + 0x3C9;
        this[Offset.TrackAITargets] = this[Offset.TrackAIAreas] + 0x30;
        this[Offset.BattleTrackNames] = this[Offset.TrackPreviewLapLines] + 0x28;
        this[Offset.CupNamesLocked] = this[Offset.BattleTrackNames] + 0x12;
        this[Offset.CupNames] = this[Offset.CupNamesLocked] + 0xE;
        this[Offset.GPTrackNames] = this[Offset.CupNames] + 0x12;
        this[Offset.NamesAndSuffixes] = this[Offset.GPTrackNames] + 0x58;
        this[Offset.CourseSelectTexts] = this[Offset.NamesAndSuffixes] + 0x6B;
        this[Offset.TrackOverlaySizes] = this[Offset.TrackOverlayPatterns] + 0x147;
        this[Offset.ItemProbabilities] = this[Offset.ItemIconTileLayout] + 0x1C3;

        this[Offset.TileGenreIndexes] = this[Offset.TrackMaps] - Theme.Count * 2;
        this[Offset.ThemeRoadGraphics] = this[Offset.TrackMaps] + Track.Count * 3;
        this[Offset.ThemePalettes] = this[Offset.ThemeRoadGraphics] + Theme.Count * 3;
        this[Offset.TrackObjectGraphics] = this[Offset.ThemePalettes] + Theme.Count * 3;
        this[Offset.ThemeBackgroundGraphics] = this[Offset.TrackObjectGraphics] + Theme.Count * 3;
        this[Offset.ThemeBackgroundLayouts] = this[Offset.ThemeBackgroundGraphics] + Theme.Count * 3;
        this[Offset.GPTrackOrder] = this[Offset.ThemeBackgroundLayouts] + Theme.Count * 3;
        this[Offset.TrackThemes] = this[Offset.GPTrackOrder] + GPTrack.Count;

        this[Offset.TrackObjectPalHack2] = this[Offset.TrackObjectPalHack1] + 0x0E;
        this[Offset.TrackObjectPalHack3] = this[Offset.TrackObjectPalHack2] + 0x6A;
        this[Offset.TrackObjectPalHack4] = this[Offset.TrackObjectPalHack3] + 0x2750;
    }

    public int this[Offset offset]
    {
        get => _offsets[(int)offset];
        set => _offsets[(int)offset] = value;
    }
}