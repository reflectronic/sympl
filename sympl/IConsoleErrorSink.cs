using System;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting.Shell;
using Microsoft.Scripting.Utils;

namespace Sympl
{
    sealed class IConsoleErrorSink : ErrorSink
    {
        public IConsoleErrorSink(IConsole console)
        {
            Console = console;
        }

        public IConsole Console { get; }

        public override void Add(SourceUnit source, String message, SourceSpan span, Int32 errorCode, Severity severity)
        {
            Add(message, source.Path, source.GetCode(), source.GetCodeLine(span.Start.Line), span, errorCode, severity);
        }

        public override void Add(String message, String path, String code, String line, SourceSpan span, Int32 errorCode, Severity severity)
        {
            if (severity is Severity.Ignore)
                return;

            var kind = severity switch
            {
                Severity.Warning => "warning",
                Severity.Error => "error",
                Severity.FatalError => "error",
                _ => throw Assert.Unreachable,
            };

            // TODO: Multi-line support

            var str = @$"{kind} {errorCode:0000}: {message}";
            if (!String.IsNullOrWhiteSpace(line) && span.IsValid && span.Length != 0)
            {
                str += $"{Environment.NewLine}{line}{Environment.NewLine}{new String('~', span.Length).PadLeft(Math.Max(0, span.End.Column - 1))}";
            }

            Console.WriteLine(str, severity switch
            {
                Severity.Warning => Style.Warning,
                Severity.Error => Style.Error,
                Severity.FatalError => Style.Error,
                _ => Style.Out
            });
        }
    }
}