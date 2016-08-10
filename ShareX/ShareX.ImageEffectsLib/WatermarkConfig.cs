﻿#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2016 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.Drawing;

namespace ShareX.ImageEffectsLib
{
    public class WatermarkConfig
    {
        public WatermarkType Type = WatermarkType.Text;
        public ContentAlignment Placement = ContentAlignment.BottomRight;
        public int Offset = 5;
        public DrawText Text = new DrawText { DrawTextShadow = false };
        public DrawImage Image = new DrawImage();

        public Image Apply(Image img)
        {
            Text.Placement = Image.Placement = Placement;
            Text.Offset = Image.Offset = new Point(Offset, Offset);

            switch (Type)
            {
                default:
                case WatermarkType.Text:
                    return Text.Apply(img);
                case WatermarkType.Image:
                    return Image.Apply(img);
            }
        }
    }
}