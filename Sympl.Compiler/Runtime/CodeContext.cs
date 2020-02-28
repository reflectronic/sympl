using System;
using System.Collections.Concurrent;
using System.Dynamic;
using Microsoft.Scripting.Runtime;
using Sympl.Hosting;

namespace Sympl.Runtime
{
    public sealed class CodeContext
    {
        readonly ExpandoObject globals = new ExpandoObject();
        readonly Scope dlrGlobals;

        public CodeContext(Scope dlrGlobals, SymplContext languageContext)
        {
            this.dlrGlobals = dlrGlobals;
            LanguageContext = languageContext;
        }

        public ConcurrentDictionary<String, Symbol> Symbols { get; }  = new ConcurrentDictionary<String, Symbol>(StringComparer.OrdinalIgnoreCase);
        public SymplContext LanguageContext { get; }
        public IDynamicMetaObjectProvider Globals => globals;
        public IDynamicMetaObjectProvider DlrGlobals => dlrGlobals;

    }
}
