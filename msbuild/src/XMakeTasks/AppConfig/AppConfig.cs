﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Collections;
using System.Globalization;

using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Read information from application .config files.
    /// </summary>
    internal sealed class AppConfig
    {
        /// <summary>
        /// Corresponds to the contents of the &lt;runtime&gt; element.
        /// </summary>
        private RuntimeSection _runtime = new RuntimeSection();

        /// <summary>
        /// Read the .config from a file.
        /// </summary>
        /// <param name="appConfigFile"></param>
        internal void Load(string appConfigFile)
        {
            XmlTextReader reader = null;
            try
            {
                reader = new XmlTextReader(appConfigFile);
                reader.DtdProcessing = DtdProcessing.Ignore;
                Read(reader);
            }
            catch (XmlException e)
            {
                throw new AppConfigException(e.Message, appConfigFile, (reader != null ? reader.LineNumber : 0), (reader != null ? reader.LinePosition : 0), e);
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                throw new AppConfigException(e.Message, appConfigFile, (reader != null ? reader.LineNumber : 0), (reader != null ? reader.LinePosition : 0), e);
            }
            finally
            {
                reader?.Close();
            }
        }

        /// <summary>
        /// Read the .config from an XmlReader
        /// </summary>
        /// <param name="reader"></param>
        internal void Read(XmlTextReader reader)
        {
            // Read the app.config XML
            while (reader.Read())
            {
                // Look for the <runtime> section
                if (reader.NodeType == XmlNodeType.Element && StringEquals(reader.Name, "runtime"))
                {
                    _runtime.Read(reader);
                }
            }
        }

        /// <summary>
        /// Access the Runtime section of the application .config file.
        /// </summary>
        /// <value></value>
        internal RuntimeSection Runtime
        {
            get { return _runtime; }
        }

        /// <summary>
        /// App.config files seem to come with mixed casing for element and attribute names.
        /// If the fusion loader can handle this then this code should too.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        static internal bool StringEquals(string a, string b)
        {
            return String.Compare(a, b, StringComparison.OrdinalIgnoreCase) == 0;
        }
    }
}
