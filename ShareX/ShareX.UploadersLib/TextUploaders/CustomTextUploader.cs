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
using ShareX.UploadersLib.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ShareX.UploadersLib.TextUploaders
{
    public class CustomTextUploaderService : TextUploaderService
    {
        public override TextDestination EnumValue { get; } = TextDestination.CustomTextUploader;

        public override bool CheckConfig(UploadersConfig config)
        {
            return config.CustomUploadersList != null && config.CustomUploadersList.IsValidIndex(config.CustomTextUploaderSelected);
        }

        public override GenericUploader CreateUploader(UploadersConfig config, TaskReferenceHelper taskInfo)
        {
            int index;

            if (taskInfo.OverrideCustomUploader)
            {
                index = taskInfo.CustomUploaderIndex.BetweenOrDefault(0, config.CustomUploadersList.Count - 1);
            }
            else
            {
                index = config.CustomTextUploaderSelected;
            }

            CustomUploaderItem customUploader = config.CustomUploadersList.ReturnIfValidIndex(index);

            if (customUploader != null)
            {
                return new CustomTextUploader(customUploader);
            }

            return null;
        }

        public override TabPage GetUploadersConfigTabPage(UploadersConfigForm form) => form.tpCustomUploaders;
    }

    public sealed class CustomTextUploader : TextUploader
    {
        private CustomUploaderItem customUploader;

        public CustomTextUploader(CustomUploaderItem customUploaderItem)
        {
            customUploader = customUploaderItem;
        }

        public override UploadResult UploadText(string text, string fileName)
        {
            UploadResult result = new UploadResult();

            string requestURL = customUploader.GetRequestURL();

            if ((customUploader.RequestType != CustomUploaderRequestType.POST || string.IsNullOrEmpty(customUploader.FileFormName)) &&
                (customUploader.Arguments == null || !customUploader.Arguments.Any(x => x.Value.Contains("$input$") || x.Value.Contains("%input"))))
                throw new Exception("Atleast one '$input$' required for argument value.");

            Dictionary<string, string> args = customUploader.GetArguments(text);

            if (customUploader.RequestType == CustomUploaderRequestType.POST)
            {
                if (string.IsNullOrEmpty(customUploader.FileFormName))
                {
                    result.Response = SendRequest(HttpMethod.POST, requestURL, args, customUploader.GetHeaders(), responseType: customUploader.ResponseType);
                }
                else
                {
                    byte[] byteArray = Encoding.UTF8.GetBytes(text);
                    using (MemoryStream stream = new MemoryStream(byteArray))
                    {
                        result = UploadData(stream, requestURL, fileName, customUploader.GetFileFormName(), args, customUploader.GetHeaders(), responseType: customUploader.ResponseType);
                    }
                }
            }
            else
            {
                result.Response = SendRequest(customUploader.GetHttpMethod(), requestURL, args, customUploader.GetHeaders(), responseType: customUploader.ResponseType);
            }

            try
            {
                customUploader.ParseResponse(result);
            }
            catch (Exception e)
            {
                Errors.Add(Resources.CustomFileUploader_Upload_Response_parse_failed_ + Environment.NewLine + e);
            }

            return result;
        }
    }
}