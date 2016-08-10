﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class EventArgsFormattingTests
    {
        [Fact]
        public void NoLineInfoFormatEventMessage()
        {
            // Testing the method in Shared.EventArgsFormatting directly
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 0, 0, 0, 0, 0);
            Assert.Equal(
                      "source.cs : CS error 312: Missing ;", s);
        }

        // Valid forms for line/col number patterns:
        // (line) or (line-line) or (line,col) or (line,col-col) or (line,col,line,col)
        [Fact]
        public void LineNumberRange()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 2, 0, 0, 0);
            Assert.Equal(
                      "source.cs(1-2): CS error 312: Missing ;", s);
        }

        [Fact]
        public void ColumnNumberRange()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 0, 0, 1, 2, 0);
            Assert.Equal(
                      "source.cs : CS error 312: Missing ;", s);
        }

        [Fact]
        public void LineAndColumnNumberRange()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 2, 3, 4, 0);
            Assert.Equal(
                      "source.cs(1,3,2,4): CS error 312: Missing ;", s);
        }

        [Fact]
        public void LineAndColumnNumberRange2()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 0, 3, 4, 0);
            Assert.Equal(
                      "source.cs(1,3-4): CS error 312: Missing ;", s);
        }

        [Fact]
        public void LineAndColumnNumberRange3()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 2, 3, 0, 0);
            Assert.Equal(
                      "source.cs(1-2,3): CS error 312: Missing ;", s);
        }

        [Fact]
        public void LineAndColumnNumberRange4()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 2, 0, 3, 0);
            Assert.Equal(
                      "source.cs(1-2): CS error 312: Missing ;", s);
        }

        [Fact]
        public void LineAndColumnNumberRange5()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 0, 2, 0, 0);
            Assert.Equal(
                      "source.cs(1,2): CS error 312: Missing ;", s);
        }

        [Fact]
        public void BasicFormatEventMessage()
        {
            // Testing the method in Shared.EventArgsFormatting directly
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 42, 0, 0, 0, 0);
            Assert.Equal(
                      "source.cs(42): CS error 312: Missing ;", s);
        }

        [Fact]
        public void EscapeCarriageReturnMessages()
        {
            BuildErrorEventArgs error = new BuildErrorEventArgs("CS", "312", "source.cs", 42, 0, 0, 0, "message\r Hello", "help", "sender");
            BuildWarningEventArgs warning = new BuildWarningEventArgs("CS", "312", "source.cs", 42, 0, 0, 0, "message\r Hello", "help", "sender");

            // Testing the method in Shared.EventArgsFormatting directly
            string errorString = EventArgsFormatting.FormatEventMessage(error, true);
            string warningString = EventArgsFormatting.FormatEventMessage(warning, true);
            string errorString2 = EventArgsFormatting.FormatEventMessage(error, false);
            string warningString2 = EventArgsFormatting.FormatEventMessage(warning, false);

            Assert.Equal("source.cs(42): CS error 312: message\\r Hello", errorString);
            Assert.Equal("source.cs(42): CS warning 312: message\\r Hello", warningString);

            Assert.Equal("source.cs(42): CS error 312: message\r Hello", errorString2);
            Assert.Equal("source.cs(42): CS warning 312: message\r Hello", warningString2);
        }

        [Fact]
        public void ExactLocationFormatEventMessage()
        {
            // Testing the method in Shared.EventArgsFormatting directly
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 233, 236, 4, 8, 0);
            Assert.Equal(
                    "source.cs(233,4,236,8): CS error 312: Missing ;", s);
        }

        [Fact]
        public void NullMessage()
        {
            // Testing the method in Shared.EventArgsFormatting directly
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      null, "312", "source.cs", 233, 236, 4, 8, 0);
            // No exception was thrown

        }

        /// <summary>
        /// Mainline test FormatEventMessage(BuildErrorEvent) 's common case
        /// </summary>
        [Fact]
        public void FormatEventMessageOnBEEA()
        {
            MyLogger l = new MyLogger();
            BuildErrorEventArgs beea = new BuildErrorEventArgs("VBC",
                        "31415", "file.vb", 42, 0, 0, 0,
                        "Some long message", "help", "sender");
            string s = l.FormatErrorEvent(beea);
            Assert.Equal(
               "file.vb(42): VBC error 31415: Some long message", s);
        }

        /// <summary>
        /// Mainline test FormatEventMessage(BuildWarningEvent) 's common case
        /// </summary>
        [Fact]
        public void FormatEventMessageOnBWEA()
        {
            MyLogger l = new MyLogger();
            BuildWarningEventArgs bwea = new BuildWarningEventArgs("VBC",
                        "31415", "file.vb", 42, 0, 0, 0,
                        "Some long message", "help", "sender");
            string s = l.FormatWarningEvent(bwea);
            Assert.Equal(
               "file.vb(42): VBC warning 31415: Some long message", s);
        }

        /// <summary>
        /// Check null handling
        /// </summary>
        [Fact]
        public void FormatEventMessageOnNullBEEA()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                MyLogger l = new MyLogger();
                BuildErrorEventArgs beea = null;
                string s = l.FormatErrorEvent(beea);
            }
           );
        }
        /// <summary>
        /// Check null handling
        /// </summary>
        [Fact]
        public void FormatEventMessageOnNullBWEA()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                MyLogger l = new MyLogger();
                BuildWarningEventArgs bwea = null;
                string s = l.FormatWarningEvent(bwea);
            }
           );
        }
    }

    /// <summary>
    /// Minimal logger implementation
    /// </summary>
    internal class MyLogger : Logger
    {
        public override void Initialize(IEventSource eventSource)
        {
            // do nothing
        }
    }
}

