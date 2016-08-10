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

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ShareX.HelpersLib.Properties;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ShareX.HelpersLib
{
    public partial class ExportImportControl : UserControl
    {
        public delegate object ExportEventHandler();
        public event ExportEventHandler ExportRequested;

        public delegate void ImportEventHandler(object obj);
        public event ImportEventHandler ImportRequested;

        public delegate void UploadEventHandler(string json);
        public static event UploadEventHandler UploadRequested;

        // Can't use generic class because not works in form designer
        public Type ObjectType { get; set; }

        [DefaultValue(false)]
        public bool ExportIgnoreNull { get; set; }

        public ExportImportControl()
        {
            InitializeComponent();
        }

        public string Serialize(object obj)
        {
            if (obj != null)
            {
                try
                {
                    StringBuilder sb = new StringBuilder(256);
                    StringWriter stringWriter = new StringWriter(sb, CultureInfo.InvariantCulture);

                    using (JsonTextWriter textWriter = new JsonTextWriter(stringWriter))
                    {
                        textWriter.Formatting = Formatting.Indented;

                        JsonSerializer serializer = new JsonSerializer();
                        serializer.ContractResolver = new WritablePropertiesOnlyResolver();
                        serializer.Converters.Add(new StringEnumConverter());
                        serializer.NullValueHandling = ExportIgnoreNull ? NullValueHandling.Ignore : NullValueHandling.Include;
                        serializer.TypeNameHandling = TypeNameHandling.Auto;
                        serializer.Serialize(textWriter, obj, ObjectType);
                    }

                    return stringWriter.ToString();
                }
                catch (Exception e)
                {
                    DebugHelper.WriteException(e);
                    MessageBox.Show(Resources.ExportImportControl_Serialize_Export_failed_ + "\n\n" + e, "ShareX - " + Resources.Error,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            return null;
        }

        private void tsmiExportClipboard_Click(object sender, EventArgs e)
        {
            if (ExportRequested != null)
            {
                object obj = ExportRequested();

                string json = Serialize(obj);

                if (!string.IsNullOrEmpty(json) && ClipboardHelpers.CopyText(json))
                {
                    MessageBox.Show(Resources.ExportImportControl_tsmiExportClipboard_Click_Settings_copied_to_your_clipboard_, "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void tsmiExportFile_Click(object sender, EventArgs e)
        {
            if (ExportRequested != null)
            {
                object obj = ExportRequested();

                string json = Serialize(obj);

                if (!string.IsNullOrEmpty(json))
                {
                    using (SaveFileDialog sfd = new SaveFileDialog() { Filter = "Settings (*.json)|*.json" })
                    {
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            File.WriteAllText(sfd.FileName, json, Encoding.UTF8);
                        }
                    }
                }
            }
        }

        private void tsmiExportUpload_Click(object sender, EventArgs e)
        {
            if (ExportRequested != null && UploadRequested != null)
            {
                object obj = ExportRequested();

                string json = Serialize(obj);

                if (!string.IsNullOrEmpty(json))
                {
                    UploadRequested(json);
                }
            }
        }

        public object Deserialize(string json)
        {
            try
            {
                using (JsonTextReader textReader = new JsonTextReader(new StringReader(json)))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Converters.Add(new StringEnumConverter());
                    serializer.Error += (sender, e) => e.ErrorContext.Handled = true;
                    serializer.ObjectCreationHandling = ObjectCreationHandling.Replace;
                    serializer.TypeNameHandling = TypeNameHandling.Auto;
                    return serializer.Deserialize(textReader, ObjectType);
                }
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e);
                MessageBox.Show(Resources.ExportImportControl_Deserialize_Import_failed_ + "\n\n" + e, "ShareX - " + Resources.Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return null;
        }

        private void Import(string json)
        {
            if (!string.IsNullOrEmpty(json))
            {
                object obj = Deserialize(json);

                if (obj != null)
                {
                    ImportRequested(obj);
                }
            }
        }

        private void tsmiImportClipboard_Click(object sender, EventArgs e)
        {
            if (ImportRequested != null)
            {
                if (Clipboard.ContainsText())
                {
                    string json = Clipboard.GetText();
                    Import(json);
                }
            }
        }

        private void tsmiImportFile_Click(object sender, EventArgs e)
        {
            if (ImportRequested != null)
            {
                using (OpenFileDialog ofd = new OpenFileDialog() { Filter = "Settings (*.json)|*.json|All files (*.*)|*.*", Multiselect = true })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        foreach (string filename in ofd.FileNames)
                        {
                            string json = File.ReadAllText(filename, Encoding.UTF8);
                            Import(json);
                        }
                    }
                }
            }
        }

        private void tsmiImportURL_Click(object sender, EventArgs e)
        {
            if (ImportRequested != null)
            {
                string url = InputBox.GetInputText(Resources.ExportImportControl_tsmiImportURL_Click_URL_to_download_settings_from);

                if (!string.IsNullOrEmpty(url))
                {
                    btnImport.Enabled = false;

                    string json = null;

                    TaskEx.Run(() =>
                    {
                        json = Helpers.DownloadString(url);
                    },
                    () =>
                    {
                        Import(json);

                        btnImport.Enabled = true;
                    });
                }
            }
        }
    }
}