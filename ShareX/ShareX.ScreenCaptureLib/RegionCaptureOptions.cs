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
using System.Drawing;

namespace ShareX.ScreenCaptureLib
{
    public class RegionCaptureOptions
    {
        public const int MagnifierPixelCountMinimum = 3;
        public const int MagnifierPixelCountMaximum = 35;
        public const int MagnifierPixelSizeMinimum = 3;
        public const int MagnifierPixelSizeMaximum = 30;
        public const int SnapDistance = 30;
        public const int MoveSpeedMinimum = 1;
        public const int MoveSpeedMaximum = 10;

        public bool QuickCrop = true;
        public RegionCaptureAction MouseRightClickAction = RegionCaptureAction.OpenOptionsMenu;
        public RegionCaptureAction MouseMiddleClickAction = RegionCaptureAction.CancelCapture;
        public RegionCaptureAction Mouse4ClickAction = RegionCaptureAction.SwapToolType;
        public RegionCaptureAction Mouse5ClickAction = RegionCaptureAction.CaptureFullscreen;
        public bool DetectWindows = true;
        public bool DetectControls = true;
        public bool UseDimming = true;
        public bool UseCustomInfoText = false;
        public string CustomInfoText = "X: $x, Y: $y$nR: $r, G: $g, B: $b$nHex: $hex"; // Formats: $x, $y, $r, $g, $b, $hex, $n
        public List<SnapSize> SnapSizes = new List<SnapSize>()
        {
            new SnapSize(426, 240), // 240p
            new SnapSize(640, 360), // 360p
            new SnapSize(854, 480), // 480p
            new SnapSize(1280, 720), // 720p
            new SnapSize(1920, 1080) // 1080p
        };
        public bool ShowTips = true;
        public bool ShowInfo = true;
        public bool ShowMagnifier = true;
        public bool UseSquareMagnifier = false;
        public int MagnifierPixelCount = 15; // Must be odd number like 11, 13, 15 etc.
        public int MagnifierPixelSize = 10;
        public bool ShowCrosshair = false;
        public bool IsFixedSize = false;
        public Size FixedSize = new Size(250, 250);
        public bool ShowFPS = false;

        public AnnotationOptions AnnotationOptions = new AnnotationOptions();
        public ShapeType LastRegionTool = ShapeType.RegionRectangle;
        public ShapeType LastAnnotationTool = ShapeType.DrawingRectangle;
        public bool ShowMenuTip = true;
    }
}