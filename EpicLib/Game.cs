﻿using System.ComponentModel;
using System.Drawing;
using EpicLib.Compression;
using EpicLib.Settings;
using EpicLib.Tracks;
using EpicLib.Tracks.AI;
using EpicLib.Tracks.Items;
using EpicLib.Tracks.Objects;
using EpicLib.Tracks.Overlay;
using EpicLib.Tracks.Road;
using EpicLib.Tracks.Start;
using EpicLib.Utility;

namespace EpicLib;

/// <summary>
///     The Super Mario Kart Game class, which contains all the game data.
/// </summary>
public class Game
{
    #region Constructor

    /// <param name="filePath">The path to the ROM file.</param>
    public Game(string filePath)
    {
        FilePath = filePath;
        LoadRom();
        ValidateRom();
        LoadData();
        HandleChanges();
    }

    #endregion Constructor

    #region IDisposable

    public void Dispose()
    {
        Themes.Dispose();
        ObjectGraphics.Dispose();
        ItemIconGraphics.Dispose();

        GC.SuppressFinalize(this);
    }

    #endregion IDisposable

    #region Constants

    private const int RomTypeOffset = 0xFFD5;
    private const int CartTypeOffset = 0xFFD6;
    private const int RomSizeOffset = 0xFFD7;
    private const int RamSizeOffset = 0xFFD8;
    private const int RegionOffset = 0xFFD9;
    private const int ChecksumOffset1 = 0xFFDC;
    private const int ChecksumOffset2 = 0xFFDD;
    private const int ChecksumOffset3 = 0xFFDE;
    private const int ChecksumOffset4 = 0xFFDF;

    #endregion Constants

    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    public event EventHandler<EventArgs> TracksReordered;

    #endregion

    #region Public properties

    /// <summary>
    ///     Gets the track groups, each of which contains several tracks.
    /// </summary>
    public TrackGroups TrackGroups { get; private set; }

    /// <summary>
    ///     Gets the track themes.
    /// </summary>
    public Themes Themes { get; private set; }

    /// <summary>
    ///     Gets the overlay tile sizes.
    /// </summary>
    public OverlayTileSizes OverlayTileSizes { get; private set; }

    /// <summary>
    ///     Gets the overlay tile patterns.
    /// </summary>
    public OverlayTilePatterns OverlayTilePatterns { get; private set; }

    /// <summary>
    ///     Gets the path to the loaded ROM file.
    /// </summary>
    public string FilePath { get; private set; }

    /// <summary>
    ///     Gets the file name of the loaded ROM.
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    ///     Gets the game settings.
    /// </summary>
    public GameSettings Settings { get; private set; }

    /// <summary>
    ///     Gets the track object graphics.
    /// </summary>
    public TrackObjectGraphics ObjectGraphics { get; private set; }

    /// <summary>
    ///     Gets the item icon graphics.
    /// </summary>
    public ItemIconGraphics ItemIconGraphics { get; private set; }

    public int HeaderSize => _romHeader.Length;

    public bool Modified { get; private set; }

    #endregion Public properties

    #region Private members

    /// <summary>
    ///     Some ROMs have a 512-byte header.
    /// </summary>
    private byte[] _romHeader;

    /// <summary>
    ///     Buffer that contains all the ROM data.
    /// </summary>
    private byte[] _romBuffer;

    /// <summary>
    ///     The region of the ROM (Jap, US or Euro).
    /// </summary>
    private Region _region;

    /// <summary>
    ///     The offsets to find the needed data in the ROM.
    /// </summary>
    private Offsets _offsets;

    #endregion Private members

    #region Read ROM & validate

    /// <summary>
    ///     Loads all the ROM content in a buffer.
    /// </summary>
    private void LoadRom()
    {
        _romBuffer = File.ReadAllBytes(FilePath);
    }

    /// <summary>
    ///     Checks if the ROM is valid. If it is, the ROM header value is initialized.
    ///     Either there is no ROM header (value 0), or if there is one, it's usually a 512-byte one.
    ///     The ROM header is used by adding its value to all ROM offsets, to find the relevent data.
    /// </summary>
    private void ValidateRom()
    {
        if (!IsSnesRom()) throw new InvalidDataException($"\"{FileName}\" is not an SNES ROM.");

        if (!IsSuperMarioKart()) throw new InvalidDataException($"\"{FileName}\" is not a Super Mario Kart ROM.");
    }

    /// <summary>
    ///     Checks whether the file loaded is a Super Nintendo ROM.
    /// </summary>
    private bool IsSnesRom()
    {
        var romHeaderSize = _romBuffer.Length % RomSize.Size256;

        if (romHeaderSize == 0)
        {
            _romHeader = new byte[0];
        }
        else if (romHeaderSize == 512)
        {
            _romHeader = Utilities.ReadBlock(_romBuffer, 0, romHeaderSize);
            var romBufferWithoutHeader =
                Utilities.ReadBlock(_romBuffer, romHeaderSize, _romBuffer.Length - romHeaderSize);
            _romBuffer = romBufferWithoutHeader;
        }
        else
        {
            // Wrong header size
            return false;
        }

        if (_romBuffer.Length < RomSize.Size256 || // ROM size < 256 KiB
            _romBuffer.Length > RomSize.Size8192) // ROM size > 8 MiB
            return false;

        return true;
    }

    /// <summary>
    ///     Checks whether the file loaded is Super Mario Kart.
    /// </summary>
    private bool IsSuperMarioKart()
    {
        if (_romBuffer.Length < RomSize.Size512) return false;

        var cartType =
            _romBuffer
                [CartTypeOffset]; // Cartridge type. SMK has 05 here, if this byte in any SNES ROM is not 05 then it is not a battery backed DSP-1 game
        var cartRamSize =
            _romBuffer[RamSizeOffset]; // Cart RAM size. SMK has 01 here, to say that there's 2 KiB of oncart RAM
        var cartRomType = _romBuffer[RomTypeOffset]; // SMK has 31 here, to indicate a HiROM FastROM game

        if (cartType != 0x05 || cartRamSize != 0x01 || cartRomType != 0x31) return false;

        return true;
    }

    public int Size => _romBuffer.Length;

    #endregion Read ROM & validate

    #region Load data

    /// <summary>
    ///     Retrieves all the needed data from the game, such as tracks and themes.
    /// </summary>
    private void LoadData()
    {
        SetRegion();
        Codec.SetRegion(_region);
        _offsets = new Offsets(_romBuffer, _region);

        Settings = new GameSettings(_romBuffer, _offsets, _region);
        TrackGroups = new TrackGroups();

        Themes = new Themes(_romBuffer, _offsets, Settings.CourseSelectTexts);
        var overlayTileSizesData =
            Utilities.ReadBlock(_romBuffer, _offsets[Offset.TrackOverlaySizes], OverlayTileSizes.Size);
        OverlayTileSizes = new OverlayTileSizes(overlayTileSizesData);
        OverlayTilePatterns = new OverlayTilePatterns(_romBuffer, _offsets, OverlayTileSizes);

        var trackThemes = Utilities.ReadBlock(_romBuffer, _offsets[Offset.TrackThemes], Track.Count);
        var trackOrder = GetTrackOrder();
        var cupNameIndexes = GetCupNameIndexes();
        var trackNameIndexes = GetTrackNameIndexes();

        var mapOffsets = Utilities.ReadBlockOffset(_romBuffer, _offsets[Offset.TrackMaps], Track.Count);

        var aiOffsetBase = _romBuffer[_offsets[Offset.TrackAIDataFirstAddressByte]];
        var aiAreaOffsets =
            Utilities.ReadBlock(_romBuffer, _offsets[Offset.TrackAIAreas], Track.Count * 2); // 2 offset bytes per track
        var aiTargetOffsets =
            Utilities.ReadBlock(_romBuffer, _offsets[Offset.TrackAITargets],
                Track.Count * 2); // 2 offset bytes per track

        for (var i = 0; i < TrackGroups.Count; i++)
        {
            int trackCountInGroup;
            SuffixedTextItem trackGroupNameItem;
            if (i != TrackGroups.Count - 1) // GP track group
            {
                trackCountInGroup = GPTrack.CountPerGroup;
                var trackGroupTextItem = Settings.CourseSelectTexts[cupNameIndexes[i][1]];
                var trackGroupNameSuffixData = Utilities.ReadBlock(cupNameIndexes[i], 2, cupNameIndexes[i].Length - 2);
                var trackGroupNameSuffix = trackGroupTextItem.Converter.DecodeText(trackGroupNameSuffixData, false);
                trackGroupNameItem = new SuffixedTextItem(trackGroupTextItem, trackGroupNameSuffix,
                    Settings.CupAndTrackNameSuffixCollection);
            }
            else // Battle track group
            {
                trackCountInGroup = BattleTrack.Count;
                var trackGroupTextItem = Settings.CourseSelectTexts[trackNameIndexes[GPTrack.Count][1]];

                // NOTE: The "Battle Course" track group doesn't actually exist in the game.
                // It's only created in the editor to have a logical group that contains the Battle Courses.
                trackGroupNameItem = new SuffixedTextItem(trackGroupTextItem, null, null);
            }

            var tracks = new Track[trackCountInGroup];

            for (var j = 0; j < trackCountInGroup; j++)
            {
                var iterator = i * GPTrack.CountPerGroup + j;
                int trackIndex = trackOrder[iterator];

                var trackNameItem = GetTrackNameItem(trackNameIndexes[iterator][1]);
                var trackNameSuffixData =
                    Utilities.ReadBlock(trackNameIndexes[iterator], 2, trackNameIndexes[iterator].Length - 2);
                var trackNameSuffix = trackNameItem.Converter.DecodeText(trackNameSuffixData, false);
                var suffixedTrackNameItem = new SuffixedTextItem(trackNameItem, trackNameSuffix,
                    Settings.CupAndTrackNameSuffixCollection);

                var themeId = trackThemes[trackIndex] >> 1;
                var trackTheme = Themes[themeId];
                var trackMap = GetTrackMap(trackIndex, mapOffsets[trackIndex]);
                var overlayTileData = GetOverlayTileData(trackIndex);
                LoadAIData(trackIndex, aiOffsetBase, aiAreaOffsets, aiTargetOffsets, out var aiAreaData,
                    out var aiTargetData);

                if (trackIndex < GPTrack.Count) // GP track
                {
                    var startPositionData = GetGPStartPositionData(trackIndex);
                    var lapLineData = GetLapLineData(trackIndex);
                    var objectData = GetObjectData(trackIndex);
                    var objectAreaData = GetObjectAreaData(trackIndex);
                    var objectPropData = GetObjectPropertiesData(trackIndex, themeId);
                    var itemProbaIndex = _romBuffer[_offsets[Offset.TrackItemProbabilityIndexes] + trackIndex] >> 1;

                    tracks[j] = new GPTrack(suffixedTrackNameItem, trackTheme,
                        trackMap, overlayTileData,
                        aiAreaData, aiTargetData,
                        startPositionData, lapLineData,
                        objectData, objectAreaData, objectPropData,
                        OverlayTileSizes,
                        OverlayTilePatterns,
                        itemProbaIndex);
                }
                else // Battle track
                {
                    var startPositionData = GetBattleStartPositionData(trackIndex);

                    tracks[j] = new BattleTrack(suffixedTrackNameItem, trackTheme,
                        trackMap, overlayTileData,
                        aiAreaData, aiTargetData,
                        startPositionData,
                        OverlayTileSizes,
                        OverlayTilePatterns);
                }
            }

            TrackGroups[i] = new TrackGroup(trackGroupNameItem, tracks);
        }

        ObjectGraphics = new TrackObjectGraphics(_romBuffer, _offsets);
        ItemIconGraphics = new ItemIconGraphics(_romBuffer, _offsets);
    }

