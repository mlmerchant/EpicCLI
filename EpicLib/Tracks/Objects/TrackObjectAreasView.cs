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

using EpicLib.Tracks.AI;
using EpicLib.Tracks.Road;
using EpicLib.Utility;

namespace EpicLib.Tracks.Objects;

public class TrackObjectAreasView
{
    /// <summary>
    ///     The object area grid size (horizontally and vertically).
    /// </summary>
    public const int GridSize = TrackMap.Size / TrackAIElement.Precision;

    /// <summary>
    ///     The maximum value (horizontally and vertically) within the object area grid.
    /// </summary>
    private const int GridLimit = GridSize - 1;

    private readonly byte[] _areas;

    public TrackObjectAreasView(byte[] data, TrackAI ai)
    {
        AI = ai;
        _areas = new byte[4];
        SetBytes(data);
    }

    public TrackAI AI { get; }

    public event EventHandler<EventArgs<int>> DataChanged;

    public void SetBytes(byte[] data)
    {
        for (var i = 0; i < _areas.Length; i++) SetAreaValue(i, data[i]);
    }

    private byte GetAreaIndex(int aiElementIndex)
    {
        for (byte i = 0; i < _areas.Length; i++)
            if (aiElementIndex < _areas[i])
                return i;

        return 0;
    }

    public byte GetAreaValue(int areaIndex)
    {
        return _areas[areaIndex];
    }

    public void SetAreaValue(int areaIndex, byte value)
    {
        if (_areas[areaIndex] == value) return;

        _areas[areaIndex] = value;
        OnDataChanged(areaIndex);
    }

    private void OnDataChanged(int areaIndex)
    {
        DataChanged?.Invoke(this, new EventArgs<int>(areaIndex));
    }

    public byte[][] GetGrid()
    {
        byte[][] areas;

        if (AI.ElementCount == 0)
        {
            areas = new byte[GridSize][];

            for (var y = 0; y < areas.Length; y++) areas[y] = new byte[GridSize];

            return areas;
        }

        var sAreas = InitAreas();
        FillGridFromAI(sAreas);
        areas = GetGridFilledFromNearestTiles(sAreas);

        return areas;
    }

    private static sbyte[][] InitAreas()
    {
        var areas = new sbyte[GridSize][];

        for (var y = 0; y < areas.Length; y++) areas[y] = new sbyte[GridSize];

        for (var x = 0; x < areas[0].Length; x++) areas[0][x] = -1;

        for (var y = 1; y < areas.Length; y++) Buffer.BlockCopy(areas[0], 0, areas[y], 0, areas[y].Length);

        return areas;
    }

    private void FillGridFromAI(sbyte[][] areas)
    {
        foreach (var aiElem in AI)
        {
            var aiElemIndex = AI.GetElementIndex(aiElem);
            var areaIndex = (sbyte)GetAreaIndex(aiElemIndex);
            var left = Math.Min(aiElem.Area.X / TrackAIElement.Precision, GridSize);
            var top = Math.Min(aiElem.Area.Y / TrackAIElement.Precision, GridSize);
            var right = Math.Min(aiElem.Area.Right / TrackAIElement.Precision, GridSize);
            var bottom = Math.Min(aiElem.Area.Bottom / TrackAIElement.Precision, GridSize);

            switch (aiElem.AreaShape)
            {
                case TrackAIElementShape.Rectangle:
                    for (var y = top; y < bottom; y++)
                    for (var x = left; x < right; x++)
                        areas[y][x] = areaIndex;

                    break;

                case TrackAIElementShape.TriangleTopLeft:
                    for (var y = top; y < bottom; y++)
                    {
                        for (var x = left; x < right; x++) areas[y][x] = areaIndex;
                        right--;
                    }

                    break;

                case TrackAIElementShape.TriangleTopRight:
                    for (var y = top; y < bottom; y++)
                    {
                        for (var x = left; x < right; x++) areas[y][x] = areaIndex;
                        left++;
                    }

                    break;

                case TrackAIElementShape.TriangleBottomRight:
                    left = right - 1;
                    for (var y = top; y < bottom; y++)
                    {
                        for (var x = left; x < right; x++) areas[y][x] = areaIndex;
                        left--;
                    }

                    break;

                case TrackAIElementShape.TriangleBottomLeft:
                    right = left + 1;
                    for (var y = top; y < bottom; y++)
                    {
                        for (var x = left; x < right; x++) areas[y][x] = areaIndex;
                        right++;
                    }

                    break;
            }
        }
    }

