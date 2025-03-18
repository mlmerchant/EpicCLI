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

using System.ComponentModel;
using System.Globalization;
using EpicLib.Utility;

namespace EpicLib.Settings;

/// <summary>
///     Item of a <see cref="TextCollection" />.
/// </summary>
public class TextItem : INotifyPropertyChanged
{
    private readonly ITextCollection _collection;

    private string _value;

    public TextItem(ITextCollection collection, string value)
    {
        _collection = collection;
        _value = value;
    }

    public TextConverter Converter => _collection.Converter;

    public string Value
    {
        get => _value;
        set
        {
            var oldValue = _value;
            var newValue = Converter.GetValidatedText(value);

            if (oldValue == newValue) return;

            _value = newValue;

            var diff = _collection.TotalCharacterCount - _collection.MaxCharacterCount;
            if (diff > 0)
            {
                newValue = _value.Substring(0, _value.Length - diff);

                if (oldValue == newValue)
                {
                    _value = oldValue;
                    return;
                }

                _value = newValue;
            }

            OnPropertyChanged(PropertyNames.TextItem.Value);
        }
    }

    public string FormattedValue =>
        _collection.Region == Region.Jap
            ? _value
            : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(_value.ToLowerInvariant());

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString()
    {
        return FormattedValue;
    }
}