    private TextItem GetTrackNameItem(int cupAndThemTextId)
    {
        TextItem trackNameItem;

        try
        {
            trackNameItem = Settings.CourseSelectTexts[cupAndThemTextId];
        }
        catch
        {
            // HACK: Handle invalid text data ("Super Mario Kart (J) - Series 2" has this issue)
            trackNameItem = new TextItem(Settings.CourseSelectTexts, string.Empty);
        }

        return trackNameItem;
    }

    private byte[] GetTrackMap(int trackIndex, int mapOffset)
    {
        byte[] trackMap;

        if (!IsMakeTrack(trackIndex))
        {
            trackMap = Codec.Decompress(_romBuffer, mapOffset, true);
        }
        else
        {
            // HACK: It seems like this track has been saved with MAKE,
            // which does not compress track maps and just leaves them uncompressed, unlike the original game.
            // Let's load the track map without decompressing it, using the MAKE data offsets.

            mapOffset = GetMakeTrackMapOffset(trackIndex);
            trackMap = Utilities.ReadBlock(_romBuffer, mapOffset, TrackMap.SquareSize);
        }

        return trackMap;
    }

    private int GetMakeTrackMapOffset(int trackIndex)
    {
        var mapOffsetIndex = _offsets[Offset.MakeTrackMap] + trackIndex * 4;
        return Utilities.BytesToOffset(_romBuffer, mapOffsetIndex);
    }

    private bool IsMakeTrack(int trackIndex)
    {
        return
            _romBuffer[_offsets[Offset.MakeDataReset2]] != 0xBD && // Is MAKE ROM
            _romBuffer.Length > RomSize.Size512 && // Is expanded ROM
            GetMakeTrackMapOffset(trackIndex) != 0; // Is MAKE track
    }

    private void SetRegion()
    {
        int region = _romBuffer[RegionOffset];

        if (!Enum.IsDefined(typeof(Region), region))
            throw new InvalidDataException(
                $"\"{FileName}\" has an invalid region. Value at {RegionOffset + _romHeader.Length:X} must be 0, 1 or 2, was: {region:X}.");

        _region = (Region)region;
    }

    public static Region GetRegion(byte[] romBuffer)
    {
        return (Region)romBuffer[RegionOffset];
    }

    /// <summary>
    ///     Gets the order of the tracks.
    /// </summary>
    /// <returns></returns>
    private byte[] GetTrackOrder()
    {
        var gpTrackOrder = Utilities.ReadBlock(_romBuffer, _offsets[Offset.GPTrackOrder], GPTrack.Count);
        var battleTrackOrder = Utilities.ReadBlock(_romBuffer, _offsets[Offset.BattleTrackOrder], BattleTrack.Count);

        var trackOrder = new byte[Track.Count];

        Buffer.BlockCopy(gpTrackOrder, 0, trackOrder, 0, GPTrack.Count);
        Buffer.BlockCopy(battleTrackOrder, 0, trackOrder, GPTrack.Count, BattleTrack.Count);
        return trackOrder;
    }

    private byte[][] GetCupNameIndexes()
    {
        var cupNameIndexes = Utilities.ReadBlockGroup(_romBuffer, _offsets[Offset.CupNames] + 2, 4, GPTrack.GroupCount);

        for (var i = 0; i < cupNameIndexes.Length; i++)
        {
            var offset = Utilities.BytesToOffset(cupNameIndexes[i][0], cupNameIndexes[i][1], 1);
            cupNameIndexes[i] = Utilities.ReadBlockUntil(_romBuffer, offset, 0xFF);
            cupNameIndexes[i][1] = (byte)(cupNameIndexes[i][1] & 0xF);
        }

        return cupNameIndexes;
    }

    private byte[][] GetTrackNameIndexes()
    {
        var gpTrackNamesOffset = _offsets[Offset.GPTrackNames];

        var gpTrackPointers = new byte[GPTrack.Count][];

        for (var i = 0; i < GPTrack.GroupCount; i++)
        {
            gpTrackNamesOffset += 2; // Skip leading bytes
            Utilities.ReadBlockGroup(_romBuffer, gpTrackNamesOffset, 4, GPTrack.CountPerGroup)
                .CopyTo(gpTrackPointers, GPTrack.CountPerGroup * i);
            gpTrackNamesOffset += GPTrack.CountPerGroup * GPTrack.GroupCount;
        }

        var battleTrackPointers =
            Utilities.ReadBlockGroup(_romBuffer, _offsets[Offset.BattleTrackNames] + 2, 4, BattleTrack.Count);
        var trackNameIndexes = new byte[Track.Count][];

        for (var i = 0; i < gpTrackPointers.Length; i++)
        {
            var offset = Utilities.BytesToOffset(gpTrackPointers[i][0], gpTrackPointers[i][1], 1);
            trackNameIndexes[i] = Utilities.ReadBlockUntil(_romBuffer, offset, 0xFF);
        }

        for (var i = 0; i < battleTrackPointers.Length; i++)
        {
            var offset = Utilities.BytesToOffset(battleTrackPointers[i][0], battleTrackPointers[i][1], 1);
            trackNameIndexes[gpTrackPointers.Length + i] = Utilities.ReadBlockUntil(_romBuffer, offset, 0xFF);
        }

        for (var i = 0; i < trackNameIndexes.Length; i++)
        {
            if (trackNameIndexes[i].Length < 2)
            {
                // HACK: Handle invalid text data ("Super Mario Kart (J) - Series 2" has this issue)
                var invalidData = trackNameIndexes[i];
                trackNameIndexes[i] = new[] { (byte)0xFF, (byte)0xFF };

                if (invalidData.Length == 1) trackNameIndexes[i][0] = invalidData[0];
            }

            trackNameIndexes[i][1] = (byte)(trackNameIndexes[i][1] & 0xF);
        }

        return trackNameIndexes;
    }

    #endregion Load data

    #region Get / set, load / save specific data

    #region Track overlay tiles

    private byte[] GetOverlayTileData(int trackIndex)
    {
        var offset = GetOverlayTileDataOffset(trackIndex);
        return Utilities.ReadBlock(_romBuffer, offset, OverlayTiles.Size);
    }

    private void SaveOverlayTileData(byte[] data, int trackIndex)
    {
        var offset = GetOverlayTileDataOffset(trackIndex);
        Buffer.BlockCopy(data, 0, _romBuffer, offset, OverlayTiles.Size);
    }

    private int GetOverlayTileDataOffset(int trackIndex)
    {
        return _offsets[Offset.TrackOverlayItems] + trackIndex * OverlayTiles.Size;
    }

    #endregion Track overlay tiles

    #region GP start positions

    private byte[] GetGPStartPositionData(int trackIndex)
    {
        var offset = GetGPStartPositionDataOffset(trackIndex);
        return Utilities.ReadBlock(_romBuffer, offset, GPStartPosition.Size);
    }

    private void SaveGPStartPositionData(GPTrack track, int trackIndex)
    {
        var data = track.StartPosition.GetBytes();
        var offset = GetGPStartPositionDataOffset(trackIndex);
        Buffer.BlockCopy(data, 0, _romBuffer, offset, GPStartPosition.Size);
    }

    private int GetGPStartPositionDataOffset(int trackIndex)
    {
        // TODO: Retrieve order dynamically from the ROM
        int[] reorder = { 14, 10, 7, 8, 15, 19, 16, 4, 17, 13, 6, 12, 11, 5, 18, 9, 2, 3, 1, 0 };
        return _offsets[Offset.GPTrackStartPositions] + reorder[trackIndex] * 8;
    }

    #endregion GP start positions

    #region Lap line

    private byte[] GetLapLineData(int trackIndex)
    {
        var offset = _offsets[Offset.TrackLapLines] + trackIndex * LapLine.Size;
        return Utilities.ReadBlock(_romBuffer, offset, LapLine.Size);
    }

    #endregion Lap line

    #region Battle start positions

    private byte[] GetBattleStartPositionData(int trackIndex)
    {
        var startPositionOffset = GetBattleStartPositionDataOffset(trackIndex);

        var data = new byte[8];

        if (BattleStartPositionsRelocated)
        {
            for (var i = 0; i < data.Length; i++) data[i] = _romBuffer[startPositionOffset + i];
        }
        else
        {
            // In the original game, it's 2P data first, then 1P.
            var index = 0;
            for (var i = 4; i < data.Length; i++) data[index++] = _romBuffer[startPositionOffset + i];

            for (var i = 0; i < 4; i++) data[index++] = _romBuffer[startPositionOffset + i];
        }

        return data;
    }

    private int GetBattleStartPositionDataOffset(int trackIndex)
    {
        int startPositionOffset;
        var bTrackIndex = trackIndex - GPTrack.Count;

        if (BattleStartPositionsRelocated)
        {
            startPositionOffset = _offsets[Offset.NewBattleStart] + bTrackIndex * 8;
        }
        else
        {
            // The battle starting positions haven't been relocated yet.
            // Ie: This ROM has not been resaved with Epic Edit yet.
            var startPositionOffsetIndex = _offsets[Offset.BattleTrackStartPositions] + bTrackIndex * 8;
            startPositionOffset = Utilities.BytesToOffset(_romBuffer[startPositionOffsetIndex],
                _romBuffer[startPositionOffsetIndex + 1], 1);
            startPositionOffset += 2; // Skip 2 leading bytes
        }

        return startPositionOffset;
    }

    private bool BattleStartPositionsRelocated
    {
        get
        {
            var offset = _offsets[Offset.BattleTrackStartPositionsIndex];

            if (_romBuffer[offset] == 0x5C &&
                _romBuffer[offset + 1] == 0x20 &&
                _romBuffer[offset + 2] == 0x00 &&
                _romBuffer[offset + 3] == 0xC8)
                return true;

            if (_romBuffer[offset] == 0xAD &&
                _romBuffer[offset + 1] == 0x24 &&
                _romBuffer[offset + 2] == 0x01 &&
                _romBuffer[offset + 3] == 0x0A)
                return false;

            throw new InvalidDataException("Error when loading battle track starting positions.");
        }
    }

    #endregion Battle start positions