    private static byte[][] GetGridFilledFromNearestTiles(sbyte[][] areas)
    {
        var newAreas = new byte[areas.Length][];

        for (var y = 0; y < areas.Length; y++)
        {
            newAreas[y] = new byte[areas[y].Length];
            for (var x = 0; x < areas[y].Length; x++)
            {
                if (areas[y][x] != -1)
                {
                    newAreas[y][x] = (byte)areas[y][x];
                    continue;
                }

                var depth = 1;
                sbyte areaIndex = -1;
                while (areaIndex == -1)
                {
                    sbyte matchFound;

                    matchFound = GetTopRightNearestTile(areas, x, y, depth);
                    if (matchFound > areaIndex) areaIndex = matchFound;

                    matchFound = GetBottomRightNearestTile(areas, x, y, depth);
                    if (matchFound > areaIndex) areaIndex = matchFound;

                    matchFound = GetBottomLeftNearestTile(areas, x, y, depth);
                    if (matchFound > areaIndex) areaIndex = matchFound;

                    matchFound = GetTopLeftNearestTile(areas, x, y, depth);
                    if (matchFound > areaIndex) areaIndex = matchFound;

                    depth++;
                }

                newAreas[y][x] = (byte)areaIndex;
            }
        }

        return newAreas;
    }

    private static sbyte GetTopRightNearestTile(sbyte[][] areas, int x, int y, int depth)
    {
        sbyte matchFound = -1;

        var x2 = x;
        var y2 = y - depth;

        if (y2 < 0)
        {
            x2 -= y2;
            y2 = 0;
        }

        while (x2 <= GridLimit && y2 <= y)
        {
            if (areas[y2][x2] > matchFound) matchFound = areas[y2][x2];

            x2++;
            y2++;
        }

        return matchFound;
    }

    private static sbyte GetBottomRightNearestTile(sbyte[][] areas, int x, int y, int depth)
    {
        sbyte matchFound = -1;

        var x2 = x + depth;
        var y2 = y;

        if (x2 > GridLimit)
        {
            y2 += x2 - GridLimit;
            x2 = GridLimit;
        }

        while (x2 >= x && y2 <= GridLimit)
        {
            if (areas[y2][x2] > matchFound) matchFound = areas[y2][x2];

            x2--;
            y2++;
        }

        return matchFound;
    }

    private static sbyte GetBottomLeftNearestTile(sbyte[][] areas, int x, int y, int depth)
    {
        sbyte matchFound = -1;

        var x2 = x;
        var y2 = y + depth;

        if (y2 > GridLimit)
        {
            x2 -= y2 - GridLimit;
            y2 = GridLimit;
        }

        while (x2 >= 0 && y2 >= y)
        {
            if (areas[y2][x2] > matchFound) matchFound = areas[y2][x2];

            x2--;
            y2--;
        }

        return matchFound;
    }

    private static sbyte GetTopLeftNearestTile(sbyte[][] areas, int x, int y, int depth)
    {
        sbyte matchFound = -1;

        var x2 = x - depth;
        var y2 = y;

        if (x2 < 0)
        {
            y2 += x2;
            x2 = 0;
        }

        while (x2 <= x && y2 >= 0)
        {
            if (areas[y2][x2] > matchFound) matchFound = areas[y2][x2];

            x2++;
            y2--;
        }

        return matchFound;
    }

    /// <summary>
    ///     Returns the <see cref="TrackObjectAreasView" /> data as a byte array, in the format the SMK ROM expects.
    /// </summary>
    /// <returns>The <see cref="TrackObjectAreasView" /> bytes.</returns>
    public byte[] GetBytes()
    {
        byte[] data =
        {
            _areas[0],
            _areas[1],
            _areas[2],
            _areas[3],
            0xFF
        };

        return data;
    }
}