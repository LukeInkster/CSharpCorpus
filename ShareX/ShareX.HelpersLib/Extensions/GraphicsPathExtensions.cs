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

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ShareX.HelpersLib
{
    public static class GraphicsPathExtensions
    {
        public static void AddRectangleProper(this GraphicsPath graphicsPath, RectangleF rect, float penWidth = 1)
        {
            if (penWidth == 1)
            {
                rect = new RectangleF(rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            }

            if (rect.Width > 0 && rect.Height > 0)
            {
                graphicsPath.AddRectangle(rect);
            }
        }

        public static void AddRoundedRectangleProper(this GraphicsPath graphicsPath, RectangleF rect, float radius, float penWidth = 1)
        {
            if (penWidth == 1)
            {
                rect = new RectangleF(rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            }

            if (rect.Width > 0 && rect.Height > 0)
            {
                graphicsPath.AddRoundedRectangle(rect, radius);
            }
        }

        public static void AddRoundedRectangle(this GraphicsPath graphicsPath, RectangleF rect, float radius)
        {
            if (radius <= 0f)
            {
                graphicsPath.AddRectangle(rect);
            }
            else
            {
                // If the corner radius is greater than or equal to
                // half the width, or height (whichever is shorter)
                // then return a capsule instead of a lozenge
                if (radius >= (Math.Min(rect.Width, rect.Height) / 2.0f))
                {
                    graphicsPath.AddCapsule(rect);
                }
                else
                {
                    // Create the arc for the rectangle sides and declare
                    // a graphics path object for the drawing
                    float diameter = radius * 2.0f;
                    SizeF size = new SizeF(diameter, diameter);
                    RectangleF arc = new RectangleF(rect.Location, size);

                    // Top left arc
                    graphicsPath.AddArc(arc, 180, 90);

                    // Top right arc
                    arc.X = rect.Right - diameter;
                    graphicsPath.AddArc(arc, 270, 90);

                    // Bottom right arc
                    arc.Y = rect.Bottom - diameter;
                    graphicsPath.AddArc(arc, 0, 90);

                    // Bottom left arc
                    arc.X = rect.Left;
                    graphicsPath.AddArc(arc, 90, 90);

                    graphicsPath.CloseFigure();
                }
            }
        }

        public static void AddCapsule(this GraphicsPath graphicsPath, RectangleF rect)
        {
            float diameter;
            RectangleF arc;

            try
            {
                if (rect.Width > rect.Height)
                {
                    // Horizontal capsule
                    diameter = rect.Height;
                    SizeF sizeF = new SizeF(diameter, diameter);
                    arc = new RectangleF(rect.Location, sizeF);
                    graphicsPath.AddArc(arc, 90, 180);
                    arc.X = rect.Right - diameter;
                    graphicsPath.AddArc(arc, 270, 180);
                }
                else if (rect.Width < rect.Height)
                {
                    // Vertical capsule
                    diameter = rect.Width;
                    SizeF sizeF = new SizeF(diameter, diameter);
                    arc = new RectangleF(rect.Location, sizeF);
                    graphicsPath.AddArc(arc, 180, 180);
                    arc.Y = rect.Bottom - diameter;
                    graphicsPath.AddArc(arc, 0, 180);
                }
                else
                {
                    // Circle
                    graphicsPath.AddEllipse(rect);
                }
            }
            catch
            {
                graphicsPath.AddEllipse(rect);
            }

            graphicsPath.CloseFigure();
        }

        public static void AddTriangle(this GraphicsPath graphicsPath, RectangleF rect, TriangleAngle angle = TriangleAngle.Top)
        {
            PointF p1, p2, p3;

            switch (angle)
            {
                default:
                case TriangleAngle.Top:
                    p1 = new PointF(rect.X + rect.Width / 2.0f, rect.Y);
                    p2 = new PointF(rect.X, rect.Y + rect.Height);
                    p3 = new PointF(rect.X + rect.Width, rect.Y + rect.Height);
                    break;
                case TriangleAngle.Right:
                    p1 = new PointF(rect.X + rect.Width, rect.Y + rect.Height / 2.0f);
                    p2 = new PointF(rect.X, rect.Y);
                    p3 = new PointF(rect.X, rect.Y + rect.Height);
                    break;
                case TriangleAngle.Bottom:
                    p1 = new PointF(rect.X + rect.Width / 2.0f, rect.Y + rect.Height);
                    p2 = new PointF(rect.X + rect.Width, rect.Y);
                    p3 = new PointF(rect.X, rect.Y);
                    break;
                case TriangleAngle.Left:
                    p1 = new PointF(rect.X, rect.Y + rect.Height / 2.0f);
                    p2 = new PointF(rect.X + rect.Width, rect.Y + rect.Height);
                    p3 = new PointF(rect.X + rect.Width, rect.Y);
                    break;
            }

            graphicsPath.AddPolygon(new PointF[] { p1, p2, p3 });
        }

        public static void AddDiamond(this GraphicsPath graphicsPath, RectangleF rect)
        {
            PointF p1 = new PointF(rect.X + rect.Width / 2.0f, rect.Y);
            PointF p2 = new PointF(rect.X + rect.Width, rect.Y + rect.Height / 2.0f);
            PointF p3 = new PointF(rect.X + rect.Width / 2.0f, rect.Y + rect.Height);
            PointF p4 = new PointF(rect.X, rect.Y + rect.Height / 2.0f);

            graphicsPath.AddPolygon(new PointF[] { p1, p2, p3, p4 });
        }

        public static void AddPolygon(this GraphicsPath graphicsPath, RectangleF rect, int sideCount)
        {
            PointF[] points = new PointF[sideCount];

            float a = 0;

            for (int i = 0; i < sideCount; i++)
            {
                points[i] = new PointF(rect.X + ((rect.Width / 2.0f) * (float)Math.Cos(a)) + rect.Width / 2.0f,
                    rect.Y + ((rect.Height / 2.0f) * (float)Math.Sin(a)) + rect.Height / 2.0f);

                a += (float)Math.PI * 2.0f / sideCount;
            }

            graphicsPath.AddPolygon(points);
        }

        public static void WindingModeOutline(this GraphicsPath graphicsPath)
        {
            IntPtr handle = (IntPtr)graphicsPath.GetType().GetField("nativePath", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(graphicsPath);
            HandleRef path = new HandleRef(graphicsPath, handle);
            NativeMethods.GdipWindingModeOutline(path, IntPtr.Zero, 0.25F);
        }
    }
}