    #region Objects

    private byte[] GetObjectData(int trackIndex)
    {
        var objectOffset = _offsets[Offset.TrackObjects] + trackIndex * 64;
        return Utilities.ReadBlock(_romBuffer, objectOffset, TrackObjects.Size);
    }

    private byte[] GetObjectPropertiesData(int trackIndex, int themeId)
    {
        var data = new byte[8];
        byte[] paletteIndexes;

        if (ObjectAreasRelocated)
        {
            var offset = _offsets[Offset.TrackObjectProperties] + trackIndex;
            data[0] = _romBuffer[offset];
            data[1] = _romBuffer[offset + Track.Count];
            data[2] = _romBuffer[offset + Track.Count * 2];
            data[7] = _romBuffer[_offsets[Offset.TrackObjectFlashing] + trackIndex];
            paletteIndexes = GetObjectPaletteIndexes(trackIndex);
        }
        else
        {
            var objectType = (byte)GetObjectType(themeId, trackIndex);
            data[0] = objectType;
            data[1] = objectType;
            data[2] = objectType;
            data[7] = themeId == 7 ? (byte)1 : (byte)0; // Rainbow Road
            paletteIndexes = GetObjectPaletteIndexes(themeId, trackIndex);
        }

        data[3] = paletteIndexes[0];
        data[4] = paletteIndexes[1];
        data[5] = paletteIndexes[2];
        data[6] = paletteIndexes[3];

        return data;
    }

    private static TrackObjectType GetObjectType(int themeId, int trackIndex)
    {
        // HACK: We guess the object type based on the track index or theme.
        // It must be possible to retrieve the actual object properties from somewhere.
        TrackObjectType objectType;

        if (trackIndex == 19) // Donut Plains 1
            // This track is an exception
            objectType = TrackObjectType.Pipe;
        else
            switch (themeId)
            {
                case 0: // Ghost Valley
                    objectType = TrackObjectType.Pillar;
                    break;

                case 1: // Mario Circuit
                case 4: // Vanilla Lake
                    objectType = TrackObjectType.Pipe;
                    break;

                case 2: // Donut Plains
                    objectType = TrackObjectType.Mole;
                    break;

                case 3: // Choco Island
                    objectType = TrackObjectType.Plant;
                    break;

                case 5: // Koopa Beach
                    objectType = TrackObjectType.Fish;
                    break;

                case 6: // Bowser Castle
                    objectType = TrackObjectType.Thwomp;
                    break;

                case 7: // Rainbow Road
                    objectType = TrackObjectType.RThwomp;
                    break;

                default: throw new ArgumentOutOfRangeException(nameof(themeId));
            }

        return objectType;
    }

    private byte[] GetObjectPaletteIndexes(int trackIndex)
    {
        var offset = _offsets[Offset.TrackObjectPaletteIndexes] + trackIndex * 4;
        byte[] data =
        {
            (byte)(_romBuffer[offset++] >> 1),
            (byte)(_romBuffer[offset++] >> 1),
            (byte)(_romBuffer[offset++] >> 1),
            (byte)(_romBuffer[offset] >> 1)
        };

        return data;
    }

    /// <summary>
    ///     Guesses the object palette indexes from the theme id (and track index for Donut Plains 1).
    /// </summary>
    private static byte[] GetObjectPaletteIndexes(int themeId, int trackIndex)
    {
        var paletteIndexes = new byte[4];

        if (trackIndex == 19) // Donut Plains 1
            // This track is an exception, as it has orange pipes instead of moles (like other Donut Plains tracks do).
            paletteIndexes[0] = 5;
        else
            switch (themeId)
            {
                case 0: // Ghost Valley
                case 1: // Mario Circuit
                case 2: // Donut Plains
                case 4: // Vanilla Lake
                    paletteIndexes[0] = 7;
                    break;

                case 3: // Choco Island
                case 5: // Koopa Beach
                    paletteIndexes[0] = 6;
                    break;

                case 6: // Bowser Castle
                    paletteIndexes[0] = 4;
                    break;

                case 7: // Rainbow Road
                    paletteIndexes[0] = 1;
                    paletteIndexes[1] = 7;
                    paletteIndexes[2] = 4;
                    paletteIndexes[3] = 7;
                    break;

                default: throw new ArgumentOutOfRangeException(nameof(themeId));
            }

        return paletteIndexes;
    }

    #endregion Objects

    #region Object areas

    private byte[] GetObjectAreaData(int trackIndex)
    {
        var objectAreaOffset = GetObjectAreaOffset(trackIndex);

        return objectAreaOffset == -1
            ? new byte[TrackObjectAreas.Size]
            : // Ghost Valley track, does not have objects but has pillars (unsupported)
            Utilities.ReadBlock(_romBuffer, objectAreaOffset, TrackObjectAreas.Size);
    }

    private int GetObjectAreaOffset(int trackIndex)
    {
        if (ObjectAreasRelocated) return _offsets[Offset.TrackObjectAreasRelocated] + trackIndex * 10;

        // TODO: Retrieve order dynamically from the ROM
        int[] reorder =
        {
            2, -1 /* Ghost Valley 2 */, 12, 8, 15,
            10, 17, 0, -1 /* Ghost Valley 3 */, 9,
            5, 13, 14, 17, 3,
            1, -1 /* Ghost Valley 1 */, 7, 4, 11
        };

        // NOTE: The 2 bytes at 4DB85 (93DB) are an address (4DB93)
        // to 2 other bytes (CFDA), which are an address (4DACF)
        // to the object areas of Mario Circuit 1. The other tracks follow.
        // But I don't know where the track order is defined.

        if (reorder[trackIndex] == -1)
            // Ghost Valley tracks do not have object data.
            // They have pillar data, which is not supported by this editor.
            return -1;

        var objectAreasOffset = _offsets[Offset.TrackObjectAreas];
        var index = objectAreasOffset + reorder[trackIndex] * 2;
        return Utilities.BytesToOffset(_romBuffer[index], _romBuffer[index + 1], 4);
    }

    private bool ObjectAreasRelocated => _romBuffer[_offsets[Offset.TrackObjectHack6]] == 0xB7;

    #endregion Object areas

    #region AI

    private void LoadAIData(int trackIndex, byte aiOffsetBase, byte[] aiAreaOffsets, byte[] aiTargetOffsets,
        out byte[] aiAreaData, out byte[] aiTargetData)
    {
        var aiOffset = trackIndex * 2;
        int aiAreaAreaOffset;
        int aiTargetDataOffset;

        if (!IsMakeTrack(trackIndex))
        {
            aiAreaAreaOffset =
                Utilities.BytesToOffset(aiAreaOffsets[aiOffset], aiAreaOffsets[aiOffset + 1], aiOffsetBase);
            aiTargetDataOffset =
                Utilities.BytesToOffset(aiTargetOffsets[aiOffset], aiTargetOffsets[aiOffset + 1], aiOffsetBase);
        }
        else
        {
            var aiAreaDataOffsetIndex = _offsets[Offset.MakeAIArea] + trackIndex * 3;
            var aiTargetDataOffsetIndex = _offsets[Offset.MakeAITarget] + trackIndex * 3;
            aiAreaAreaOffset = Utilities.BytesToOffset(_romBuffer, aiAreaDataOffsetIndex);
            aiTargetDataOffset = Utilities.BytesToOffset(_romBuffer, aiTargetDataOffsetIndex);
        }

        aiAreaData = Utilities.ReadBlockUntil(_romBuffer, aiAreaAreaOffset, 0xFF);
        var aiTargetDataLength = TrackAI.GetTargetDataLength(aiAreaData);
        aiTargetData = Utilities.ReadBlock(_romBuffer, aiTargetDataOffset, aiTargetDataLength);
    }

    #endregion AI

    #endregion Get / set, load / save specific data

    #region Track reordering

    public void ReorderTracks(int sourceTrackGroupId, int sourceTrackId, int destinationTrackGroupId,
        int destinationTrackId)
    {
        if (sourceTrackGroupId == destinationTrackGroupId &&
            sourceTrackId == destinationTrackId)
            return;

        // TODO: This method is complex and could be simplified a lot.
        // At the moment, it reorders tracks and updates the ROM data to reflect the reordering.
        // Instead, it could only reorder the track objects, and let the SaveRom method update all the data in the ROM.
        // This would also allow us to move this method to the TrackGroups class.

        if (sourceTrackGroupId < GPTrack.GroupCount) // GP track reordering
        {
            ReorderGPTracks(sourceTrackGroupId, sourceTrackId, destinationTrackGroupId, destinationTrackId);

            #region GP track specific data update

            // Update Time Trial lap line positions
            var startingLineOffset = _offsets[Offset.TrackPreviewLapLines];
            byte[] sourceTrackStartingLine =
            {
                _romBuffer[startingLineOffset + sourceTrackId * 2],
                _romBuffer[startingLineOffset + sourceTrackId * 2 + 1]
            };

            if (sourceTrackId < destinationTrackId)
                Buffer.BlockCopy(
                    _romBuffer,
                    startingLineOffset + (sourceTrackId + 1) * 2,
                    _romBuffer,
                    startingLineOffset + sourceTrackId * 2,
                    (destinationTrackId - sourceTrackId) * 2
                );
            else
                Buffer.BlockCopy(
                    _romBuffer,
                    startingLineOffset + destinationTrackId * 2,
                    _romBuffer,
                    startingLineOffset + (destinationTrackId + 1) * 2,
                    (sourceTrackId - destinationTrackId) * 2
                );

            _romBuffer[startingLineOffset + destinationTrackId * 2] = sourceTrackStartingLine[0];
            _romBuffer[startingLineOffset + destinationTrackId * 2 + 1] = sourceTrackStartingLine[1];

            #endregion GP track specific data update
        }
        else // Battle track reordering
        {
            ReorderBattleTracks(sourceTrackId, destinationTrackId);

            #region Battle track specific data update

            var trackOrderOffset = _offsets[Offset.BattleTrackOrder];

            // Update the track shown by default when entering the battle track selection
            _romBuffer[_offsets[Offset.FirstBattleTrack]] = _romBuffer[trackOrderOffset];

            // Update the selection cursor positions of the battle track selection
            for (byte i = 0; i < BattleTrack.Count; i++)
            {
                var value = (byte)(_romBuffer[trackOrderOffset + i] - 0x14);
                _romBuffer[trackOrderOffset + BattleTrack.Count + value] = i;
            }

            #endregion Battle track specific data update
        }

        MarkAsModified();

        TracksReordered?.Invoke(this, EventArgs.Empty);
    }

