﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.EntityFrameworkCore.Tools.Cli
{
    public class MigrationsListCommand
    {
        public static void Configure([NotNull] CommandLineApplication command, [NotNull] CommonCommandOptions commonOptions)
        {
            command.Description = "List the migrations";

            var context = command.Option(
                "-c|--context <context>",
                "The DbContext to use. If omitted, the default DbContext is used");
            var environment = command.Option(
                "-e|--environment <environment>",
                "The environment to use. If omitted, \"Development\" is used.");
            var json = command.JsonOption();

            command.HelpOption();
            command.VerboseOption();

            command.OnExecute(
                () => Execute(commonOptions.Value(),
                    context.Value(),
                    environment.Value(),
                    json.HasValue()
                        ? (Action<IEnumerable<MigrationInfo>>)ReportJsonResults
                        : ReportResults));
        }

        private static int Execute(CommonOptions commonOptions,
            string context,
            string environment,
            Action<IEnumerable<MigrationInfo>> reportResultsAction)
        {
            var migrations = new OperationExecutor(commonOptions, environment)
                .GetMigrations(context);

            reportResultsAction(migrations);

            return 0;
        }

        private static void ReportJsonResults(IEnumerable<MigrationInfo> migrations)
        {
            var nameGroups = migrations.GroupBy(m => m.Name).ToList();
            var output = new StringBuilder();

            output.AppendLine("//BEGIN");
            output.Append("[");

            var first = true;
            foreach (var m in migrations)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    output.Append(",");
                }

                var safeName = nameGroups.Count(g => g.Key == m.Name) == 1
                    ? m.Name
                    : m.Id;

                output.AppendLine();
                output.AppendLine("  {");
                output.AppendLine("    \"id\": \"" + m.Id + "\",");
                output.AppendLine("    \"name\": \"" + m.Name + "\",");
                output.AppendLine("    \"safeName\": \"" + safeName + "\"");
                output.Append("  }");
            }

            output.AppendLine();
            output.AppendLine("]");
            output.AppendLine("//END");

            ConsoleCommandLogger.Output(output.ToString());
        }

        private static void ReportResults(IEnumerable<MigrationInfo> migrations)
        {
            var any = false;
            foreach (var migration in migrations)
            {
                ConsoleCommandLogger.Output(migration.Id as string);
                any = true;
            }

            if (!any)
            {
                ConsoleCommandLogger.Error("No migrations were found");
            }
        }
    }
}
