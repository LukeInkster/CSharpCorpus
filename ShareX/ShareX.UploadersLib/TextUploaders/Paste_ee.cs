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

using ShareX.UploadersLib.Properties;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ShareX.UploadersLib.TextUploaders
{
    public class Paste_eeTextUploaderService : TextUploaderService
    {
        public override TextDestination EnumValue { get; } = TextDestination.Paste_ee;

        public override Image ServiceImage => Resources.document;

        public override bool CheckConfig(UploadersConfig config) => true;

        public override GenericUploader CreateUploader(UploadersConfig config, TaskReferenceHelper taskInfo)
        {
            return new Paste_ee(config.Paste_eeUserAPIKey);
        }

        public override TabPage GetUploadersConfigTabPage(UploadersConfigForm form) => form.tpPaste_ee;
    }

    public sealed class Paste_ee : TextUploader
    {
        public string APIKey { get; private set; }

        public Paste_ee()
        {
            APIKey = "public";
        }

        public Paste_ee(string apiKey)
        {
            APIKey = apiKey;
        }

        public override UploadResult UploadText(string text, string fileName)
        {
            UploadResult ur = new UploadResult();

            if (!string.IsNullOrEmpty(text))
            {
                if (string.IsNullOrEmpty(APIKey))
                {
                    APIKey = "public";
                }

                Dictionary<string, string> arguments = new Dictionary<string, string>();
                arguments.Add("key", APIKey);
                arguments.Add("description", "");
                arguments.Add("paste", text);
                arguments.Add("format", "simple");
                arguments.Add("return", "link");

                ur.Response = SendRequest(HttpMethod.POST, "http://paste.ee/api", arguments);

                if (!string.IsNullOrEmpty(ur.Response) && ur.Response.StartsWith("error"))
                {
                    Errors.Add(ur.Response);
                }
                else
                {
                    ur.URL = ur.Response;
                }
            }

            return ur;
        }
    }
}