    private void ReorderGPTracks(int sourceTrackGroupId, int sourceTrackId, int destinationTrackGroupId,
        int destinationTrackId)
    {
        #region Global track array creation

        // To make the treatment easier, we simply create an array with all the GP tracks
        var tracks = new Track[GPTrack.Count];
        for (var i = 0; i < TrackGroups.Count - 1; i++)
        {
            var trackGroup = TrackGroups[i];

            for (var j = 0; j < trackGroup.Count; j++) tracks[i * trackGroup.Count + j] = trackGroup[j];
        }

        sourceTrackId = sourceTrackGroupId * GPTrack.CountPerGroup + sourceTrackId;
        destinationTrackId = destinationTrackGroupId * GPTrack.CountPerGroup + destinationTrackId;

        #endregion Global track array creation

        ReorderTracks(tracks, sourceTrackId, destinationTrackId, _offsets[Offset.GPTrackOrder]);

        #region Update track pointers in track groups

        for (var i = 0; i < TrackGroups.Count - 1; i++)
        {
            var trackGroup = TrackGroups[i];

            for (var j = 0; j < trackGroup.Count; j++) trackGroup[j] = tracks[i * trackGroup.Count + j];
        }

        #endregion Update track pointers in track groups
    }

    private void ReorderBattleTracks(int sourceTrackId, int destinationTrackId)
    {
        #region Track array creation

        var tracks = new Track[BattleTrack.Count];

        for (var i = 0; i < tracks.Length; i++) tracks[i] = TrackGroups[GPTrack.GroupCount][i];

        #endregion Track array creation

        ReorderTracks(tracks, sourceTrackId, destinationTrackId, _offsets[Offset.BattleTrackOrder]);

        #region Update track pointers in track groups

        var trackGroup = TrackGroups[GPTrack.GroupCount];

        for (var i = 0; i < trackGroup.Count; i++) trackGroup[i] = tracks[i];

        #endregion Update track pointers in track groups
    }

    private void ReorderTracks(Track[] tracks, int sourceTrackId, int destinationTrackId, int trackOrderOffset)
    {
        var sourceTrack = tracks[sourceTrackId];
        var sourceTrackOrder = _romBuffer[trackOrderOffset + sourceTrackId];

        if (sourceTrackId < destinationTrackId)
            for (var i = sourceTrackId; i < destinationTrackId; i++)
                RemapTrack(tracks, i + 1, i, trackOrderOffset);
        else
            for (var i = sourceTrackId; i > destinationTrackId; i--)
                RemapTrack(tracks, i - 1, i, trackOrderOffset);

        tracks[destinationTrackId] = sourceTrack;
        _romBuffer[trackOrderOffset + destinationTrackId] = sourceTrackOrder;
    }

    private void RemapTrack(Track[] tracks, int sourceTrackId, int destinationTrackId, int trackOrderOffset)
    {
        tracks[destinationTrackId] = tracks[sourceTrackId];
        _romBuffer[trackOrderOffset + destinationTrackId] = _romBuffer[trackOrderOffset + sourceTrackId];
    }

    #endregion Track reordering

    #region Save data

    public void SaveRom()
    {
        SaveRom(FilePath);
    }

    public void SaveRom(string filePath)
    {
        FilePath = filePath;

        SaveDataToBuffer();
        SetChecksum();
        SaveFile();
        ResetModifiedState();
    }

    private void SaveDataToBuffer()
    {
        var saveBuffer = new SaveBuffer(_romBuffer);
        SaveBattleStartPositions(saveBuffer);
        SaveObjectData(saveBuffer);
        SaveTileGenres(saveBuffer);
        SaveAIs(saveBuffer);
        SaveTracks(saveBuffer);
        SaveThemes(saveBuffer);
        _romBuffer = saveBuffer.GetRomBuffer();

        Settings.Save(_romBuffer);
        SaveCupAndTrackNames();
        ResetTrackLoadingLogic();
    }

    private void SetChecksum()
    {
        var romSizes = new[]
        {
            RomSize.Size256, RomSize.Size512, RomSize.Size1024, RomSize.Size2048, RomSize.Size4096, RomSize.Size8192
        };

        var isExactSize = false;
        var sizeIndex = 0;
        for (; sizeIndex < romSizes.Length; sizeIndex++)
        {
            if (_romBuffer.Length == romSizes[sizeIndex])
            {
                isExactSize = true;
                break;
            }

            if (_romBuffer.Length < romSizes[sizeIndex]) break;
        }

        // Set the ROM size
        //  8 =  2 Mb
        //  9 =  4 Mb
        // 10 =  8 Mb
        // 11 = 16 Mb
        // 12 = 32 Mb
        // 13 = 64 Mb
        _romBuffer[RomSizeOffset] = (byte)(sizeIndex + 8);

        // Reset the checksum in case it is corrupted
        _romBuffer[ChecksumOffset1] = 0xFF;
        _romBuffer[ChecksumOffset2] = 0xFF;
        _romBuffer[ChecksumOffset3] = 0x00;
        _romBuffer[ChecksumOffset4] = 0x00;

        var end = isExactSize ? romSizes[sizeIndex] : romSizes[sizeIndex - 1];

        var total = 0;
        for (var index = 0; index < end; index++) total += _romBuffer[index];

        if (!isExactSize)
        {
            var sizeLeft = _romBuffer.Length - end;
            var multiplier = (romSizes[sizeIndex] - romSizes[sizeIndex - 1]) / sizeLeft;
            var lastPartTotal = 0;
            for (var index = end; index < _romBuffer.Length; index++) lastPartTotal += _romBuffer[index];
            total += lastPartTotal * multiplier;
        }

        _romBuffer[ChecksumOffset3] = (byte)(total & 0xFF);
        _romBuffer[ChecksumOffset4] = (byte)((total & 0xFF00) >> 8);
        _romBuffer[ChecksumOffset1] = (byte)(0xFF - _romBuffer[ChecksumOffset3]);
        _romBuffer[ChecksumOffset2] = (byte)(0xFF - _romBuffer[ChecksumOffset4]);
    }

    private void SaveBattleStartPositions(SaveBuffer saveBuffer)
    {
        // Saves data from 0x80000 to 0x80061
        var trackOrder = GetTrackOrder();

        var trackGroup = TrackGroups[GPTrack.GroupCount];

        for (var i = 0; i < trackGroup.Count; i++)
        {
            var iterator = GPTrack.Count + i;
            int trackIndex = trackOrder[iterator];
            var bTrackIndex = trackIndex - GPTrack.Count;

            SaveBattleStartPositions((BattleTrack)trackGroup[bTrackIndex], saveBuffer);
        }

        RelocateBattleStartPositions(saveBuffer);
    }

    private void SaveBattleStartPositions(BattleTrack track, SaveBuffer saveBuffer)
    {
        var startPositionP1Data = track.StartPositionP1.GetBytes();
        var startPositionP2Data = track.StartPositionP2.GetBytes();
        saveBuffer.Add(startPositionP1Data);
        saveBuffer.Add(startPositionP2Data);
    }

    private void RelocateBattleStartPositions(SaveBuffer saveBuffer)
    {
        // Relocate the battle track starting positions (to 0x80000).
        var offset = _offsets[Offset.BattleTrackStartPositionsIndex];
        _romBuffer[offset++] = 0x5C;
        _romBuffer[offset++] = 0x20;
        _romBuffer[offset++] = 0x00;
        _romBuffer[offset] = 0xC8;

        // Some values differ depending on the ROM region
        var diff = _region == Region.Jap ? (byte)0x14 : _region == Region.Euro ? (byte)0x19 : (byte)0;

        var val1 = (byte)(0x79 + diff);
        var val2 = (byte)(0x26 + diff);
        var val3 = (byte)(0x18 + diff);

        // New code for battle track starting positions
        byte[] hack =
        {
            0xAD, 0x24, 0x01, 0xC9, 0x14, 0x00, 0x90, 0x35,
            0xE9, 0x14, 0x00, 0x0A, 0x0A, 0x0A, 0xAA, 0xBF,
            0x00, 0x00, 0xC8, 0x8D, 0x18, 0x10, 0xBF, 0x02,
            0x00, 0xC8, 0x8D, 0x1C, 0x10, 0xBF, 0x04, 0x00,
            0xC8, 0x8D, 0x18, 0x11, 0xBF, 0x06, 0x00, 0xC8,
            0x8D, 0x1C, 0x11, 0x9C, 0x20, 0x10, 0x9C, 0x20,
            0x11, 0xAD, 0x24, 0x01, 0x0A, 0xAA, 0xBC, val1,
            0x8A, 0x5C, val2, 0x8F, 0x81, 0x0A, 0x5C, val3,
            0x8F, 0x81
        };

        saveBuffer.Add(hack);
    }

    /// <summary>
    ///     Saves all the object data (locations, areas, properties).
    ///     Also applies hacks that make the track object engine more flexible.
    /// </summary>
    private void SaveObjectData(SaveBuffer saveBuffer)
    {
        /*
            The hacks below include the following improvements:

            - Make all tracks have independent object areas (Koopa Beach 1 and 2 share the same one in the original game)
            - Make it possible to change the object type of each track (regular or GV pillar)
            - Remove Donut Plains pipe / mole hacks with something cleaner and reusable
            - Make it possible to mix the properties of each object (tileset, interaction, routine)
            - Make it possible to define the object palette used for each track
            - Make it possible to add Rainbow Road Thwomp-like flashing (palette cycler) for each track
        */

        RelocateObjectData();
        SaveObjectProperties(saveBuffer); // From 0x80062 to 0x800D9
        AddObjectCodeChunk1(saveBuffer, _region); // From 0x800DA to 0x80218
        SaveObjectLocationsAndAreas(saveBuffer); // From 0x80219 to 0x802E1
        AddObjectCodeChunk2(saveBuffer); // From 0x802E2 to 0x80330
        SavePillars(saveBuffer); // From 0x80331 to 0x85D30
        AddObjectCodeChunk3(saveBuffer, _region); // From 0x85D31 to 0x85D43
        SaveObjectPalettes(saveBuffer); // From 0x85D44 to 0x85DBB
        AddObjectPaletteCodeChunk(saveBuffer, _region); // From 0x85DBC to 0x85EFC
    }

