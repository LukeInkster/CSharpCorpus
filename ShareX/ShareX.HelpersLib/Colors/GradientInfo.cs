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

using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace ShareX.HelpersLib
{
    public class GradientInfo
    {
        [DefaultValue(LinearGradientMode.Vertical)]
        public LinearGradientMode Type { get; set; }

        public List<GradientStop> Colors { get; set; }

        public bool IsValid
        {
            get
            {
                return Colors != null && Colors.Count >= 2 && Colors.Any(x => x.Location == 0f) && Colors.Any(x => x.Location == 100f);
            }
        }

        public GradientInfo()
        {
            Type = LinearGradientMode.Vertical;
            Colors = new List<GradientStop>();
        }

        public void Draw(Graphics g, Rectangle rect)
        {
            if (IsValid)
            {
                try
                {
                    using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, rect.Width, rect.Height), Color.White, Color.White, Type))
                    {
                        ColorBlend colorBlend = new ColorBlend();
                        IEnumerable<GradientStop> gradient = Colors.OrderBy(x => x.Location);
                        colorBlend.Colors = gradient.Select(x => x.Color).ToArray();
                        colorBlend.Positions = gradient.Select(x => x.Location / 100).ToArray();
                        brush.InterpolationColors = colorBlend;
                        g.FillRectangle(brush, rect);
                    }
                }
                catch { }
            }
        }

        public override string ToString()
        {
            return "Gradient";
        }
    }
}