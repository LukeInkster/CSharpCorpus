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

namespace ShareX.HelpersLib
{
    [Serializable]
    public class PrintSettings
    {
        public int Margin { get; set; }
        public bool AutoRotateImage { get; set; }
        public bool AutoScaleImage { get; set; }
        public bool AllowEnlargeImage { get; set; }
        public bool CenterImage { get; set; }
        public XmlFont TextFont { get; set; }
        public bool ShowPrintDialog { get; set; }

        public PrintSettings()
        {
            Margin = 5;
            AutoRotateImage = true;
            AutoScaleImage = true;
            AllowEnlargeImage = false;
            CenterImage = false;
            TextFont = new XmlFont("Arial", 10);
            ShowPrintDialog = true;
        }
    }
}