    private void RelocateObjectData()
    {
        /*
            Update addresses to point to the relocated data:

            table tileset: c80062
            table interact: c8007a
            table routine: c80092
            table Z: c800aa
            table loading: c800c2
            A ptr: c800da
            B ptr: c80123
            C ptr: c8014f
            D ptr: c801ab
            normal area table: c80219
            GV checkpoint table: c80331
            GV position data: c80d31
            E ptr: c85d31
            palettes: c85d44
            palette cycle flags: c85da4
            pw1: c85dbc
            pw2: c85e1d
            pw3: c85e49
            co: c85e84
            getcp: c85ed1
        */

        var offset = _offsets[Offset.TrackObjectHack1];
        _romBuffer[offset++] = 0x5C;
        _romBuffer[offset++] = 0x23;
        _romBuffer[offset++] = 0x01;
        _romBuffer[offset] = 0xC8;

        offset = _offsets[Offset.TrackObjectHack2];
        _romBuffer[offset++] = 0x5C;
        _romBuffer[offset++] = 0x4F;
        _romBuffer[offset++] = 0x01;
        _romBuffer[offset] = 0xC8;

        offset = _offsets[Offset.TrackObjectHack3];
        _romBuffer[offset++] = 0x5C;
        _romBuffer[offset++] = 0x31;
        _romBuffer[offset++] = 0x5D;
        _romBuffer[offset] = 0xC8;

        offset = _offsets[Offset.TrackObjectHack4];
        _romBuffer[offset++] = 0x5C;
        _romBuffer[offset++] = 0xDA;
        _romBuffer[offset++] = 0x00;
        _romBuffer[offset] = 0xC8;

        offset = _offsets[Offset.TrackObjectHack5];
        _romBuffer[offset++] = 0x5C;
        _romBuffer[offset++] = 0xAB;
        _romBuffer[offset++] = 0x01;
        _romBuffer[offset] = 0xC8;

        _romBuffer[_offsets[Offset.TrackObjectHack6]] = 0xB7;
        _romBuffer[_offsets[Offset.TrackObjectHack7]] = 0xB7;
        _romBuffer[_offsets[Offset.TrackObjectHack8]] = 0xB7;

        // Object palette changes:

        offset = _offsets[Offset.TrackObjectPalHack1];
        _romBuffer[offset++] = 0x5C;
        _romBuffer[offset++] = 0xBC;
        _romBuffer[offset++] = 0x5D;
        _romBuffer[offset] = 0xC8;

        offset = _offsets[Offset.TrackObjectPalHack2];
        _romBuffer[offset++] = 0x5C;
        _romBuffer[offset++] = 0x49;
        _romBuffer[offset++] = 0x5E;
        _romBuffer[offset] = 0xC8;

        offset = _offsets[Offset.TrackObjectPalHack3];
        _romBuffer[offset++] = 0x5C;
        _romBuffer[offset++] = 0x1D;
        _romBuffer[offset++] = 0x5E;
        _romBuffer[offset] = 0xC8;

        _romBuffer[_offsets[Offset.TrackObjectPalHack4]] = 0x80;
    }

    private void SaveObjectProperties(SaveBuffer saveBuffer)
    {
        var trackOrder = GetTrackOrder();

        var tilesetData = new byte[Track.Count];
        var interactData = new byte[Track.Count];
        var routineData = new byte[Track.Count];
        var zData = new byte[Track.Count];
        var loadingData = new byte[Track.Count];

        for (var i = 0; i < TrackGroups.Count - 1; i++)
        {
            var trackGroup = TrackGroups[i];

            for (var j = 0; j < trackGroup.Count; j++)
            {
                int trackIndex = trackOrder[i * GPTrack.CountPerGroup + j];
                var gpTrack = (GPTrack)trackGroup[j];

                tilesetData[trackIndex] = (byte)gpTrack.Objects.Tileset;
                interactData[trackIndex] = (byte)gpTrack.Objects.Interaction;
                routineData[trackIndex] = (byte)gpTrack.Objects.Routine;
                zData[trackIndex] = routineData[trackIndex];
                loadingData[trackIndex] = (byte)gpTrack.Objects.Loading;
            }
        }

        // Mark battle tracks as not having objects
        const byte noObject = (byte)TrackObjectLoading.None;
        loadingData[GPTrack.Count] = noObject;
        loadingData[GPTrack.Count + 1] = noObject;
        loadingData[GPTrack.Count + 2] = noObject;
        loadingData[GPTrack.Count + 3] = noObject;

        saveBuffer.Add(tilesetData);
        saveBuffer.Add(interactData);
        saveBuffer.Add(routineData);
        saveBuffer.Add(zData);
        saveBuffer.Add(loadingData);
    }

    private static void AddObjectCodeChunk1(SaveBuffer saveBuffer, Region region)
    {
        byte[] hack1 =
        {
            0xE2, 0x30, 0xAE, 0x24, 0x01, 0xBF, 0x7A, 0x00,
            0xC8, 0xAA, 0xBF, 0x15, 0x01, 0xC8, 0x8D, 0x30,
            0x0E, 0x9C, 0x31, 0x0E, 0xAE, 0x24, 0x01, 0xBF,
            0x62, 0x00, 0xC8, 0xAA, 0xBF, 0x1C, 0x01, 0xC8,
            0x48, 0x4A, 0x18, 0x63, 0x01, 0x83, 0x01, 0x68,
            0xC2, 0x30, 0x29, 0xFF, 0x00, 0xAA, 0xBF
        };

        var hack2 =
            region == Region.Jap ? new byte[]
            {
                0xD7, 0xEB, 0x81, 0xA8, 0xBF, 0xD9, 0xEB, 0x81,
                0x5C, 0x9C, 0xE9, 0x81, 0x02, 0x00, 0x0C, 0x04,
                0x06, 0x0A, 0x0C, 0x02, 0x00, 0x0C, 0x04, 0x06,
                0x0A, 0x0E, 0xE2, 0x30, 0xAE, 0x24, 0x01, 0xBF,
                0xAA, 0x00, 0xC8, 0xAA, 0xBF, 0x48, 0x01, 0xC8,
                0xAA, 0xC2, 0x20, 0xBF, 0xE1, 0x8B, 0x81, 0x8D,
                0xFC, 0x0F, 0xBF, 0xF1, 0x8B, 0x81, 0x8D, 0xFE,
                0x0F, 0xC2, 0x30, 0x5C, 0xFF, 0x8E, 0x81, 0x02,
                0x00, 0x0C, 0x04, 0x06, 0x0A, 0x0C, 0xE2, 0x30,
                0xAE, 0x24, 0x01, 0xBF, 0x92, 0x00, 0xC8, 0x0A,
                0x48, 0x0A, 0x18, 0x63, 0x01, 0x83, 0x01, 0xFA,
                0xC2, 0x20, 0xBF, 0x81, 0x01, 0xC8, 0x85, 0x06,
                0xBF, 0x83, 0x01, 0xC8, 0x8D, 0x98, 0x0F, 0x85,
                0x08, 0xBF, 0x85, 0x01, 0xC8, 0x8D, 0x9A, 0x0F,
                0x85, 0x0A, 0xC2, 0x10, 0x5C, 0x6A, 0x91, 0x81,
                0xE4, 0x91, 0x0C, 0xE5, 0x1C, 0xE5, 0xE4, 0x91,
                0x04, 0xE7, 0x0E, 0xE7, 0xE4, 0x91, 0xB0, 0xE1,
                0xC2, 0xE1, 0xF8, 0x91, 0x90, 0xE3, 0xA0, 0xE3,
                0xE4, 0x91, 0x93, 0xE0, 0x9F, 0xE0, 0xD4, 0x91,
                0xA3, 0xDF, 0xB3, 0xDF, 0xE4, 0x91, 0x69, 0xE1,
                0x7B
            } :
            region == Region.Euro ? new byte[]
            {
                0xC2, 0xEB, 0x81, 0xA8, 0xBF, 0xC4, 0xEB, 0x81,
                0x5C, 0x87, 0xE9, 0x81, 0x02, 0x00, 0x0C, 0x04,
                0x06, 0x0A, 0x0C, 0x02, 0x00, 0x0C, 0x04, 0x06,
                0x0A, 0x0E, 0xE2, 0x30, 0xAE, 0x24, 0x01, 0xBF,
                0xAA, 0x00, 0xC8, 0xAA, 0xBF, 0x48, 0x01, 0xC8,
                0xAA, 0xC2, 0x20, 0xBF, 0xE6, 0x8B, 0x81, 0x8D,
                0xFC, 0x0F, 0xBF, 0xF6, 0x8B, 0x81, 0x8D, 0xFE,
                0x0F, 0xC2, 0x30, 0x5C, 0x01, 0x8F, 0x81, 0x02,
                0x00, 0x0C, 0x04, 0x06, 0x0A, 0x0C, 0xE2, 0x30,
                0xAE, 0x24, 0x01, 0xBF, 0x92, 0x00, 0xC8, 0x0A,
                0x48, 0x0A, 0x18, 0x63, 0x01, 0x83, 0x01, 0xFA,
                0xC2, 0x20, 0xBF, 0x81, 0x01, 0xC8, 0x85, 0x06,
                0xBF, 0x83, 0x01, 0xC8, 0x8D, 0x98, 0x0F, 0x85,
                0x08, 0xBF, 0x85, 0x01, 0xC8, 0x8D, 0x9A, 0x0F,
                0x85, 0x0A, 0xC2, 0x10, 0x5C, 0x6F, 0x91, 0x81,
                0xE9, 0x91, 0x0C, 0xE5, 0x1C, 0xE5, 0xE9, 0x91,
                0x23, 0xE7, 0x2D, 0xE7, 0xE9, 0x91, 0xB0, 0xE1,
                0xC2, 0xE1, 0xFD, 0x91, 0x90, 0xE3, 0xA0, 0xE3,
                0xE9, 0x91, 0x93, 0xE0, 0x9F, 0xE0, 0xD9, 0x91,
                0xA3, 0xDF, 0xB3, 0xDF, 0xE9, 0x91, 0x69, 0xE1,
                0x7B
            } :
            new byte[] // Region.US
            {
                0xD3, 0xEB, 0x81, 0xA8, 0xBF, 0xD5, 0xEB, 0x81,
                0x5C, 0x98, 0xE9, 0x81, 0x02, 0x00, 0x0C, 0x04,
                0x06, 0x0A, 0x0C, 0x02, 0x00, 0x0C, 0x04, 0x06,
                0x0A, 0x0E, 0xE2, 0x30, 0xAE, 0x24, 0x01, 0xBF,
                0xAA, 0x00, 0xC8, 0xAA, 0xBF, 0x48, 0x01, 0xC8,
                0xAA, 0xC2, 0x20, 0xBF, 0xCD, 0x8B, 0x81, 0x8D,
                0xFC, 0x0F, 0xBF, 0xDD, 0x8B, 0x81, 0x8D, 0xFE,
                0x0F, 0xC2, 0x30, 0x5C, 0xEB, 0x8E, 0x81, 0x02,
                0x00, 0x0C, 0x04, 0x06, 0x0A, 0x0C, 0xE2, 0x30,
                0xAE, 0x24, 0x01, 0xBF, 0x92, 0x00, 0xC8, 0x0A,
                0x48, 0x0A, 0x18, 0x63, 0x01, 0x83, 0x01, 0xFA,
                0xC2, 0x20, 0xBF, 0x81, 0x01, 0xC8, 0x85, 0x06,
                0xBF, 0x83, 0x01, 0xC8, 0x8D, 0x98, 0x0F, 0x85,
                0x08, 0xBF, 0x85, 0x01, 0xC8, 0x8D, 0x9A, 0x0F,
                0x85, 0x0A, 0xC2, 0x10, 0x5C, 0x56, 0x91, 0x81,
                0xD0, 0x91, 0xE7, 0xE4, 0xF7, 0xE4, 0xD0, 0x91,
                0xFE, 0xE6, 0x08, 0xE7, 0xD0, 0x91, 0x8B, 0xE1,
                0x9D, 0xE1, 0xE4, 0x91, 0x6B, 0xE3, 0x7B, 0xE3,
                0xD0, 0x91, 0x6E, 0xE0, 0x7A, 0xE0, 0xC0, 0x91,
                0x7E, 0xDF, 0x8E, 0xDF, 0xD0, 0x91, 0x44, 0xE1,
                0x56
            };

        byte[] hack3 =
        {
            0xE1, 0xE2, 0x30, 0xAE, 0x24, 0x01, 0xBF, 0xC2,
            0x00, 0xC8, 0x0A, 0xAA, 0xFC, 0xBF, 0x01, 0xC2,
            0x30, 0x5C, 0xC2, 0xDA, 0x84, 0xC8, 0x01, 0xE1,
            0x02, 0xF5, 0x02, 0xC7, 0x01, 0x60, 0xC2, 0x30,
            0xAD, 0x24, 0x01, 0x0A, 0x0A, 0x18, 0x6D, 0x24,
            0x01, 0x0A, 0x18, 0x69, 0x19, 0x02, 0x85, 0x08,
            0xA9, 0xC8, 0x00, 0x85, 0x0A, 0xAC, 0xE4, 0x1E,
            0xB6, 0xC8, 0x10, 0x11, 0xA5, 0x08, 0x18, 0x69,
            0x05, 0x00, 0x85, 0x08, 0x98, 0x49, 0x02, 0x00,
            0xA8, 0xB6, 0xC8, 0x30, 0x20, 0xA0, 0x00, 0x00,
            0xE2, 0x20, 0xB5, 0xC0, 0xC9, 0xFF, 0xF0, 0x06,
            0x88, 0xC8, 0xD7, 0x08, 0xB0, 0xFB, 0xC2, 0x20,
            0xF4, 0x6B, 0x00, 0x3B, 0x4B, 0xF4, 0x16, 0x02,
            0x48, 0x5C, 0x0B, 0xDC, 0x84, 0x68, 0x60
        };

        saveBuffer.Add(hack1);
        saveBuffer.Add(hack2);
        saveBuffer.Add(hack3);
    }

