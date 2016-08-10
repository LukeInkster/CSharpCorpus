﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.EntityFrameworkCore.Tools.Cli
{
    public class DatabaseUpdateCommand
    {
        public static void Configure([NotNull] CommandLineApplication command, [NotNull] CommonCommandOptions commonOptions)
        {
            command.Description = "Updates the database to a specified migration";

            var migration = command.Argument(
                "[migration]",
                "The target migration. If '0', all migrations will be reverted. If omitted, all pending migrations will be applied");

            var context = command.Option(
                "-c|--context <context>",
                "The DbContext to use. If omitted, the default DbContext is used");
            var environment = command.Option(
                "-e|--environment <environment>",
                "The environment to use. If omitted, \"Development\" is used.");
            command.HelpOption();
            command.VerboseOption();

            command.OnExecute(
                () => Execute(commonOptions.Value(), 
                    migration.Value, 
                    context.Value(),
                    environment.Value()));
        }

        private static int Execute(CommonOptions commonOptions,
            string migration, 
            string context, 
            string environment)
        {
            new OperationExecutor(commonOptions, environment)
                 .UpdateDatabase(migration, context);

            return 0;
        }
    }
}
