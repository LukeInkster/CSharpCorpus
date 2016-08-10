﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using JetBrains.Annotations;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Migrations.Design;

namespace Microsoft.EntityFrameworkCore.Tools.Cli
{
    public class MigrationsAddCommand
    {
        public static void Configure([NotNull] CommandLineApplication command, [NotNull] CommonCommandOptions commonOptions)
        {
            command.Description = "Add a new migration";

            var name = command.Argument("[name]", "The name of the migration");

            var outputDir = command.Option(
                "-o|--output-dir <path>",
                "The directory (and sub-namespace) to use. If omitted, \"Migrations\" is used. Relative paths are relative the directory in which the command is executed.");
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
                () =>
                {
                    if (string.IsNullOrEmpty(name.Value))
                    {
                        ConsoleCommandLogger.Error(("Missing required argument '" + name.Name + "'").Bold().Red());
                        command.ShowHelp();

                        return 1;
                    }

                    return Execute(commonOptions.Value(),
                        name.Value,
                        outputDir.Value(),
                        context.Value(),
                        environment.Value(),
                        json.HasValue()
                            ? (Action<MigrationFiles>)ReportJson
                            : null);
                });
        }

        private static int Execute(CommonOptions commonOptions,
            string name,
            string outputDir,
            string context,
            string environment,
            Action<MigrationFiles> reporter)
        {
            var files = new OperationExecutor(commonOptions, environment)
                .AddMigration(name, outputDir, context);

            reporter?.Invoke(files);

            ConsoleCommandLogger.Output("Done. To undo this action, use 'dotnet ef migrations remove'");

            return 0;
        }

        private static void ReportJson(MigrationFiles files)
        {
            var sb = new StringBuilder();
            sb.AppendLine("//BEGIN");
            sb.AppendLine("{");
            sb.AppendLine("  \"MigrationFile\": \""+ SerializePath(files.MigrationFile) +"\",");
            sb.AppendLine("  \"MetadataFile\": \"" + SerializePath(files.MetadataFile) + "\",");
            sb.AppendLine("  \"SnapshotFile\": \"" + SerializePath(files.SnapshotFile) + "\"");
            sb.AppendLine("}");
            sb.AppendLine("//END");
            ConsoleCommandLogger.Output(sb.ToString());
        }


        private static string SerializePath(string path)
            => path?.Replace("\\", "\\\\");
    }
}