    private void SaveObjectLocationsAndAreas(SaveBuffer saveBuffer)
    {
        var trackOrder = GetTrackOrder();
        var objectAreasData = new byte[GPTrack.Count * TrackObjectAreas.Size];

        for (var i = 0; i < TrackGroups.Count - 1; i++)
        {
            var trackGroup = TrackGroups[i];

            for (var j = 0; j < trackGroup.Count; j++)
            {
                int trackIndex = trackOrder[i * GPTrack.CountPerGroup + j];
                var gpTrack = (GPTrack)trackGroup[j];

                // Update object areas
                var data = gpTrack.Objects.Areas.GetBytes();
                Buffer.BlockCopy(data, 0, objectAreasData,
                    trackIndex * data.Length,
                    data.Length);

                if (gpTrack.Modified) // Avoid saving data if not necessary
                {
                    // Update object coordinates
                    data = gpTrack.Objects.GetBytes();
                    Buffer.BlockCopy(data, 0, _romBuffer, _offsets[Offset.TrackObjects] + trackIndex * 64, data.Length);
                }
            }
        }

        saveBuffer.Add(objectAreasData);
    }

    private static void AddObjectCodeChunk2(SaveBuffer saveBuffer)
    {
        byte[] hack =
        {
            0x20, 0xC8, 0x01, 0x90, 0x0E, 0xF4, 0x6B, 0x00,
            0x3B, 0x4B, 0xF4, 0xF2, 0x02, 0x48, 0x5C, 0xBC,
            0xDB, 0x84, 0x68, 0x60, 0xC2, 0x30, 0xAC, 0xE4,
            0x1E, 0xB6, 0xC8, 0x30, 0x32, 0xB9, 0x7C, 0xDC,
            0x85, 0x0C, 0xAD, 0x24, 0x01, 0xEB, 0x4A, 0x85,
            0x04, 0x0A, 0x0A, 0x0A, 0x18, 0x69, 0x31, 0x0D,
            0x85, 0x08, 0xA5, 0x04, 0x18, 0x69, 0x31, 0x03,
            0x85, 0x04, 0xA9, 0xC8, 0x00, 0x85, 0x06, 0x85,
            0x0A, 0xF4, 0x6B, 0x00, 0x3B, 0x4B, 0xF4, 0x2E,
            0x03, 0x48, 0x5C, 0xA3, 0xDC, 0x84, 0x68, 0x60
        };

        saveBuffer.Add(hack);
    }

    private void SavePillars(SaveBuffer saveBuffer)
    {
        // TODO: Load and save GV pillar data

        // GV checkpoint table: c80331 (20 * 128 bytes)
        // GV position data: c80d31 (20 * 1024 bytes)

        var data = new byte[GPTrack.Count * (128 + 1024)];

        // Copy original pillar data to new location
        Buffer.BlockCopy(_romBuffer, 0x4DE2E, data, 128, 128);
        Buffer.BlockCopy(_romBuffer, 0x4DF08, data, 1024, 128);
        Buffer.BlockCopy(_romBuffer, 0x4DD91, data, 2048, 128);
        Buffer.BlockCopy(_romBuffer, 0x4DDB4, data, 3584, 172);
        Buffer.BlockCopy(_romBuffer, 0x4DDB4, data, 6656, 120);
        Buffer.BlockCopy(_romBuffer, 0x4DE60, data, 10752, 166);
        Buffer.BlockCopy(_romBuffer, 0x4DD1D, data, 18944, 114);

        saveBuffer.Add(data);
    }

    private static void AddObjectCodeChunk3(SaveBuffer saveBuffer, Region region)
    {
        // A value differs depending on the ROM region
        var val = region == Region.Jap ? (byte)0x94 : region == Region.Euro ? (byte)0x6E : (byte)0x31;

        byte[] hack =
        {
            0xDA, 0xAE, 0x24, 0x01, 0xE2, 0x20, 0xBF, 0x7A,
            0x00, 0xC8, 0xFA, 0xC9, 0x06, 0xC2, 0x20, 0x5C,
            val, 0x9E, 0x81
        };

        saveBuffer.Add(hack);
    }

    private void SaveObjectPalettes(SaveBuffer saveBuffer)
    {
        var trackOrder = GetTrackOrder();
        var objectPalData = new byte[Track.Count * 5];
        const int flashingOffset = Track.Count * 4;

        for (var i = 0; i < TrackGroups.Count - 1; i++)
        {
            var trackGroup = TrackGroups[i];

            for (var j = 0; j < trackGroup.Count; j++)
            {
                int trackIndex = trackOrder[i * GPTrack.CountPerGroup + j];
                var offset = trackIndex * 4;
                var gpTrack = (GPTrack)trackGroup[j];

                objectPalData[offset++] = (byte)(gpTrack.Objects.PaletteIndexes[0] << 1);
                objectPalData[offset++] = (byte)(gpTrack.Objects.PaletteIndexes[1] << 1);
                objectPalData[offset++] = (byte)(gpTrack.Objects.PaletteIndexes[2] << 1);
                objectPalData[offset] = (byte)(gpTrack.Objects.PaletteIndexes[3] << 1);

                objectPalData[flashingOffset + trackIndex] =
                    gpTrack.Objects.Flashing ? (byte)1 : (byte)0;
            }
        }

        saveBuffer.Add(objectPalData);
    }

    private static void AddObjectPaletteCodeChunk(SaveBuffer saveBuffer, Region region)
    {
        byte val1;
        byte val2;

        if (region == Region.Us)
        {
            val1 = 0x2A;
            val2 = 0x8F;
        }
        else
        {
            val1 = 0x4F;
            val2 = 0xB4;
        }

        byte[] hack1 =
        {
            0x20, 0x84, 0x5E, 0xD0, 0x3C, 0xD4, 0x00, 0x20,
            0xD1, 0x5E, 0xB9, 0x06, 0x00, 0x45, 0x1A, 0x29,
            0xFF, 0xF1, 0x05, 0x00, 0x95, 0x0E, 0xB9, 0x04,
            0x00, 0x45, 0x1A, 0x29, 0xFF, 0xF1, 0x05, 0x00,
            0x95, 0x0A, 0xB9, 0x02, 0x00, 0x45, 0x1A, 0x29,
            0xFF, 0xF1, 0x05, 0x00, 0x95, 0x06, 0xB9, 0x00,
            0x00, 0x45, 0x1A, 0x29, 0xFF, 0xF1, 0x05, 0x00,
            0x95, 0x02, 0x68, 0x85, 0x00, 0x5C, val1, 0xBD,
            0x80, 0xB9, 0x06, 0x00, 0x45, 0x1A, 0x95, 0x0E,
            0xB9, 0x04, 0x00, 0x45, 0x1A, 0x95, 0x0A, 0xB9,
            0x02, 0x00, 0x45, 0x1A, 0x95, 0x06, 0xB9, 0x00,
            0x00, 0x45, 0x1A, 0x95, 0x02, 0x5C, val1, 0xBD,
            0x80, 0x20, 0x84, 0x5E, 0xD0, 0x1A, 0xD4, 0x00,
            0x20, 0xD1, 0x5E, 0xBD, 0x02, 0x00, 0xA6, 0x3C,
            0x45, 0x1A, 0x29, 0xFF, 0xF1, 0x05, 0x00, 0x95,
            0x02, 0x68, 0x85, 0x00, 0x5C, val2, 0xBD, 0x80,
            0xBD, 0x02, 0x00, 0xA6, 0x3C, 0x45, 0x1A, 0x95,
            0x02, 0x5C, val2, 0xBD, 0x80, 0x20, 0x84, 0x5E,
            0xD0, 0x24, 0xD4, 0x00, 0x20, 0xD1, 0x5E, 0xB9,
            0x02, 0x00, 0x45, 0x1A, 0x29, 0xFF, 0xF1, 0x05,
            0x00, 0x95, 0x06, 0xB9, 0x00, 0x00, 0x45, 0x1A,
            0x29, 0xFF, 0xF1, 0x05, 0x00, 0x95, 0x02, 0x68,
            0x85, 0x00, 0x5C, val1, 0xBD, 0x80, 0xB9, 0x02,
            0x00, 0x45, 0x1A, 0x95, 0x06, 0xB9, 0x00, 0x00,
            0x45, 0x1A, 0x95, 0x02, 0x5C, val1, 0xBD, 0x80,
            0xDA, 0xA6, 0xB4, 0xB5, 0x04, 0xAE, 0x24, 0x01,
            0x48, 0xBF, 0x92, 0x00, 0xC8, 0x29, 0xFF, 0x00,
            0x0A, 0xAA, 0x68, 0xDF, 0xB5, 0x5E, 0xC8, 0xF0,
            0x11, 0xDF, 0xC3, 0x5E, 0xC8, 0xF0, 0x0B, 0xC0
        };

        var hack2 =
            region == Region.Jap ? new byte[]
            {
                0x73, 0xE4, 0xF0, 0x06, 0xFA, 0x18, 0xA9, 0x01,
                0x00, 0x60, 0x18, 0xFA, 0x18, 0xA9, 0x00, 0x00,
                0x60, 0x0C, 0xE5, 0x04, 0xE7, 0xB0, 0xE1, 0x90,
                0xE3, 0x93, 0xE0, 0xA3, 0xDF, 0x69, 0xE1, 0x1C,
                0xE5, 0x0E, 0xE7, 0xC2, 0xE1, 0xA0, 0xE3, 0x9F,
                0xE0, 0xB3, 0xDF, 0x7B
            } :
            region == Region.Euro ? new byte[]
            {
                0x7E, 0xE4, 0xF0, 0x06, 0xFA, 0x18, 0xA9, 0x01,
                0x00, 0x60, 0x18, 0xFA, 0x18, 0xA9, 0x00, 0x00,
                0x60, 0x0C, 0xE5, 0x23, 0xE7, 0xB0, 0xE1, 0x90,
                0xE3, 0x93, 0xE0, 0xA3, 0xDF, 0x69, 0xE1, 0x1C,
                0xE5, 0x2D, 0xE7, 0xC2, 0xE1, 0xA0, 0xE3, 0x9F,
                0xE0, 0xB3, 0xDF, 0x7B
            } :
            new byte[] // Region.US
            {
                0x4E, 0xE4, 0xF0, 0x06, 0xFA, 0x18, 0xA9, 0x01,
                0x00, 0x60, 0x18, 0xFA, 0x18, 0xA9, 0x00, 0x00,
                0x60, 0xE7, 0xE4, 0xFE, 0xE6, 0x8B, 0xE1, 0x6B,
                0xE3, 0x6E, 0xE0, 0x7E, 0xDF, 0x44, 0xE1, 0xF7,
                0xE4, 0x08, 0xE7, 0x9D, 0xE1, 0x7B, 0xE3, 0x7A,
                0xE0, 0x8E, 0xDF, 0x56
            };

        byte[] hack3 =
        {
            0xE1, 0xDA, 0xAE, 0x24, 0x01, 0xE2, 0x20, 0xBF,
            0xA4, 0x5D, 0xC8, 0xF0, 0x0E, 0x8A, 0x0A, 0x0A,
            0x85, 0x00, 0xA5, 0x38, 0x29, 0x03, 0x18, 0x65,
            0x00, 0x80, 0x03, 0x8A, 0x0A, 0x0A, 0xAA, 0xBF,
            0x44, 0x5D, 0xC8, 0xC2, 0x20, 0x29, 0xFF, 0x00,
            0xEB, 0x85, 0x00, 0xFA, 0x60
        };

        saveBuffer.Add(hack1);
        saveBuffer.Add(hack2);
        saveBuffer.Add(hack3);
    }

