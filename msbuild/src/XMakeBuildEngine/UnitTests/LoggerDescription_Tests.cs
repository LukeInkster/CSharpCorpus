﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using Microsoft.Build.Logging;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class LoggerDescription_Tests
    {
        [Fact]
        public void LoggerDescriptionCustomSerialization()
        {
            string className = "Class";
            string loggerAssemblyName = "Class";
            string loggerFileAssembly = null;
            string loggerSwitchParameters = "Class";
            LoggerVerbosity verbosity = LoggerVerbosity.Detailed;

            LoggerDescription description = new LoggerDescription(className, loggerAssemblyName, loggerFileAssembly, loggerSwitchParameters, verbosity);
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            BinaryReader reader = new BinaryReader(stream);
            try
            {
                stream.Position = 0;
                description.WriteToStream(writer);
                long streamWriteEndPosition = stream.Position;
                stream.Position = 0;
                LoggerDescription description2 = new LoggerDescription();
                description2.CreateFromStream(reader);
                long streamReadEndPosition = stream.Position;
                Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream end positions should be equal"

                Assert.Equal(description.Verbosity, description2.Verbosity); // "Expected Verbosity to Match"
                Assert.Equal(description.LoggerId, description2.LoggerId); // "Expected Verbosity to Match"
                Assert.Equal(0, string.Compare(description.LoggerSwitchParameters, description2.LoggerSwitchParameters, StringComparison.OrdinalIgnoreCase)); // "Expected LoggerSwitchParameters to Match"
                Assert.Equal(0, string.Compare(description.Name, description2.Name, StringComparison.OrdinalIgnoreCase)); // "Expected Name to Match"
            }
            finally
            {
                reader.Close();
                writer = null;
                stream = null;
            }
        }
    }
}
