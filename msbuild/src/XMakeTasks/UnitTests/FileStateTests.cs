﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;
using System.Globalization;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Test FileState utility class
    /// </summary>
    public class FileStateTests
    {
        [Fact]
        public void BadNoName()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                new FileState("");
            }
           );
        }
        [Fact]
        public void BadCharsCtorOK()
        {
            new FileState("|");
        }

        [Fact]
        public void BadTooLongCtorOK()
        {
            new FileState(new String('x', 5000));
        }

        [Fact]
        public void BadChars()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                var state = new FileState("|");
                var time = state.LastWriteTime;
            }
           );
        }
        [Fact]
        public void BadTooLongLastWriteTime()
        {
            Helpers.VerifyAssertThrowsSameWay(
                delegate () { var x = new FileInfo(new String('x', 5000)).LastWriteTime; },
                delegate () { var x = new FileState(new String('x', 5000)).LastWriteTime; });
        }

        [Fact]
        public void Exists()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.Equal(info.Exists, state.FileExists);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void Name()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.Equal(info.FullName, state.Name);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void IsDirectoryTrue()
        {
            var state = new FileState(Path.GetTempPath());

            Assert.Equal(true, state.IsDirectory);
        }

        [Fact]
        public void LastWriteTime()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.Equal(info.LastWriteTime, state.LastWriteTime);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void LastWriteTimeUtc()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.Equal(info.LastWriteTimeUtc, state.LastWriteTimeUtcFast);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void Length()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.Equal(info.Length, state.Length);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void AccessDenied()
        {
            string locked = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "system32\\notepad.exe");

            FileInfo info = new FileInfo(locked);
            FileState state = new FileState(locked);

            Assert.Equal(info.Exists, state.FileExists);

            if (!info.Exists)
            {
                // meh, somewhere else
                return;
            }

            Assert.Equal(info.Length, state.Length);
            Assert.Equal(info.LastWriteTime, state.LastWriteTime);
            Assert.Equal(info.LastWriteTimeUtc, state.LastWriteTimeUtcFast);
        }

#if CHECKING4GBFILESWORK
        [TestMethod]
        public void LengthHuge()
        {
            var bigFile = @"d:\proj\hugefile";
            //var dummy = new string('x', 10000000);
            //using (StreamWriter w = new StreamWriter(bigFile))
            //{
            //    for (int i = 0; i < 450; i++)
            //    {
            //        w.Write(dummy);
            //    }
            //}

            Console.WriteLine((new FileState(bigFile)).Length);

            FileInfo info = new FileInfo(bigFile);
            FileState state = new FileState(bigFile);

            Assert.AreEqual(info.Exists, state.Exists);

            if (!info.Exists)
            {
                // meh, somewhere else  
                return;
            }

            Assert.AreEqual(info.Length, state.Length);
        }