    private void SaveTileGenres(SaveBuffer saveBuffer)
    {
        // Saves data from 0x85EFD to 0x86731

        // Apply a hack that gives a full 256-byte behavior table for each theme
        // in uncompressed form. It helps the load times a little bit,
        // and allows theme-specific tile genre values for shared tiles.
        // Also reimplement the theme-specific behavior of the Browser Castle jump bars
        // that slows you down, to make it reusable.

        /*
            LoadBehaviour: c85efd
            JumpBarCheck: c85f18
            behaviour tables: c85f2a
            jump bar table: c8672a
        */

        // JumpBarCheck offset (make it point to 85F18)
        var offset = _offsets[Offset.JumpBarCheck];
        _romBuffer[offset++] = 0x5C;
        _romBuffer[offset++] = 0x18;
        _romBuffer[offset++] = 0x5F;
        _romBuffer[offset] = 0xC8;

        // LoadBehavior offset (make it point to 85EFD)
        offset = _offsets[Offset.TileGenreLoad];
        _romBuffer[offset++] = 0x5C;
        _romBuffer[offset++] = 0xFD;
        _romBuffer[offset++] = 0x5E;
        _romBuffer[offset] = 0xC8;

        var r = _region;
        var val1 = r == Region.Jap ? (byte)0x4E : r == Region.Euro ? (byte)0x39 : (byte)0x4A;
        var val2 = r == Region.Jap ? (byte)0x9B : r == Region.Euro ? (byte)0xA9 : (byte)0xA4;
        byte[] data =
        {
            0xC2, 0x20, 0xAD, 0x26, 0x01, 0x4A, 0xEB, 0x18,
            0x69, 0x2A, 0x5F, 0xAA, 0xA0, 0x00, 0x0B, 0xA9,
            0xFF, 0x00, 0x8B, 0x54, 0x7E, 0xC8, 0xAB, 0x5C,
            val1, 0xEB, 0xC1, 0xDA, 0xAD, 0x26, 0x01, 0x4A,
            0xAA, 0xBF, 0x2A, 0x67, 0xC8, 0xFA, 0x29, 0xFF,
            0x00, 0x5C, val2, 0xB7, 0xC0
        };

        saveBuffer.Add(data);

        // "behavior tables" is 256 byte behavior tables for each theme.
        foreach (var theme in Themes) saveBuffer.Add(theme.RoadTileset.GetTileGenreBytes());

        // "jump bar table" has 1 byte per theme.
        // If it is zero, then jump bars will slow you down.
        // If it is not zero, they act like they do outside of BC tracks.
        data = new byte[] { 1, 1, 1, 1, 1, 1, 0, 1 };

        saveBuffer.Add(data);
    }

    private void SaveAIs(SaveBuffer saveBuffer)
    {
        var aiFirstAddressByteOffset = _offsets[Offset.TrackAIDataFirstAddressByte];
        _romBuffer[aiFirstAddressByteOffset] = 0xC8;

        var trackOrder = GetTrackOrder();

        for (var i = 0; i < TrackGroups.Count; i++)
        {
            var trackGroup = TrackGroups[i];

            for (var j = 0; j < trackGroup.Count; j++)
            {
                int trackIndex = trackOrder[i * GPTrack.CountPerGroup + j];
                SaveAI(trackGroup[j], trackIndex, saveBuffer);
            }
        }
    }

    private void SaveAI(Track track, int trackIndex, SaveBuffer saveBuffer)
    {
        var trackAIData = track.AI.GetBytes();

        // Update AI offsets
        var trackAIAreaIndex = _offsets[Offset.TrackAIAreas] + trackIndex * 2;
        var trackAITargetIndex = _offsets[Offset.TrackAITargets] + trackIndex * 2;

        var aiAreaOffset = Utilities.OffsetToBytes(saveBuffer.Index);
        var aiTargetOffset = Utilities.OffsetToBytes(saveBuffer.Index + trackAIData.Length - track.AI.ElementCount * 3);

        _romBuffer[trackAIAreaIndex] = aiAreaOffset[0];
        _romBuffer[trackAIAreaIndex + 1] = aiAreaOffset[1];
        _romBuffer[trackAITargetIndex] = aiTargetOffset[0];
        _romBuffer[trackAITargetIndex + 1] = aiTargetOffset[1];

        saveBuffer.Add(trackAIData);
    }

    private void SaveTracks(SaveBuffer saveBuffer)
    {
        var trackOrder = GetTrackOrder();
        var mapOffsets = Utilities.ReadBlockOffset(_romBuffer, _offsets[Offset.TrackMaps], Track.Count);

        for (var i = 0; i < TrackGroups.Count; i++)
        {
            var trackGroup = TrackGroups[i];

            for (var j = 0; j < trackGroup.Count; j++)
            {
                var iterator = i * GPTrack.CountPerGroup + j;
                int trackIndex = trackOrder[iterator];

                if (trackGroup[j].Modified || IsMakeTrack(trackIndex))
                {
                    // HACK: We need to detect whether a track has been modified with MAKE in order to make sure
                    // we compress its track map when resaving the ROM, as MAKE does not compress track maps.
                    // We don't want to bother handling both compressed and uncompressed track maps when resaving them.
                    SaveTrack(trackGroup[j], iterator, trackIndex, saveBuffer);
                }
                else
                {
                    var mapOffset = mapOffsets[trackIndex];

                    if (saveBuffer.Includes(mapOffset)) MoveTrackMap(trackIndex, mapOffset, saveBuffer);
                }
            }
        }
    }

    private void SaveTrack(Track track, int iterator, int trackIndex, SaveBuffer saveBuffer)
    {
        // Update track map
        var compressedMap = Codec.Compress(track.Map.GetBytes(), true, false);
        SaveTrackMap(trackIndex, compressedMap, saveBuffer);

        // Update track theme id
        var themeId = Themes.GetThemeId(track.Theme);
        var themeIdOffset = _offsets[Offset.TrackThemes] + trackIndex;
        _romBuffer[themeIdOffset] = themeId;

        // Update overlay tiles
        var overlayTileData = track.OverlayTiles.GetBytes();
        SaveOverlayTileData(overlayTileData, trackIndex);

        if (track is GPTrack gpTrack)
        {
            // Update driver starting position
            SaveGPStartPositionData(gpTrack, trackIndex);

            // Update lap line position and length
            var lapLineData = gpTrack.LapLine.GetBytes();
            Buffer.BlockCopy(lapLineData, 0, _romBuffer,
                _offsets[Offset.TrackLapLines] + trackIndex * lapLineData.Length, lapLineData.Length);

            // Update lap line position on track preview
            var previewLapLineOffset = _offsets[Offset.TrackPreviewLapLines] + iterator * 2;
            var previewLapLineLocation = GetPreviewLapLineLocation(gpTrack);
            _romBuffer[previewLapLineOffset] = (byte)previewLapLineLocation.X;
            _romBuffer[previewLapLineOffset + 1] = (byte)previewLapLineLocation.Y;

            // Update item probability index
            _romBuffer[_offsets[Offset.TrackItemProbabilityIndexes] + trackIndex] =
                (byte)(gpTrack.ItemProbabilityIndex << 1);
        }
    }

    private static Point GetPreviewLapLineLocation(GPTrack track)
    {
        // Track coordinates:
        const int xTopLeft = 40; // Top-left X value
        const int xBottomLeft = 6; // Bottom-left X value
        const int xBottomRight = 235; // Bottom-right X value
        const int yTop = 16; // Top value
        const int yBottom = 104; // Bottom value

        float yRelative = (1023 - track.LapLine.Y) * (xBottomRight - xBottomLeft) / 1023;
        var y = (int)(yBottom - yRelative * Math.Sin(0.389) - 7);

        var xPercent = (float)(track.StartPosition.X + track.StartPosition.SecondRowOffset / 2) / 1023;
        var yPercent = (float)(y - yTop) / (yBottom - yTop);
        var xStart = (int)(xTopLeft - (xTopLeft - xBottomLeft) * yPercent);
        var mapWidth = xBottomRight - (xStart - xBottomLeft) * 2;
        var x = (int)(xStart + mapWidth * xPercent);
        if (x < (xBottomRight - xBottomLeft) / 2)
            // If the lap line is on the left side, shift its position a bit
            x -= 5;

        return new Point(x, y);
    }

