using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Sympl.Hosting;
using Sympl.Runtime;

namespace Sympl.Analysis
{
    /// <summary>
    /// Holds identifier information so that we can do name binding during analysis.
    /// </summary>
    /// <devdoc>
    /// It manages a map from names to <see cref="ParameterExpression"/> so ET definition locations
    /// and reference locations can alias the same variable.
    ///
    /// These chain from inner most BlockExpressions, through LambdaExpressions, to the root which models a file
    /// or top-level expression. The root has non-None ModuleExpr and RuntimeExpr, which are
    /// <see cref="ParameterExpression"/>s.
    /// </devdoc>
    class AnalysisScope
    {
        readonly String name;

        public AnalysisScope(AnalysisScope? parent, String name, SymplContext? context = null, ParameterExpression? runtimeParam = null, ParameterExpression? moduleParam = null)
        {
            Parent = parent;
            this.name = name;
            Context = context;
            
            Runtime = runtimeParam;
            Module = moduleParam;   
            IsLambda = false;
        }

        public AnalysisScope? Parent { get; }

        public ParameterExpression? Module { get; }

        public ParameterExpression? Runtime { get; }

        /// <devdoc>
        /// Need runtime for interning Symbol constants at code gen time.
        /// </devdoc>
        public SymplContext? Context { get; }

        public Boolean IsModule => Module is { };

        /// <devdoc>
        /// Need IsLambda when support return to find tightest closing fun.
        /// </devdoc>
        public Boolean IsLambda { get; set; }

        public Boolean IsLoop { get; set; }

        public LabelTarget? LoopBreak { get; set; }

        /*
        public LabelTarget LoopContinue
        {
            get => LoopBreak;
            set => LoopBreak = value;
        }
        */

        public Dictionary<String, ParameterExpression> Names { get; set; } =
            new Dictionary<String, ParameterExpression>(StringComparer.OrdinalIgnoreCase);

        public ParameterExpression? GetModuleExpression()
        {
            AnalysisScope? curScope = this;
            while (curScope?.Module is null)
            {
                curScope = curScope?.Parent;
            }

            return curScope.Module;
        }

        public SymplContext GetContext()
        {
            AnalysisScope? curScope = this;
            while (curScope?.Context is null)
            {
                curScope = curScope?.Parent;
            }

            return curScope.Context;
        }
    }
}