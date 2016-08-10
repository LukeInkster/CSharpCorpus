﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class CustomEventArgSerialization_Tests : IDisposable
    {
        // Generic build class to test custom serialization of abstract class BuildEventArgs
        internal class GenericBuildEventArg : BuildEventArgs
        {
            internal GenericBuildEventArg
        (
            string message,
            string helpKeyword,
            string senderName
        )
                : base(message, helpKeyword, senderName)
            {
                //Do Nothing
            }
        }

        // Stream, writer and reader where the events will be serialized and deserialized from
        private MemoryStream _stream;
        private BinaryWriter _writer;
        private BinaryReader _reader;

        private int _eventArgVersion = (Environment.Version.Major * 10) + Environment.Version.Minor;

        public CustomEventArgSerialization_Tests()
        {
            _stream = new MemoryStream();
            _writer = new BinaryWriter(_stream);
            _reader = new BinaryReader(_stream);
        }

        public void Dispose()
        {
            // Close will close the writer/reader and the underlying stream
            _writer.Close();
            _reader.Close();
            _reader = null;
            _stream = null;
            _writer = null;
        }

        [Fact]
        public void TestGenericBuildEventArgs()
        {
            // Test using reasonable messages
            GenericBuildEventArg genericEvent = new GenericBuildEventArg("Message", "HelpKeyword", "SenderName");
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            // Get position of stream after write so it can be compared to the position after read
            long streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            GenericBuildEventArg newGenericEvent = new GenericBuildEventArg(null, null, null);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            long streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);

            // Test using empty strings
            _stream.Position = 0;
            genericEvent = new GenericBuildEventArg(string.Empty, string.Empty, string.Empty);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new GenericBuildEventArg(null, null, null);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);

            // Test using null strings
            _stream.Position = 0;
            genericEvent = new GenericBuildEventArg(null, null, null);
            genericEvent.BuildEventContext = null;

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new GenericBuildEventArg(null, null, null);
            newGenericEvent.BuildEventContext = new BuildEventContext(1, 3, 4, 5);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
        }

        /// <summary>
        /// Compares two BuildEventArgs
        /// </summary>
        private static void VerifyGenericEventArg(BuildEventArgs genericEvent, BuildEventArgs newGenericEvent)
        {
            Assert.Equal(genericEvent.BuildEventContext, newGenericEvent.BuildEventContext); // "Expected Event Context to Match"
            Assert.Equal(0, string.Compare(genericEvent.HelpKeyword, newGenericEvent.HelpKeyword, StringComparison.OrdinalIgnoreCase)); // "Expected Help Keywords to Match"
            Assert.Equal(0, string.Compare(genericEvent.Message, newGenericEvent.Message, StringComparison.OrdinalIgnoreCase)); // "Expected Message to Match"
            Assert.Equal(0, string.Compare(genericEvent.SenderName, newGenericEvent.SenderName, StringComparison.OrdinalIgnoreCase)); // "Expected Sender Name to Match"
            Assert.Equal(genericEvent.ThreadId, newGenericEvent.ThreadId); // "Expected ThreadId to Match"
            Assert.Equal(genericEvent.Timestamp, newGenericEvent.Timestamp); // "Expected TimeStamp to Match"
        }

        [Fact]
        public void TestBuildErrorEventArgs()
        {
            // Test using reasonable messages
            BuildErrorEventArgs genericEvent = new BuildErrorEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "SenderName");
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            long streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            BuildErrorEventArgs newGenericEvent = new BuildErrorEventArgs(null, null, null, -1, -1, -1, -1, null, null, null);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            long streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyBuildErrorEventArgs(genericEvent, newGenericEvent);

            // Test using empty strings
            _stream.Position = 0;
            genericEvent = new BuildErrorEventArgs(string.Empty, string.Empty, string.Empty, 1, 2, 3, 4, string.Empty, string.Empty, string.Empty);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new BuildErrorEventArgs(null, null, null, -1, -1, -1, -1, null, null, null);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyBuildErrorEventArgs(genericEvent, newGenericEvent);

            // Test using null strings
            _stream.Position = 0;
            genericEvent = new BuildErrorEventArgs(null, null, null, 1, 2, 3, 4, null, null, null);
            genericEvent.BuildEventContext = null;

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new BuildErrorEventArgs("Something", "SomeThing", "SomeThing", -1, -1, -1, -1, "Something", "SomeThing", "Something");
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyBuildErrorEventArgs(genericEvent, newGenericEvent);
        }

        /// <summary>
        /// Compare two BuildEventArgs 
        /// </summary>
        private static void VerifyBuildErrorEventArgs(BuildErrorEventArgs genericEvent, BuildErrorEventArgs newGenericEvent)
        {
            Assert.Equal(0, string.Compare(genericEvent.Code, newGenericEvent.Code, StringComparison.OrdinalIgnoreCase)); // "Expected Code to Match"
            Assert.Equal(0, string.Compare(genericEvent.File, newGenericEvent.File, StringComparison.OrdinalIgnoreCase)); // "Expected File to Match"
            Assert.Equal(genericEvent.ColumnNumber, newGenericEvent.ColumnNumber); // "Expected ColumnNumber to Match"
            Assert.Equal(genericEvent.EndColumnNumber, newGenericEvent.EndColumnNumber); // "Expected EndColumnNumber to Match"
            Assert.Equal(genericEvent.EndLineNumber, newGenericEvent.EndLineNumber); // "Expected EndLineNumber to Match"
        }


        [Fact]
        public void TestBuildFinishedEventArgs()
        {
            // Test using reasonable messages
            BuildFinishedEventArgs genericEvent = new BuildFinishedEventArgs("Message", "HelpKeyword", true);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);

            // Deserialize and Verify
            _stream.Position = 0;
            BuildFinishedEventArgs newGenericEvent = new BuildFinishedEventArgs(null, null, false);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            Assert.Equal(genericEvent.Succeeded, newGenericEvent.Succeeded); // "Expected Succeeded to Match"

            // Test using empty strings
            _stream.Position = 0;
            genericEvent = new BuildFinishedEventArgs(string.Empty, string.Empty, true);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new BuildFinishedEventArgs(null, null, false);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            Assert.Equal(genericEvent.Succeeded, newGenericEvent.Succeeded); // "Expected Succeeded to Match"

            // Test using null strings
            _stream.Position = 0;
            genericEvent = new BuildFinishedEventArgs(null, null, true);
            genericEvent.BuildEventContext = null;

            // Serialize
            genericEvent.WriteToStream(_writer);

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new BuildFinishedEventArgs("Something", "Something", false);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            Assert.Equal(genericEvent.Succeeded, newGenericEvent.Succeeded); // "Expected Succeeded to Match"
        }

        [Fact]
        public void TestBuildMessageEventArgs()
        {
            // Test using reasonable messages
            BuildMessageEventArgs genericEvent = new BuildMessageEventArgs("Message", "HelpKeyword", "SenderName", MessageImportance.High);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            long streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            BuildMessageEventArgs newGenericEvent = new BuildMessageEventArgs(null, null, null, MessageImportance.Low);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            long streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            Assert.Equal(genericEvent.Importance, newGenericEvent.Importance); // "Expected Message Importance to Match"

            // Test empty strings
            _stream.Position = 0;
            // Make sure empty strings are passed correctly
            genericEvent = new BuildMessageEventArgs(string.Empty, string.Empty, string.Empty, MessageImportance.Low);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new BuildMessageEventArgs(null, null, null, MessageImportance.Low);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            Assert.Equal(genericEvent.Importance, newGenericEvent.Importance); // "Expected Message Importance to Match"

            // Test null strings
            _stream.Position = 0;
            // Make sure null string are passed correctly
            genericEvent = new BuildMessageEventArgs(null, null, null, MessageImportance.Low);
            genericEvent.BuildEventContext = null;

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new BuildMessageEventArgs("Something", "Something", "Something", MessageImportance.Low);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            Assert.Equal(genericEvent.Importance, newGenericEvent.Importance); // "Expected Message Importance to Match"
        }

        private void VerifyMessageEventArg(BuildMessageEventArgs messageEvent, BuildMessageEventArgs newMessageEvent)
        {
            VerifyGenericEventArg(messageEvent, newMessageEvent);

            Assert.Equal(messageEvent.Importance, newMessageEvent.Importance); // "Expected Message Importance to Match"
            Assert.Equal(messageEvent.Subcategory, newMessageEvent.Subcategory); // "Expected message Subcategory to match"
            Assert.Equal(messageEvent.Code, newMessageEvent.Code); // "Expected message Code to match"
            Assert.Equal(messageEvent.File, newMessageEvent.File); // "Expected message File to match"
            Assert.Equal(messageEvent.LineNumber, newMessageEvent.LineNumber); // "Expected message LineNumber to match"
            Assert.Equal(messageEvent.ColumnNumber, newMessageEvent.ColumnNumber); // "Expected message ColumnNumber to match"
            Assert.Equal(messageEvent.EndLineNumber, newMessageEvent.EndLineNumber); // "Expected message EndLineNumber to match"
            Assert.Equal(messageEvent.EndColumnNumber, newMessageEvent.EndColumnNumber); // "Expected message EndColumnNumber to match"
        }

        [Fact]
        public void TestBuildMessageEventArgsWithFileInfo()
        {
            // Test using reasonable messages
            BuildMessageEventArgs messageEvent = new BuildMessageEventArgs("SubCategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "SenderName", MessageImportance.High);
            messageEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            messageEvent.WriteToStream(_writer);
            long streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            BuildMessageEventArgs newMessageEvent = new BuildMessageEventArgs(null, null, null, 0, 0, 0, 0, null, null, null, MessageImportance.Low);
            newMessageEvent.CreateFromStream(_reader, _eventArgVersion);
            long streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyMessageEventArg(messageEvent, newMessageEvent);

            // Test empty strings
            _stream.Position = 0;
            // Make sure empty strings are passed correctly
            messageEvent = new BuildMessageEventArgs(string.Empty, string.Empty, string.Empty, 1, 2, 3, 4, string.Empty, string.Empty, string.Empty, MessageImportance.Low);
            messageEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            messageEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newMessageEvent = new BuildMessageEventArgs(null, null, null, 0, 0, 0, 0, null, null, null, MessageImportance.Low);
            newMessageEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyMessageEventArg(messageEvent, newMessageEvent);

            // Test null strings
            _stream.Position = 0;
            // Make sure null string are passed correctly
            messageEvent = new BuildMessageEventArgs(null, null, null, 1, 2, 3, 4, null, null, null, MessageImportance.Low);
            messageEvent.BuildEventContext = null;

            // Serialize
            messageEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newMessageEvent = new BuildMessageEventArgs("Something", "Something", "Something", 0, 0, 0, 0, "Something", "Something", "Something", MessageImportance.Low);
            newMessageEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyMessageEventArg(messageEvent, newMessageEvent);
        }

        [Fact]
        public void TestCriticalBuildMessageEventArgs()
        {
            // Test using reasonable messages
            CriticalBuildMessageEventArgs criticalMessageEvent = new CriticalBuildMessageEventArgs("SubCategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "SenderName");
            criticalMessageEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            criticalMessageEvent.WriteToStream(_writer);
            long streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            CriticalBuildMessageEventArgs newCriticalMessageEvent = new CriticalBuildMessageEventArgs(null, null, null, 0, 0, 0, 0, null, null, null);
            newCriticalMessageEvent.CreateFromStream(_reader, _eventArgVersion);
            long streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyMessageEventArg(criticalMessageEvent, newCriticalMessageEvent);

            // Test empty strings
            _stream.Position = 0;
            // Make sure empty strings are passed correctly
            criticalMessageEvent = new CriticalBuildMessageEventArgs(string.Empty, string.Empty, string.Empty, 1, 2, 3, 4, string.Empty, string.Empty, string.Empty);
            criticalMessageEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            criticalMessageEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newCriticalMessageEvent = new CriticalBuildMessageEventArgs(null, null, null, 0, 0, 0, 0, null, null, null);
            newCriticalMessageEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyMessageEventArg(criticalMessageEvent, newCriticalMessageEvent);

            // Test null strings
            _stream.Position = 0;
            // Make sure null string are passed correctly
            criticalMessageEvent = new CriticalBuildMessageEventArgs(null, null, null, 1, 2, 3, 4, null, null, null);
            criticalMessageEvent.BuildEventContext = null;

            // Serialize
            criticalMessageEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newCriticalMessageEvent = new CriticalBuildMessageEventArgs("Something", "Something", "Something", 0, 0, 0, 0, "Something", "Something", "Something");
            newCriticalMessageEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyMessageEventArg(criticalMessageEvent, newCriticalMessageEvent);
        }

        [Fact]
        public void TestBuildWarningEventArgs()
        {
            // Test with reasonable messages
            BuildWarningEventArgs genericEvent = new BuildWarningEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "SenderName");
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            long streamWriteEndPosition = _stream.Position;

            //Deserialize and Verify
            _stream.Position = 0;
            BuildWarningEventArgs newGenericEvent = new BuildWarningEventArgs(null, null, null, -1, -1, -1, -1, null, null, null);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            long streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyBuildWarningEventArgs(genericEvent, newGenericEvent);

            // Test with empty strings
            _stream.Position = 0;
            genericEvent = new BuildWarningEventArgs(string.Empty, string.Empty, string.Empty, 1, 2, 3, 4, string.Empty, string.Empty, string.Empty);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            //Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new BuildWarningEventArgs(null, null, null, -1, -1, -1, -1, null, null, null);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyBuildWarningEventArgs(genericEvent, newGenericEvent);

            // Test with null strings
            _stream.Position = 0;
            genericEvent = new BuildWarningEventArgs(null, null, null, 1, 2, 3, 4, null, null, null);
            genericEvent.BuildEventContext = null;

            //Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            //Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new BuildWarningEventArgs("Something", "SomeThing", "SomeThing", -1, -1, -1, -1, "Something", "SomeThing", "Something");
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyBuildWarningEventArgs(genericEvent, newGenericEvent);
        }

        /// <summary>
        /// Compares two build warning events
        /// </summary>
        private static void VerifyBuildWarningEventArgs(BuildWarningEventArgs genericEvent, BuildWarningEventArgs newGenericEvent)
        {
            Assert.Equal(0, string.Compare(genericEvent.Subcategory, newGenericEvent.Subcategory, StringComparison.OrdinalIgnoreCase)); // "Expected SubCategory to Match"
            Assert.Equal(0, string.Compare(genericEvent.Code, newGenericEvent.Code, StringComparison.OrdinalIgnoreCase)); // "Expected Code to Match"
            Assert.Equal(0, string.Compare(genericEvent.File, newGenericEvent.File, StringComparison.OrdinalIgnoreCase)); // "Expected File to Match"
            Assert.Equal(genericEvent.ColumnNumber, newGenericEvent.ColumnNumber); // "Expected ColumnNumber to Match"
            Assert.Equal(genericEvent.EndColumnNumber, newGenericEvent.EndColumnNumber); // "Expected EndColumnNumber to Match"
            Assert.Equal(genericEvent.EndLineNumber, newGenericEvent.EndLineNumber); // "Expected EndLineNumber to Match"
        }

        [Fact]
        public void TestProjectFinishedEventArgs()
        {
            // Test with reasonable values
            ProjectFinishedEventArgs genericEvent = new ProjectFinishedEventArgs("Message", "HelpKeyword", "ProjectFile", true);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            long streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            ProjectFinishedEventArgs newGenericEvent = new ProjectFinishedEventArgs(null, null, null, false);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            long streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            Assert.Equal(genericEvent.Succeeded, newGenericEvent.Succeeded); // "Expected Succeeded to Match"
            Assert.Equal(0, string.Compare(genericEvent.ProjectFile, newGenericEvent.ProjectFile, StringComparison.OrdinalIgnoreCase)); // "Expected ProjectFile to Match"

            // Test with empty strings
            _stream.Position = 0;
            genericEvent = new ProjectFinishedEventArgs(string.Empty, string.Empty, string.Empty, true);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new ProjectFinishedEventArgs(null, null, null, false);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            Assert.Equal(genericEvent.Succeeded, newGenericEvent.Succeeded); // "Expected Succeeded to Match"
            Assert.Equal(0, string.Compare(genericEvent.ProjectFile, newGenericEvent.ProjectFile, StringComparison.OrdinalIgnoreCase)); // "Expected ProjectFile to Match"

            // Test with null strings
            _stream.Position = 0;
            // Make sure null string are passed correctly
            genericEvent = new ProjectFinishedEventArgs(null, null, null, true);
            genericEvent.BuildEventContext = null;

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new ProjectFinishedEventArgs("Something", "Something", "Something", false);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            Assert.Equal(genericEvent.Succeeded, newGenericEvent.Succeeded); // "Expected Succeeded to Match"
            Assert.Equal(0, string.Compare(genericEvent.ProjectFile, newGenericEvent.ProjectFile, StringComparison.OrdinalIgnoreCase)); // "Expected ProjectFile to Match"
        }

        [Fact]
        public void TestProjectStartedPropertySerialization()
        {
            // Create a list of test properties which should make it through serialization
            List<DictionaryEntry> propertyList = new List<DictionaryEntry>();
            propertyList.Add(new DictionaryEntry("TeamBuildOutDir", "c:\\outdir"));
            propertyList.Add(new DictionaryEntry("Configuration", "BuildConfiguration"));
            propertyList.Add(new DictionaryEntry("Platform", "System Platform"));
            propertyList.Add(new DictionaryEntry("OutDir", "myOutDir"));
            propertyList.Add(new DictionaryEntry("WorkSpaceName", " MyWorkspace"));
            propertyList.Add(new DictionaryEntry("WorkSpaceOwner", "The workspace owner"));
            propertyList.Add(new DictionaryEntry("IAmBlank", string.Empty));

            ProjectStartedEventArgs genericEvent = new ProjectStartedEventArgs(8, "Message", "HelpKeyword", "ProjectFile", null, propertyList, null, new BuildEventContext(7, 8, 9, 10));
            genericEvent.BuildEventContext = new BuildEventContext(7, 8, 9, 10);

            // Serialize
            genericEvent.WriteToStream(_writer);
            long streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            ProjectStartedEventArgs newGenericEvent = new ProjectStartedEventArgs(-1, null, null, null, null, null, null, null);

            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            long streamReadEndPosition = _stream.Position;

            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            Assert.NotNull(newGenericEvent.Properties); // "Expected Properties to not be null"

            // Create a list of all of the dictionaryEntries which were deserialized
            List<DictionaryEntry> entryList = new List<DictionaryEntry>();
            foreach (DictionaryEntry entry in newGenericEvent.Properties)
            {
                entryList.Add(entry);
            }

            // Verify that each of the items in propertyList is inside of the deserialized entryList.
            AssertDictionaryEntry(entryList, propertyList);
        }

        /// <summary>
        /// Compare the BuildProperties in propertyList with the Name Value pairs in the entryList. 
        /// We need to make sure that each of the BuildProperties passed into the serializer come out correctly
        /// </summary>
        /// <param name="entryList">List of DictionaryEntries which were deserialized</param>
        /// <param name="propertyList">List of BuildProperties which were serialized</param>
        private void AssertDictionaryEntry(List<DictionaryEntry> entryList, List<DictionaryEntry> propertyList)
        {
            // make sure that there are the same number of elements in both lists as a quick initial check
            Assert.Equal(propertyList.Count, entryList.Count);

            // Go through each of the properties which were serialized and make sure we find the exact same
            // name and value in the deserialized version.
            foreach (DictionaryEntry property in propertyList)
            {
                bool found = false;
                foreach (DictionaryEntry entry in entryList)
                {
                    string key = (string)entry.Key;
                    string value = (string)entry.Value;
                    if (key.Equals((string)property.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (value.Equals((string)property.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                        }
                    }
                }

                Assert.True(found, "Expected to find Key:" + property.Key + " Value:" + property.Value);
            }
        }

        [Fact]
        public void TestProjectStartedEventArgs()
        {
            // Test with reasonable values
            ProjectStartedEventArgs genericEvent = new ProjectStartedEventArgs(8, "Message", "HelpKeyword", "ProjectFile", null, null, null, new BuildEventContext(7, 8, 9, 10));
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            long streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            ProjectStartedEventArgs newGenericEvent = new ProjectStartedEventArgs(-1, null, null, null, null, null, null, null);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            long streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyProjectStartedEvent(genericEvent, newGenericEvent);

            // Test with empty strings
            _stream.Position = 0;
            genericEvent = new ProjectStartedEventArgs(-1, string.Empty, string.Empty, string.Empty, string.Empty, null, null, null);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new ProjectStartedEventArgs(-1, null, null, null, null, null, null, null);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream end positions should be equal"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyProjectStartedEvent(genericEvent, newGenericEvent);

            // Test with null strings
            _stream.Position = 0;
            genericEvent = new ProjectStartedEventArgs(-1, null, null, null, null, null, null, null);
            genericEvent.BuildEventContext = null;

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new ProjectStartedEventArgs(4, "Something", "Something", "Something", null, null, null, null);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyProjectStartedEvent(genericEvent, newGenericEvent);
        }

        /// <summary>
        /// Compare two project started events 
        /// </summary>
        private static void VerifyProjectStartedEvent(ProjectStartedEventArgs genericEvent, ProjectStartedEventArgs newGenericEvent)
        {
            Assert.Equal(genericEvent.Items, newGenericEvent.Items); // "Expected Properties to match"
            Assert.Equal(genericEvent.Properties, newGenericEvent.Properties); // "Expected Properties to match"
            Assert.Equal(genericEvent.ParentProjectBuildEventContext, newGenericEvent.ParentProjectBuildEventContext); // "Expected ParentEvent Contexts to match"
            Assert.Equal(genericEvent.ProjectId, newGenericEvent.ProjectId); // "Expected ProjectId to Match"
            Assert.Equal(0, string.Compare(genericEvent.ProjectFile, newGenericEvent.ProjectFile, StringComparison.OrdinalIgnoreCase)); // "Expected ProjectFile to Match"
            Assert.Equal(0, string.Compare(genericEvent.TargetNames, newGenericEvent.TargetNames, StringComparison.OrdinalIgnoreCase)); // "Expected TargetNames to Match"
        }

        [Fact]
        public void TestTargetStartedEventArgs()
        {
            // Test using reasonable values
            TargetStartedEventArgs genericEvent = new TargetStartedEventArgs("Message", "HelpKeyword", "TargetName", "ProjectFile", "TargetFile", "ParentTargetStartedEvent", DateTime.UtcNow);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            long streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            TargetStartedEventArgs newGenericEvent = new TargetStartedEventArgs(null, null, null, null, null);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            long streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyTargetStarted(genericEvent, newGenericEvent);

            //Test using Empty strings
            _stream.Position = 0;
            // Make sure empty strings are passed correctly
            genericEvent = new TargetStartedEventArgs(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, DateTime.Now);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new TargetStartedEventArgs(null, null, null, null, null);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyTargetStarted(genericEvent, newGenericEvent);

            // Test using null strings
            _stream.Position = 0;
            // Make sure null string are passed correctly
            genericEvent = new TargetStartedEventArgs(null, null, null, null, null, null, DateTime.Now);
            genericEvent.BuildEventContext = null;
            //Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;
            //Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new TargetStartedEventArgs("Something", "Something", "Something", "Something", "Something", "Something", DateTime.Now);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyTargetStarted(genericEvent, newGenericEvent);
        }

        /// <summary>
        /// Compare two targetStarted events
        /// </summary>
        private static void VerifyTargetStarted(TargetStartedEventArgs genericEvent, TargetStartedEventArgs newGenericEvent)
        {
            Assert.Equal(0, string.Compare(genericEvent.TargetFile, newGenericEvent.TargetFile, StringComparison.OrdinalIgnoreCase)); // "Expected TargetFile to Match"
            Assert.Equal(0, string.Compare(genericEvent.ProjectFile, newGenericEvent.ProjectFile, StringComparison.OrdinalIgnoreCase)); // "Expected ProjectFile to Match"
            Assert.Equal(0, string.Compare(genericEvent.TargetName, newGenericEvent.TargetName, StringComparison.OrdinalIgnoreCase)); // "Expected TargetName to Match"
            Assert.Equal(0, string.Compare(genericEvent.ParentTarget, newGenericEvent.ParentTarget, StringComparison.OrdinalIgnoreCase)); // "Expected ParentTarget to Match"
        }

        [Fact]
        public void TestTargetFinishedEventArgs()
        {
            // Test using reasonable values
            TargetFinishedEventArgs genericEvent = new TargetFinishedEventArgs("Message", "HelpKeyword", "TargetName", "ProjectFile", "TargetFile", true);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            long streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            TargetFinishedEventArgs newGenericEvent = new TargetFinishedEventArgs(null, null, null, null, null, false);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            long streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyTargetFinished(genericEvent, newGenericEvent);

            // Test using empty strings
            _stream.Position = 0;
            genericEvent = new TargetFinishedEventArgs(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, true);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new TargetFinishedEventArgs(null, null, null, null, null, false);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyTargetFinished(genericEvent, newGenericEvent);

            // Test using null strings
            _stream.Position = 0;
            // Make sure null string are passed correctly
            genericEvent = new TargetFinishedEventArgs(null, null, null, null, null, true);
            genericEvent.BuildEventContext = null;
            //Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;
            //Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new TargetFinishedEventArgs("Something", "Something", "Something", "Something", "Something", false);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyTargetFinished(genericEvent, newGenericEvent);
        }

        /// <summary>
        /// Compare two TargetFinished events
        /// </summary>
        private static void VerifyTargetFinished(TargetFinishedEventArgs genericEvent, TargetFinishedEventArgs newGenericEvent)
        {
            Assert.Equal(genericEvent.Succeeded, newGenericEvent.Succeeded); // "Expected Succeeded to Match"
            Assert.Equal(0, string.Compare(genericEvent.ProjectFile, newGenericEvent.ProjectFile, StringComparison.OrdinalIgnoreCase)); // "Expected ProjectFile to Match"
            Assert.Equal(0, string.Compare(genericEvent.TargetFile, newGenericEvent.TargetFile, StringComparison.OrdinalIgnoreCase)); // "Expected TargetFile to Match"
            Assert.Equal(0, string.Compare(genericEvent.TargetName, newGenericEvent.TargetName, StringComparison.OrdinalIgnoreCase)); // "Expected TargetName to Match"
        }

        [Fact]
        public void TestTaskStartedEventArgs()
        {
            // Test using reasonable values
            TaskStartedEventArgs genericEvent = new TaskStartedEventArgs("Message", "HelpKeyword", "ProjectFile", "TaskFile", "TaskName");
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            long streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            TaskStartedEventArgs newGenericEvent = new TaskStartedEventArgs(null, null, null, null, null);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            long streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyTaskStarted(genericEvent, newGenericEvent);

            _stream.Position = 0;
            // Make sure empty strings are passed correctly
            genericEvent = new TaskStartedEventArgs(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new TaskStartedEventArgs(null, null, null, null, null);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyTaskStarted(genericEvent, newGenericEvent);

            // Test using null strings
            _stream.Position = 0;
            // Make sure null string are passed correctly
            genericEvent = new TaskStartedEventArgs(null, null, null, null, null);
            genericEvent.BuildEventContext = null;

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new TaskStartedEventArgs("Something", "Something", "Something", "Something", "Something");
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyTaskStarted(genericEvent, newGenericEvent);
        }

        /// <summary>
        /// Compare two TaskStarted events
        /// </summary>
        private static void VerifyTaskStarted(TaskStartedEventArgs genericEvent, TaskStartedEventArgs newGenericEvent)
        {
            Assert.Equal(0, string.Compare(genericEvent.TaskFile, newGenericEvent.TaskFile, StringComparison.OrdinalIgnoreCase)); // "Expected TaskFile to Match"
            Assert.Equal(0, string.Compare(genericEvent.ProjectFile, newGenericEvent.ProjectFile, StringComparison.OrdinalIgnoreCase)); // "Expected ProjectFile to Match"
            Assert.Equal(0, string.Compare(genericEvent.TaskName, newGenericEvent.TaskName, StringComparison.OrdinalIgnoreCase)); // "Expected TaskName to Match"
        }

        [Fact]
        public void TestTaskFinishedEventArgs()
        {
            // Test using reasonable values
            TaskFinishedEventArgs genericEvent = new TaskFinishedEventArgs("Message", "HelpKeyword", "ProjectFile", "TaskFile", "TaskName", true);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            long streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            TaskFinishedEventArgs newGenericEvent = new TaskFinishedEventArgs(null, null, null, null, null, false);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            long streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyTaskFinished(genericEvent, newGenericEvent);

            //Test using empty strings
            _stream.Position = 0;
            // Make sure empty strings are passed correctly
            genericEvent = new TaskFinishedEventArgs(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, true);
            genericEvent.BuildEventContext = new BuildEventContext(5, 4, 3, 2);

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new TaskFinishedEventArgs(null, null, null, null, null, false);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyTaskFinished(genericEvent, newGenericEvent);

            //Test using null strings
            _stream.Position = 0;
            // Make sure null string are passed correctly
            genericEvent = new TaskFinishedEventArgs(null, null, null, null, null, true);
            genericEvent.BuildEventContext = null;

            // Serialize
            genericEvent.WriteToStream(_writer);
            streamWriteEndPosition = _stream.Position;

            // Deserialize and Verify
            _stream.Position = 0;
            newGenericEvent = new TaskFinishedEventArgs("Something", "Something", "Something", "Something", "Something", false);
            newGenericEvent.CreateFromStream(_reader, _eventArgVersion);
            streamReadEndPosition = _stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream End Positions Should Match"
            VerifyGenericEventArg(genericEvent, newGenericEvent);
            VerifyTaskFinished(genericEvent, newGenericEvent);
        }

        /// <summary>
        /// Compare two task finished events
        /// </summary>
        private static void VerifyTaskFinished(TaskFinishedEventArgs genericEvent, TaskFinishedEventArgs newGenericEvent)
        {
            Assert.Equal(genericEvent.Succeeded, newGenericEvent.Succeeded); // "Expected Succeeded to Match"
            Assert.Equal(0, string.Compare(genericEvent.ProjectFile, newGenericEvent.ProjectFile, StringComparison.OrdinalIgnoreCase)); // "Expected ProjectFile to Match"
            Assert.Equal(0, string.Compare(genericEvent.TaskFile, newGenericEvent.TaskFile, StringComparison.OrdinalIgnoreCase)); // "Expected TaskFile to Match"
            Assert.Equal(0, string.Compare(genericEvent.TaskName, newGenericEvent.TaskName, StringComparison.OrdinalIgnoreCase)); // "Expected TaskName to Match"
        }
    }
}