    private void MoveTrackMap(int trackIndex, int trackOffset, SaveBuffer saveBuffer)
    {
        var compressedMap = Codec.GetCompressedChunk(_romBuffer, trackOffset);
        SaveTrackMap(trackIndex, compressedMap, saveBuffer);
    }

    private void SaveTrackMap(int trackIndex, byte[] compressedMap, SaveBuffer saveBuffer)
    {
        // Update track map offset
        var mapOffsetIndex = _offsets[Offset.TrackMaps] + trackIndex * 3;
        saveBuffer.AddCompressed(compressedMap, mapOffsetIndex);
    }

    private void SaveThemes(SaveBuffer saveBuffer)
    {
        // In the original game, a road tileset is composed of 192 theme-specific tiles,
        // followed by 64 tiles that are shared across all themes.
        // Modify the game so that each theme has 256 unique tiles. Nothing is shared anymore.
        _romBuffer[_offsets[Offset.RoadTilesetHack1]] = 0x60;
        _romBuffer[_offsets[Offset.RoadTilesetHack2]] = 0x60;
        _romBuffer[_offsets[Offset.RoadTilesetHack3]] = 0x00;
        _romBuffer[_offsets[Offset.RoadTilesetHack4]] = 0x40;

        for (var i = 0; i < Themes.Count; i++)
        {
            var theme = Themes[i];
            SaveRoadTiles(theme, i, saveBuffer);
            SavePalettes(theme, i, saveBuffer);
            SaveBackgroundLayout(theme, i, saveBuffer);
            SaveBackgroundTiles(theme, i, saveBuffer);
        }
    }

    private void SaveRoadTiles(Theme theme, int themeIndex, SaveBuffer saveBuffer)
    {
        var roadTileGfxIndex = _offsets[Offset.ThemeRoadGraphics] + themeIndex * 3;
        var roadTileGfxOffset = Utilities.BytesToOffset(_romBuffer, roadTileGfxIndex);

        var roadTileGfxData = !theme.RoadTileset.Modified && saveBuffer.Includes(roadTileGfxOffset)
            ? Codec.GetCompressedChunk(_romBuffer, roadTileGfxOffset)
            : // Copy the unchanged compressed data (perf optimization)
            Codec.Compress(theme.RoadTileset.GetBytes()); // Compress the modified data

        saveBuffer.AddCompressed(roadTileGfxData, roadTileGfxIndex);
    }

    private void SavePalettes(Theme theme, int themeIndex, SaveBuffer saveBuffer)
    {
        var palettesIndex = _offsets[Offset.ThemePalettes] + themeIndex * 3;
        var palettesOffset = Utilities.BytesToOffset(_romBuffer, palettesIndex);

        if (theme.Palettes.Modified || saveBuffer.Includes(palettesOffset))
        {
            var palettesData = !theme.Palettes.Modified
                ? Codec.GetCompressedChunk(_romBuffer, palettesOffset)
                : // Copy the unchanged compressed data (perf optimization)
                Codec.Compress(theme.Palettes.GetBytes()); // Compress the modified data

            saveBuffer.AddCompressed(palettesData, palettesIndex);
        }
    }

    private void SaveBackgroundLayout(Theme theme, int themeIndex, SaveBuffer saveBuffer)
    {
        var bgLayoutIndex = _offsets[Offset.ThemeBackgroundLayouts] + themeIndex * 3;
        var bgLayoutOffset = Utilities.BytesToOffset(_romBuffer, bgLayoutIndex);

        if (theme.Background.Layout.Modified || saveBuffer.Includes(bgLayoutOffset))
        {
            var bgLayoutData = !theme.Background.Layout.Modified
                ? Codec.GetCompressedChunk(_romBuffer, bgLayoutOffset)
                : // Copy the unchanged compressed data (perf optimization)
                Codec.Compress(theme.Background.Layout.GetBytes()); // Compress the modified data

            saveBuffer.AddCompressed(bgLayoutData, bgLayoutIndex);
        }
    }

    private void SaveBackgroundTiles(Theme theme, int themeIndex, SaveBuffer saveBuffer)
    {
        var bgTileGfxIndex = _offsets[Offset.ThemeBackgroundGraphics] + themeIndex * 3;
        var bgTileGfxOffset = Utilities.BytesToOffset(_romBuffer, bgTileGfxIndex);

        if (theme.Background.Tileset.Modified || saveBuffer.Includes(bgTileGfxOffset))
        {
            var bgTileGfxData = !theme.Background.Tileset.Modified
                ? Codec.GetCompressedChunk(_romBuffer, bgTileGfxOffset)
                : // Copy the unchanged compressed data (perf optimization)
                Codec.Compress(theme.Background.Tileset.GetBytes()); // Compress the modified data

            saveBuffer.AddCompressed(bgTileGfxData, bgTileGfxIndex);

            if (themeIndex == 0)
            {
                // Update the Ghost Valley animated graphics source
                // to point to the new Ghost Valley background tileset location
                var index = _offsets[Offset.GhostValleyBackgroundAnimationGraphics];
                _romBuffer[index + 3] = _romBuffer[bgTileGfxIndex];
                _romBuffer[index + 4] = _romBuffer[bgTileGfxIndex + 1];
                _romBuffer[index] = _romBuffer[bgTileGfxIndex + 2];
            }
        }
    }

    private void SaveCupAndTrackNames()
    {
        var nameOffset = _offsets[Offset.NamesAndSuffixes];

        // Update battle track names
        var battleTrackNameOffsetIndex = _offsets[Offset.BattleTrackNames] + 2; // Skip leading bytes
        foreach (var track in TrackGroups[GPTrack.GroupCount])
        {
            SaveCupOrTrackName(track.SuffixedNameItem, battleTrackNameOffsetIndex, ref nameOffset);
            battleTrackNameOffsetIndex += 4;
        }

        // Update cup names
        var allCupNameOffsetIndex = _offsets[Offset.CupNames] + 2; // Skip leading bytes
        var lockedCupNameOffsetIndex = _offsets[Offset.CupNamesLocked] + 2; // Skip leading bytes
        for (var i = 0; i < GPTrack.GroupCount; i++)
        {
            // Update cup name + name index (including Special Cup)
            SaveCupOrTrackName(TrackGroups[i].SuffixedNameItem, allCupNameOffsetIndex, ref nameOffset);

            if (i < GPTrack.GroupCount - 1)
            {
                // Update cup name index (excluding Special Cup)
                _romBuffer[lockedCupNameOffsetIndex] = _romBuffer[allCupNameOffsetIndex];
                _romBuffer[lockedCupNameOffsetIndex + 1] = _romBuffer[allCupNameOffsetIndex + 1];
                lockedCupNameOffsetIndex += 4;
            }

            allCupNameOffsetIndex += 4;
        }

        // Update GP track names
        var gpTrackNameOffsetIndex = _offsets[Offset.GPTrackNames];
        for (var i = 0; i < GPTrack.GroupCount; i++)
        {
            gpTrackNameOffsetIndex += 2; // Skip leading bytes

            foreach (var track in TrackGroups[i])
            {
                SaveCupOrTrackName(track.SuffixedNameItem, gpTrackNameOffsetIndex, ref nameOffset);
                gpTrackNameOffsetIndex += 4;
            }
        }
    }

    private void SaveCupOrTrackName(SuffixedTextItem nameItem, int nameOffsetIndex, ref int nameOffset)
    {
        var offsetAddressData = Utilities.OffsetToBytes(nameOffset);
        _romBuffer[nameOffsetIndex] = offsetAddressData[0];
        _romBuffer[nameOffsetIndex + 1] = offsetAddressData[1];

        _romBuffer[nameOffset++] = 0x29;
        _romBuffer[nameOffset++] = (byte)(0xE0 + Settings.CourseSelectTexts.IndexOf(nameItem.TextItem));

        var nameSuffixData = nameItem.TextItem.Converter.EncodeText(nameItem.Suffix.Value, null);

        for (var i = 0; i < nameSuffixData.Length; i++) _romBuffer[nameOffset++] = nameSuffixData[i];

        _romBuffer[nameOffset++] = 0xFF;
    }

    private void ResetTrackLoadingLogic()
    {
        // HACK: MAKE modifies these bytes in order to relocate track-related data.
        // I'm not sure what they are or reference, but they're probably related to track map and AI data.
        // In order to keep MAKE ROMs working when resaving them, we need to reset them to their original values,
        // because we do not save data the way MAKE does.

        var offset1 = _offsets[Offset.MakeDataReset1];
        var offset2 = _offsets[Offset.MakeDataReset2];

        _romBuffer[offset1] = _region == Region.Us ? (byte)0x9E : (byte)0x41;
        _romBuffer[offset1 + 1] = _region == Region.Us ? (byte)0xE0 : (byte)0xDF;
        _romBuffer[offset1 + 2] = _region == Region.Us ? (byte)0x84 : (byte)0x84;

        _romBuffer[offset2] = 0xBD;
        _romBuffer[offset2 + 1] = _region == Region.Jap ? (byte)0x8C : _region == Region.Euro ? (byte)0x6D : (byte)0x9B;
        _romBuffer[offset2 + 2] = 0xFF;
        _romBuffer[offset2 + 3] = 0x85;
        _romBuffer[offset2 + 4] = 0x08;
    }

    private void SaveFile()
    {
        using (var fs = new FileStream(FilePath, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(_romHeader);
            bw.Write(_romBuffer);
        }
    }

    private void HandleChanges()
    {
        TrackGroups.PropertyChanged += MarkAsModified;
        Themes.PropertyChanged += MarkAsModified;
        Settings.PropertyChanged += MarkAsModified;
    }

    private void MarkAsModified()
    {
        MarkAsModified(this, new PropertyChangedEventArgs(PropertyNames.Game.Data));
    }

    private void MarkAsModified(object sender, PropertyChangedEventArgs e)
    {
        Modified = true;
        OnPropertyChanged(sender, e);
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(sender, e);
    }

    private void ResetModifiedState()
    {
        TrackGroups.ResetModifiedState();
        Themes.ResetModifiedState();
        Settings.ResetModifiedState();

        Modified = false;
        OnPropertyChanged(PropertyNames.Game.Modified);
    }

    #endregion Save data

    #region Compression

    public void InsertData(byte[] data, int offset)
    {
        offset -= _romHeader.Length;
        Buffer.BlockCopy(data, 0, _romBuffer, offset, data.Length);
        MarkAsModified();
    }

    public byte[] Decompress(int offset, bool twice)
    {
        offset -= _romHeader.Length;
        return Codec.Decompress(_romBuffer, offset, twice);
    }

    public int GetCompressedChunkLength(int offset)
    {
        offset -= _romHeader.Length;
        return Codec.GetCompressedLength(_romBuffer, offset);
    }

    #endregion Compression
}