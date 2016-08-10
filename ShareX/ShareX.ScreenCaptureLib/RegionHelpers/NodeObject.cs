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

using ShareX.HelpersLib;
using System.Drawing;

namespace ShareX.ScreenCaptureLib
{
    internal class NodeObject : DrawableObject
    {
        private Point position;

        public Point Position
        {
            get
            {
                return position;
            }
            set
            {
                position = value;

                Rectangle = new Rectangle(position.X - (NodeSize - 1) / 2, position.Y - (NodeSize - 1) / 2, NodeSize, NodeSize);
            }
        }

        public int NodeSize { get; set; }

        public NodeShape Shape { get; set; }

        public NodeObject(int x = 0, int y = 0)
        {
            NodeSize = 13;
            Shape = NodeShape.Square;
            Position = new Point(x, y);
        }

        public override void Draw(Graphics g)
        {
            Rectangle rect = new Rectangle(Rectangle.X, Rectangle.Y, Rectangle.Width - 1, Rectangle.Height - 1);

            switch (Shape)
            {
                case NodeShape.Square:
                    g.DrawRectangle(Pens.White, rect.Offset(-1));
                    g.DrawRectangle(Pens.Black, rect);
                    break;
                case NodeShape.Circle:
                    g.DrawEllipse(Pens.White, rect.Offset(-1));
                    g.DrawEllipse(Pens.Black, rect);
                    break;
                case NodeShape.Diamond:
                    g.DrawDiamond(Pens.White, rect.Offset(-1));
                    g.DrawDiamond(Pens.Black, rect);
                    break;
            }
        }
    }
}