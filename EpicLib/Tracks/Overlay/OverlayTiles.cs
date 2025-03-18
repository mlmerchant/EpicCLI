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

using System.Collections;
using System.Drawing;
using EpicLib.Utility;

namespace EpicLib.Tracks.Overlay;

/// <summary>
///     An <see cref="OverlayTile" /> collection.
/// </summary>
public class OverlayTiles : IEnumerable<OverlayTile>
{
    public const int Size = 128;
    public const int MaxTileCount = 41;
    private readonly List<OverlayTile> _overlayTiles;
    private readonly OverlayTilePatterns _patterns;

    private readonly OverlayTileSizes _sizes;

    public OverlayTiles(byte[] data, OverlayTileSizes sizes, OverlayTilePatterns patterns)
    {
        _sizes = sizes;
        _patterns = patterns;
        _overlayTiles = new List<OverlayTile>();
        SetBytes(data);
    }

    public int Count => _overlayTiles.Count;

    public OverlayTile this[int index] => _overlayTiles[index];

    public IEnumerator<OverlayTile> GetEnumerator()
    {
        return _overlayTiles.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _overlayTiles.GetEnumerator();
    }

    public event EventHandler<EventArgs> DataChanged;
    public event EventHandler<EventArgs<OverlayTile>> ElementAdded;
    public event EventHandler<EventArgs<OverlayTile>> ElementRemoved;
    public event EventHandler<EventArgs> ElementsCleared;

    public void SetBytes(byte[] data)
    {
        if (data.Length != Size) throw new ArgumentException("Incorrect overlay tile data size", nameof(data));

        Clear();
        for (var overlayTileIndex = 0; overlayTileIndex < MaxTileCount; overlayTileIndex++)
        {
            var index = overlayTileIndex * 3;
            if (data[index + 1] == 0xFF &&
                data[index + 2] == 0xFF)
                break;

            var size = _sizes[(data[index] & 0xC0) >> 6];
            var pattern = _patterns[data[index] & 0x3F];

            if (pattern.Size != size)
                // The overlay tile size is different from the expected pattern size,
                // ignore this overlay tile, the editor cannot handle it.
                continue;

            var x = data[index + 1] & 0x7F;
            var y = ((data[index + 2] & 0x3F) << 1) + ((data[index + 1] & 0x80) >> 7);
            var location = new Point(x, y);

            Add(new OverlayTile(pattern, location));
        }
    }

    /// <summary>
    ///     Returns the OverlayTiles data as a byte array, in the format the SMK ROM expects.
    /// </summary>
    /// <returns>The OverlayTiles bytes.</returns>
    public byte[] GetBytes()
    {
        var data = new byte[Size];

        for (var overlayTileIndex = 0; overlayTileIndex < _overlayTiles.Count; overlayTileIndex++)
        {
            var index = overlayTileIndex * 3;
            var overlayTile = _overlayTiles[overlayTileIndex];
            overlayTile.GetBytes(data, index, _sizes, _patterns);
        }

        for (var index = _overlayTiles.Count * 3; index < data.Length; index++) data[index] = 0xFF;

        return data;
    }

    public void Add(OverlayTile overlayTile)
    {
        if (Count >= MaxTileCount) return;

        _overlayTiles.Add(overlayTile);
        overlayTile.DataChanged += overlayTile_DataChanged;
        OnElementAdded(overlayTile);
    }

    public void Remove(OverlayTile overlayTile)
    {
        overlayTile.DataChanged -= overlayTile_DataChanged;
        _overlayTiles.Remove(overlayTile);
        OnElementRemoved(overlayTile);
    }

    private void overlayTile_DataChanged(object sender, EventArgs e)
    {
        OnDataChanged();
    }

    /// <summary>
    ///     Removes all the overlay tiles from the collection.
    /// </summary>
    public void Clear()
    {
        foreach (var tile in _overlayTiles) tile.DataChanged -= overlayTile_DataChanged;

        _overlayTiles.Clear();
        OnElementsCleared();
    }

    private void OnDataChanged()
    {
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnElementAdded(OverlayTile value)
    {
        ElementAdded?.Invoke(this, new EventArgs<OverlayTile>(value));
    }

    private void OnElementRemoved(OverlayTile value)
    {
        ElementRemoved?.Invoke(this, new EventArgs<OverlayTile>(value));
    }

    private void OnElementsCleared()
    {
        ElementsCleared?.Invoke(this, EventArgs.Empty);
    }
}