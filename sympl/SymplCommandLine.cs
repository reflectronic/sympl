﻿using System;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting.Shell;

namespace Sympl
{
    sealed class SymplCommandLine : CommandLine
    {
        protected override ICommandDispatcher CreateCommandDispatcher() => new SymplCommandExecutor(Console);

        protected override ErrorSink ErrorSink => new IConsoleErrorSink(Console);

        protected override String Logo => $"Type \"exit\" to exit.{Environment.NewLine}";

        protected override void ExecuteCommand(String command) => ExecuteCommand(Engine.CreateScriptSourceFromString(command.Trim(), SourceCodeKind.InteractiveCode));

        // TODO: Autoindent
    }
}