#endif

        [Fact]
        public void ReadOnly()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.Equal(info.IsReadOnly, state.IsReadOnly);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void ExistsReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.Equal(info.Exists, state.FileExists);
                File.Delete(file);
                Assert.Equal(true, state.FileExists);
                state.Reset();
                Assert.Equal(false, state.FileExists);
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        [Fact]
        public void NameReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.Equal(info.FullName, state.Name);
                string originalName = info.FullName;
                string oldFile = file;
                file = oldFile + "2";
                File.Move(oldFile, file);
                Assert.Equal(originalName, state.Name);
                state.Reset();
                Assert.Equal(originalName, state.Name); // Name is from the constructor, didn't change
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void LastWriteTimeReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.Equal(info.LastWriteTime, state.LastWriteTime);

                var time = new DateTime(2111, 1, 1);
                info.LastWriteTime = time;

                Assert.NotEqual(time, state.LastWriteTime);
                state.Reset();
                Assert.Equal(time, state.LastWriteTime);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void LastWriteTimeUtcReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.Equal(info.LastWriteTimeUtc, state.LastWriteTimeUtcFast);

                var time = new DateTime(2111, 1, 1);
                info.LastWriteTime = time;

                Assert.NotEqual(time.ToUniversalTime(), state.LastWriteTimeUtcFast);
                state.Reset();
                Assert.Equal(time.ToUniversalTime(), state.LastWriteTimeUtcFast);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void LengthReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.Equal(info.Length, state.Length);
                File.WriteAllText(file, "x");

                Assert.Equal(info.Length, state.Length);
                state.Reset();
                info.Refresh();
                Assert.Equal(info.Length, state.Length);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void ReadOnlyReset()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                FileInfo info = new FileInfo(file);
                FileState state = new FileState(file);

                Assert.Equal(info.IsReadOnly, state.IsReadOnly);
                info.IsReadOnly = !info.IsReadOnly;
                state.Reset();
                Assert.Equal(true, state.IsReadOnly);
            }
            finally
            {
                (new FileInfo(file)).IsReadOnly = false;
                File.Delete(file);
            }
        }

        [Fact]
        public void ExistsButDirectory()
        {
            Assert.Equal(new FileInfo(Path.GetTempPath()).Exists, new FileState(Path.GetTempPath()).FileExists);
            Assert.Equal(true, (new FileState(Path.GetTempPath()).IsDirectory));
        }

        [Fact]
        public void ReadOnlyOnDirectory()
        {
            Assert.Equal(new FileInfo(Path.GetTempPath()).IsReadOnly, new FileState(Path.GetTempPath()).IsReadOnly);
        }

        [Fact]
        public void LastWriteTimeOnDirectory()
        {
            Assert.Equal(new FileInfo(Path.GetTempPath()).LastWriteTime, new FileState(Path.GetTempPath()).LastWriteTime);
        }

        [Fact]
        public void LastWriteTimeUtcOnDirectory()
        {
            Assert.Equal(new FileInfo(Path.GetTempPath()).LastWriteTimeUtc, new FileState(Path.GetTempPath()).LastWriteTimeUtcFast);
        }

        [Fact]
        public void LengthOnDirectory()
        {
            Helpers.VerifyAssertThrowsSameWay(delegate () { var x = new FileInfo(Path.GetTempPath()).Length; }, delegate () { var x = new FileState(Path.GetTempPath()).Length; });
        }

        [Fact]
        public void DoesNotExistLastWriteTime()
        {
            string file = Guid.NewGuid().ToString("N");

            Assert.Equal(new FileInfo(file).LastWriteTime, new FileState(file).LastWriteTime);
        }

        [Fact]
        public void DoesNotExistLastWriteTimeUtc()
        {
            string file = Guid.NewGuid().ToString("N");

            Assert.Equal(new FileInfo(file).LastWriteTimeUtc, new FileState(file).LastWriteTimeUtcFast);
        }

        [Fact]
        public void DoesNotExistLength()
        {
            string file = Guid.NewGuid().ToString("N"); // presumably doesn't exist

            Helpers.VerifyAssertThrowsSameWay(delegate () { var x = new FileInfo(file).Length; }, delegate () { var x = new FileState(file).Length; });
        }

        [Fact]
        public void DoesNotExistIsDirectory()
        {
            Assert.Throws<FileNotFoundException>(() =>
            {
                string file = Guid.NewGuid().ToString("N"); // presumably doesn't exist

                var x = new FileState(file).IsDirectory;
            }
           );
        }
        [Fact]
        public void DoesNotExistDirectoryOrFileExists()
        {
            string file = Guid.NewGuid().ToString("N"); // presumably doesn't exist

            Assert.Equal(Directory.Exists(file), new FileState(file).DirectoryExists);
        }

        [Fact]
        public void DoesNotExistParentFolderNotFound()
        {
            string file = Guid.NewGuid().ToString("N") + "\\x"; // presumably doesn't exist

            Assert.Equal(false, new FileState(file).FileExists);
            Assert.Equal(false, new FileState(file).DirectoryExists);
        }
    }
}





