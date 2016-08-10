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
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShareX.ScreenCaptureLib
{
    public class FreehandRegionShape : BaseRegionShape
    {
        public override ShapeType ShapeType { get; } = ShapeType.RegionFreehand;

        public Point LastPosition
        {
            get
            {
                if (points.Count > 0)
                {
                    return points[points.Count - 1];
                }

                return Point.Empty;
            }
            set
            {
                if (points.Count > 0)
                {
                    points[points.Count - 1] = value;
                }
            }
        }

        private List<Point> points = new List<Point>();
        private bool isPolygonMode;

        public override void OnUpdate()
        {
            if (Manager.IsCreating)
            {
                if (Manager.IsCornerMoving)
                {
                    Move(InputManager.MouseVelocity);
                }
                else
                {
                    Point pos = InputManager.MousePosition0Based;

                    if (points.Count == 0 || (!Manager.IsProportionalResizing && LastPosition != pos))
                    {
                        points.Add(pos);
                    }

                    if (Manager.IsProportionalResizing)
                    {
                        if (!isPolygonMode)
                        {
                            points.Add(pos);
                        }

                        LastPosition = pos;
                    }

                    isPolygonMode = Manager.IsProportionalResizing;

                    Rectangle = points.CreateRectangle();
                }
            }
            else if (Manager.IsMoving)
            {
                Move(InputManager.MouseVelocity);
            }
        }

        public override void OnShapePathRequested(GraphicsPath gp, Rectangle rect)
        {
            if (points.Count > 1)
            {
                gp.StartFigure();

                for (int i = 0; i < points.Count - 1; i++)
                {
                    gp.AddLine(points[i], points[i + 1]);
                }

                gp.CloseFigure();
            }
        }

        public override void Move(int x, int y)
        {
            for (int i = 0; i < points.Count; i++)
            {
                points[i] = points[i].Add(x, y);
            }

            Rectangle = Rectangle.LocationOffset(x, y);
        }

        public override void Resize(int x, int y, bool fromBottomRight)
        {
            Move(x, y);
        }

        public override void OnNodeVisible()
        {
            Manager.Nodes[(int)NodePosition.TopLeft].Shape = NodeShape.Circle;
            Manager.Nodes[(int)NodePosition.TopLeft].Visible = true;
        }

        public override void OnNodeUpdate()
        {
            if (Manager.Nodes[(int)NodePosition.TopLeft].IsDragging)
            {
                Manager.IsCreating = true;
                Manager.NodesVisible = false;
            }
        }

        public override void OnNodePositionUpdate()
        {
            Manager.Nodes[(int)NodePosition.TopLeft].Position = LastPosition;
        }
    }
}