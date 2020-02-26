using System;
using System.Collections.Generic;
using System.Linq.Expressions;
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

        public AnalysisScope(AnalysisScope? parent, String name, SymplRuntime? runtime = null, ParameterExpression? runtimeParam = null, ParameterExpression? moduleParam = null)
        {
            Parent = parent;
            this.name = name;
            Runtime = runtime;
            RuntimeExpr = runtimeParam;
            ModuleExpr = moduleParam;
            IsLambda = false;
        }

        public AnalysisScope? Parent { get; }

        public ParameterExpression? ModuleExpr { get; }

        public ParameterExpression? RuntimeExpr { get; }

        /// <devdoc>
        /// Need runtime for interning Symbol constants at code gen time.
        /// </devdoc>
        public SymplRuntime? Runtime { get; }

        public Boolean IsModule => ModuleExpr is { };

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

        public ParameterExpression? GetModuleExpr()
        {
            AnalysisScope? curScope = this;
            while (curScope?.ModuleExpr is null)
            {
                curScope = curScope?.Parent;
            }

            return curScope.ModuleExpr;
        }

        public SymplRuntime GetRuntime()
        {
            AnalysisScope? curScope = this;
            while (curScope?.Runtime is null)
            {
                curScope = curScope?.Parent;
            }

            return curScope.Runtime;
        }
    }
}