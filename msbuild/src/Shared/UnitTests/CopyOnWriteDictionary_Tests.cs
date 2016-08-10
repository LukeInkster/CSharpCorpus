﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for the copy on write dictionary class</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Construction;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Collections
{
    /// <summary>
    /// Tests for the CopyOnWriteDictionary 
    /// </summary>
    public class CopyOnWriteDictionary_Tests
    {
        /// <summary>
        /// Find with the same key inserted using the indexer
        /// </summary>
        [Fact]
        public void Indexer_ReferenceFound()
        {
            object k1 = new Object();
            object v1 = new Object();

            var dictionary = new CopyOnWriteDictionary<object, object>();
            dictionary[k1] = v1;

            // Now look for the same key we inserted
            object v2 = dictionary[k1];

            Assert.Equal(true, Object.ReferenceEquals(v1, v2));
            Assert.Equal(true, dictionary.ContainsKey(k1));
        }

        /// <summary>
        /// Find something not present with the indexer
        /// </summary>
        [Fact]
        public void Indexer_NotFound()
        {
            Assert.Throws<KeyNotFoundException>(() =>
            {
                var dictionary = new CopyOnWriteDictionary<object, object>();
                object value = dictionary[new Object()];
            }
           );
        }
        /// <summary>
        /// Find with the same key inserted using TryGetValue
        /// </summary>
        [Fact]
        public void TryGetValue_ReferenceFound()
        {
            object k1 = new Object();
            object v1 = new Object();

            var dictionary = new CopyOnWriteDictionary<object, object>();
            dictionary[k1] = v1;

            // Now look for the same key we inserted
            object v2;
            bool result = dictionary.TryGetValue(k1, out v2);

            Assert.Equal(true, result);
            Assert.Equal(true, Object.ReferenceEquals(v1, v2));
        }

        /// <summary>
        /// Find something not present with TryGetValue
        /// </summary>
        [Fact]
        public void TryGetValue_ReferenceNotFound()
        {
            var dictionary = new CopyOnWriteDictionary<object, object>();

            object v;
            bool result = dictionary.TryGetValue(new Object(), out v);

            Assert.Equal(false, result);
            Assert.Equal(null, v);
            Assert.Equal(false, dictionary.ContainsKey(new Object()));
        }

        /// <summary>
        /// Find a key that wasn't inserted but is equal
        /// </summary>
        [Fact]
        public void EqualityComparer()
        {
            string k1 = String.Concat("ke", "y");
            object v1 = new Object();

            var dictionary = new CopyOnWriteDictionary<string, object>();
            dictionary[k1] = v1;

            // Now look for a different but equatable key
            // Don't create it with a literal or the compiler will intern it!
            string k2 = String.Concat("k", "ey");

            Assert.Equal(false, Object.ReferenceEquals(k1, k2));

            object v2 = dictionary[k2];

            Assert.Equal(true, Object.ReferenceEquals(v1, v2));
        }

        /// <summary>
        /// Cloning sees the same values 
        /// </summary>
        [Fact]
        public void CloneVisibility()
        {
            var dictionary = new CopyOnWriteDictionary<string, string>();
            dictionary["test"] = "1";
            Assert.Equal(dictionary["test"], "1");

            var clone = dictionary.Clone();

            Assert.Equal(clone["test"], "1");
            Assert.Equal(clone.Count, dictionary.Count);
        }

        /// <summary>
        /// Clone uses same comparer 
        /// </summary>
        [Fact]
        public void CloneComparer()
        {
            var dictionary = new CopyOnWriteDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            dictionary["test"] = "1";
            Assert.Equal(dictionary["test"], "1");

            var clone = dictionary.Clone();

            Assert.Equal(clone["TEST"], "1");
        }

        /// <summary>
        /// Writes to original not visible to clone
        /// </summary>
        [Fact]
        public void OriginalWritesNotVisibleToClones()
        {
            var dictionary = new CopyOnWriteDictionary<string, string>();
            dictionary["test"] = "1";
            Assert.Equal(dictionary["test"], "1");

            var clone = dictionary.Clone();
            var clone2 = dictionary.Clone();

            Assert.True(dictionary.HasSameBacking(clone));
            Assert.True(dictionary.HasSameBacking(clone2));

            dictionary["test"] = "2";

            Assert.False(dictionary.HasSameBacking(clone));
            Assert.False(dictionary.HasSameBacking(clone2));
            Assert.True(clone.HasSameBacking(clone2));

            Assert.Equal(clone["test"], "1");
            Assert.Equal(clone2["test"], "1");
        }

        /// <summary>
        /// Writes to clone not visible to original
        /// </summary>
        [Fact]
        public void CloneWritesNotVisibleToOriginal()
        {
            var dictionary = new CopyOnWriteDictionary<string, string>();
            dictionary["test"] = "1";
            Assert.Equal(dictionary["test"], "1");

            var clone = dictionary.Clone();
            var clone2 = dictionary.Clone();

            Assert.True(dictionary.HasSameBacking(clone));
            Assert.True(dictionary.HasSameBacking(clone2));

            clone["test"] = "2";
            Assert.False(dictionary.HasSameBacking(clone));
            Assert.False(clone2.HasSameBacking(clone));
            Assert.True(dictionary.HasSameBacking(clone2));

            clone2["test"] = "3";
            Assert.False(dictionary.HasSameBacking(clone2));

            Assert.Equal(dictionary["test"], "1");
            Assert.Equal(clone["test"], "2");
        }

        /// <summary>
        /// Serialize basic case
        /// </summary>
        [Fact]
        public void SerializeDeserialize()
        {
            CopyOnWriteDictionary<int, string> dictionary = new CopyOnWriteDictionary<int, string>();
            dictionary.Add(1, "1");

            using (MemoryStream stream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();

                formatter.Serialize(stream, dictionary);
                stream.Position = 0;

                var dictionary2 = (CopyOnWriteDictionary<int, string>)formatter.Deserialize(stream);

                Assert.Equal(dictionary.Count, dictionary2.Count);
                Assert.Equal(dictionary.Comparer, dictionary2.Comparer);
                Assert.Equal("1", dictionary2[1]);

                dictionary2.Add(2, "2");
            }
        }

        /// <summary>
        /// Serialize custom comparer
        /// </summary>
        [Fact]
        public void SerializeDeserialize2()
        {
            CopyOnWriteDictionary<string, string> dictionary = new CopyOnWriteDictionary<string, string>(MSBuildNameIgnoreCaseComparer.Default);

            using (MemoryStream stream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();

                formatter.Serialize(stream, dictionary);
                stream.Position = 0;

                CopyOnWriteDictionary<string, string> dictionary2 = (CopyOnWriteDictionary<string, string>)formatter.Deserialize(stream);

                Assert.Equal(dictionary.Count, dictionary2.Count);
                Assert.Equal(typeof(MSBuildNameIgnoreCaseComparer), dictionary2.Comparer.GetType());
            }
        }
    }
}
