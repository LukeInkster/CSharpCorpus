﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>NodePackets which are used for node communication</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using TaskEngineAssemblyResolver = Microsoft.Build.BackEnd.Logging.TaskEngineAssemblyResolver;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// A packet to encapsulate a BuildEventArg logging message.
    /// Contents:
    /// Build Event Type
    /// Build Event Args
    /// </summary>
    internal class LogMessagePacket : LogMessagePacketBase
    {
        /// <summary>
        /// Encapsulates the buildEventArg in this packet.
        /// </summary>
        internal LogMessagePacket(KeyValuePair<int, BuildEventArgs>? nodeBuildEvent)
            : base(nodeBuildEvent, new TargetFinishedTranslator(TranslateTargetFinishedEvent))
        {
        }

        /// <summary>
        /// Constructor for deserialization
        /// </summary>
        private LogMessagePacket(INodePacketTranslator translator)
            : base(translator)
        {
        }

        /// <summary>
        /// Factory for serialization
        /// </summary>
        static internal INodePacket FactoryForDeserialization(INodePacketTranslator translator)
        {
            return new LogMessagePacket(translator);
        }

        /// <summary>
        /// Translate the TargetOutputs for the target finished event.
        /// </summary>
        private static void TranslateTargetFinishedEvent(INodePacketTranslator translator, TargetFinishedEventArgs finishedEvent)
        {
            List<TaskItem> targetOutputs = null;
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                if (finishedEvent.TargetOutputs != null)
                {
                    targetOutputs = new List<TaskItem>();
                    foreach (TaskItem item in finishedEvent.TargetOutputs)
                    {
                        targetOutputs.Add(item);
                    }
                }
            }

            translator.Translate<TaskItem>(ref targetOutputs, TaskItem.FactoryForDeserialization);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                finishedEvent.TargetOutputs = targetOutputs;
            }
        }
    }
}