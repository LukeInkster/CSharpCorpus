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
using System;
using System.Diagnostics;
using System.Drawing;

namespace ShareX.ScreenCaptureLib
{
    public class WindowInfo
    {
        public IntPtr Handle { get; }

        public string Text => NativeMethods.GetWindowText(Handle);

        public string ClassName => NativeMethods.GetClassName(Handle);

        public Process Process => NativeMethods.GetProcessByWindowHandle(Handle);

        public string ProcessName => Process?.ProcessName;

        public Rectangle Rectangle => CaptureHelpers.GetWindowRectangle(Handle);

        public Rectangle ClientRectangle => NativeMethods.GetClientRect(Handle);

        public WindowStyles Styles => (WindowStyles)NativeMethods.GetWindowLong(Handle, NativeMethods.GWL_STYLE);

        public Icon Icon => NativeMethods.GetApplicationIcon(Handle);

        public bool IsMaximized => NativeMethods.IsZoomed(Handle);

        public bool IsMinimized => NativeMethods.IsIconic(Handle);

        public bool IsVisible => NativeMethods.IsWindowVisible(Handle);

        public bool IsActive => NativeMethods.GetForegroundWindow() == Handle;

        public WindowInfo(IntPtr handle)
        {
            Handle = handle;
        }

        public void Activate()
        {
            NativeMethods.ActivateWindow(Handle);
        }

        public override string ToString()
        {
            return Text;
        }
    }
}