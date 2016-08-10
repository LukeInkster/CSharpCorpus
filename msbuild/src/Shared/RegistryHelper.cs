﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using Microsoft.Win32;
using System.Resources;
using System.Reflection;
using System.Collections;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Helper methods that simplify registry access.
    /// </summary>
    internal static class RegistryHelper
    {
        /// <summary>
        /// Given a baseKey and a subKey, get all of the subkeys names.
        /// </summary>
        /// <param name="baseKey">The base registry key.</param>
        /// <param name="subKey">The subkey</param>
        /// <returns>An enumeration of strings.</returns>        
        internal static IEnumerable<string> GetSubKeyNames(RegistryKey baseKey, string subkey)
        {
            IEnumerable<string> subKeys = null;

            using (RegistryKey subKey = baseKey.OpenSubKey(subkey))
            {
                if (subKey != null)
                {
                    subKeys = subKey.GetSubKeyNames();
                }
            }

            return subKeys;
        }

        /// <summary>
        /// Given a baseKey and subKey, get the default value of the subKey.
        /// </summary>
        /// <param name="baseKey">The base registry key.</param>
        /// <param name="subKey">The subkey</param>
        /// <returns>A string containing the default value.</returns>
        internal static string GetDefaultValue(RegistryKey baseKey, string subkey)
        {
            string value = null;

            using (RegistryKey key = baseKey.OpenSubKey(subkey))
            {
                if (key != null && key.ValueCount > 0)
                {
                    value = (string)key.GetValue("");
                }
            }

            return value;
        }

        /// <summary>
        /// Given a hive and a hive view open the base key
        ///      RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
        /// </summary>
        /// <param name="baseKey">The base registry key.</param>
        /// <param name="view">The hive view</param>
        /// <returns>A registry Key for the given baseKey and view</returns>
        internal static RegistryKey OpenBaseKey(RegistryHive hive, RegistryView view)
        {
            RegistryKey key = RegistryKey.OpenBaseKey(hive, view);
            return key;
        }
    }
}
