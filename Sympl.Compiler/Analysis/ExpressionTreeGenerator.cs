using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using Sympl.Expressions;
using Sympl.Runtime;
using Sympl.Syntax;

namespace Sympl.Analysis
{
    internal static class ExpressionTreeGenerator
    {
        public static Expression AnalyzeExpression(SymplExpression expression, AnalysisScope scope) => expression switch
        {
            SymplImport import => AnalyzeImport(import, scope),
            SymplCall call => AnalyzeCall(call, scope),
            SymplDefun defun => AnalyzeDefun(defun, scope),
            SymplLambda lambda => AnalyzeLambdaExpr(lambda, scope),
            SymplIdentifier identifier => AnalyzeIdentifier(identifier, scope),
            SymplQuote quote => AnalyzeQuote(quote, scope),
            SymplLiteral literal => Expression.Constant(literal.Value),
            SymplAssignment assignment => AnalyzeAssignment(assignment, scope),
            SymplLetStar star => AnalyzeLetStar(star, scope),
            SymplBlock block => AnalyzeBlock(block, scope),
            SymplEq eq => AnalyzeEq(eq, scope),
            SymplCons cons => AnalyzeCons(cons, scope),
            SymplListCall call => AnalyzeListCall(call, scope),
            SymplIf @if => AnalyzeIf(@if, scope),
            SymplDot dot => AnalyzeDot(dot, scope),
            SymplNew @new => AnalyzeNew(@new, scope),
            SymplLoop loop => AnalyzeLoop(loop, scope),
            SymplBreak @break => AnalyzeBreak(@break, scope),
            SymplElt elt => AnalyzeElt(elt, scope),
            SymplBinary binary => AnalyzeBinary(binary, scope),
            SymplUnary unary => AnalyzeUnary(unary, scope),
            _ => throw new InvalidOperationException("Internal: no expression to analyze.")
        };

        public static Expression AnalyzeImport(SymplImport expression, AnalysisScope scope)
        {
            if (!scope.IsModule)
            {
                throw new InvalidOperationException("Import expression must be a top level expression.");
            }

            return Expression.Call(typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.Import)), scope.RuntimeExpr,
                scope.ModuleExpr, Expression.Constant(Array.ConvertAll(expression.NamespaceExpr, id => id.Name)),
                Expression.Constant(Array.ConvertAll(expression.MemberNames, id => id.Name)),
                Expression.Constant(Array.ConvertAll(expression.Renames, id => id.Name)));
        }

        public static DynamicExpression AnalyzeDefun(SymplDefun expression, AnalysisScope scope)
        {
            if (!scope.IsModule)
            {
                throw new InvalidOperationException("Use Defmethod or Lambda when not defining top-level function.");
            }

            return Expression.Dynamic(scope.GetRuntime().GetSetMemberBinder(expression.Name), typeof(Object),
                scope.ModuleExpr,
                AnalyzeLambdaDef(expression.Params, expression.Body, scope, $"defun {expression.Name}"));
        }

        public static LambdaExpression AnalyzeLambdaExpr(SymplLambda expression, AnalysisScope scope) =>
            AnalyzeLambdaDef(expression.Params, expression.Body, scope, "lambda");

        static LambdaExpression AnalyzeLambdaDef(IdOrKeywordToken[] parameters, SymplExpression[] body, AnalysisScope scope, String description)
        {
            var funScope = new AnalysisScope(scope, description)
            {
                IsLambda = true // needed for return support.
            };

            var paramsInOrder = new List<ParameterExpression>();
            foreach (var p in parameters)
            {
                var pe = Expression.Parameter(typeof(Object), p.Name);
                paramsInOrder.Add(pe);
                funScope.Names[p.Name] = pe;
            }

            // No need to add fun name to module scope since recursive call just looks up global name
            // late bound. For lambdas, to get the effect of flet to support recursion, bind a
            // variable to nil and then set it to a lambda. Then the lambda's body can refer to the
            // let bound var in its def.
            var bodyExpressions = Array.ConvertAll(body, e => AnalyzeExpression(e, funScope));

            // Set up the Type arg array for the delegate type. Must include the return type as the
            // last Type, which is object for Sympl defs.
            var arr = new Type[parameters.Length + 1];
            arr.AsSpan().Fill(typeof(Object));
            return Expression.Lambda(Expression.GetFuncType(arr), Expression.Block(bodyExpressions), paramsInOrder);
        }

        static Expression[] Analyze(SymplExpression[] expressions, AnalysisScope scope, Expression? firstArg = null, Expression? lastArg = null)
        {
            var firstArgOffset = firstArg is { } ? 1 : 0;
            var secondArgOffset = lastArg is { } ? 1 : 0;
            Expression[] args = new Expression[expressions.Length + firstArgOffset + secondArgOffset];

            if (firstArg is { }) args[0] = firstArg;

            for (var i = 0; i < expressions.Length; i++)
            {
                args[i + firstArgOffset] = AnalyzeExpression(expressions[i], scope);
            }

            if (secondArgOffset != 0)
                args[^1] = lastArg!;

            return args;
        }


        /// <summary>
        /// Returns a dynamic InvokeMember or Invoke expression, depending on the Function expression.
        /// </summary>
        public static DynamicExpression AnalyzeCall(SymplCall expression, AnalysisScope scope) =>
            expression.Function is SymplDot dottedExpr
                // last expression must be an id
                ? Expression.Dynamic(
                    scope.GetRuntime().GetInvokeMemberBinder(
                        new InvokeMemberBinderKey(
                            ((SymplIdentifier) dottedExpr.Expressions[^1]).IdToken.Name,
                            new CallInfo(expression.Arguments.Length))),
                        typeof(Object),
                        Analyze(
                            expression.Arguments,
                            scope,
                            dottedExpr.Expressions.Length > 1
                                // create a new dot expression for the object that doesn't include the last part
                                ? AnalyzeDot(new SymplDot(dottedExpr.ObjectExpr, RuntimeHelpers.RemoveLast(dottedExpr.Expressions)), scope)
                                : AnalyzeExpression(dottedExpr.ObjectExpr, scope)))
                // Use DynamicExpression so that I don't always have to have a delegate to call, such as what
                // happens with IronPython interop.
                : Expression.Dynamic(
                    scope.GetRuntime().GetInvokeBinder(new CallInfo(expression.Arguments.Length)),
                    typeof(Object),
                    Analyze(
                        expression.Arguments,
                        scope,
                        AnalyzeExpression(expression.Function, scope)));

        /// <summary>
        /// Returns a chain of GetMember and InvokeMember dynamic expressions for the dotted expression.
        /// </summary>
        public static Expression AnalyzeDot(SymplDot expression, AnalysisScope scope)
        {
            var curExpr = AnalyzeExpression(expression.ObjectExpr, scope);
            foreach (var e in expression.Expressions)
            {
                curExpr = e switch
                {
                    SymplIdentifier identifier => Expression.Dynamic(scope.GetRuntime().GetGetMemberBinder(identifier.IdToken.Name), typeof(Object), curExpr),
                    SymplCall call => Expression.Dynamic(
                        // Dotted expressions must be simple invoke members, a.b.(c ...)
                        scope.GetRuntime().GetInvokeMemberBinder(
                            new InvokeMemberBinderKey(
                                ((SymplIdentifier) call.Function).IdToken.Name,
                                new CallInfo(call.Arguments.Length))),
                        typeof(Object),
                        Analyze(call.Arguments, scope, curExpr)),
                    _ => throw new InvalidOperationException("Internal: dotted must be IDs or Funs."),
                };
            }

            return curExpr;
        }

        /// <summary>
        /// Handles IDs, indexing, and member sets. IDs are either lexical or dynamic expressions on the
        /// module scope. Everything else is dynamic.
        /// </summary>
        public static Expression AnalyzeAssignment(SymplAssignment expression, AnalysisScope scope)
        {
            switch (expression.Location)
            {
                case SymplIdentifier idExpr:
                    var lhs = AnalyzeExpression(expression.Location, scope);
                    var val = AnalyzeExpression(expression.Value, scope);
                    var param = FindIdDef(idExpr.IdToken.Name, scope);
                    if (param is { })
                    {
                        // Assign returns value stored.
                        return Expression.Assign(lhs, Expression.Convert(val, param.Type));
                    }
                    else
                    {
                        var tmp = Expression.Parameter(typeof(Object), "assignTmpForRes");
                        // Ensure stored value is returned. Had some erroneous MOs come through here and
                        // left the code for example.
                        return Expression.Block(new[] { tmp },
                            Expression.Assign(tmp, Expression.Convert(val, typeof(Object))),
                            Expression.Dynamic(scope.GetRuntime().GetSetMemberBinder(idExpr.IdToken.Name), typeof(Object),
                                scope.GetModuleExpr(), tmp), tmp);
                    }
                case SymplElt eltExpr:
                    // Trusting MO convention to return stored values.
                    return Expression.Dynamic(
                        scope.GetRuntime().GetSetIndexBinder(new CallInfo(eltExpr.Indexes.Length)),
                        typeof(Object),
                        Analyze(
                            eltExpr.Indexes,
                            scope,
                            AnalyzeExpression(eltExpr.ObjectExpr, scope),
                            AnalyzeExpression(expression.Value, scope))
                        );

                // For now, one dot only. Later, pick last dotted member access (like
                // AnalyzeFunctionCall), and use a
                // temp and block.
                case SymplDot dottedExpr when dottedExpr.Expressions.Length > 1:
                    throw new InvalidOperationException(
                        "Don't support assigning with more than simple dotted expression, o.foo.");
                case SymplDot dottedExpr when !(dottedExpr.Expressions[0] is SymplIdentifier):
                    throw new InvalidOperationException(
                        "Only support un-indexed field or property when assigning dotted expression location.");
                case SymplDot dottedExpr:
                    // Trusting MOs convention to return stored values.
                    return Expression.Dynamic(
                        scope.GetRuntime().GetSetMemberBinder(((SymplIdentifier) dottedExpr.Expressions[0]).IdToken.Name),
                        typeof(Object),
                        AnalyzeExpression(dottedExpr.ObjectExpr, scope),
                        AnalyzeExpression(expression.Value, scope));
                default:
                    throw new InvalidOperationException("Invalid left hand side type.");
            }
        }

        /// <summary>
        /// Return an Expression for referencing the ID. If we find the name in the scope chain, then
        /// we just return the stored ParamExpr. Otherwise, the reference is a dynamic member lookup
        /// on the root scope, a module object.
        /// </summary>
        public static Expression AnalyzeIdentifier(SymplIdentifier expression, AnalysisScope scope)
        {
            if (expression.IdToken is KeywordToken token)
            {
                return token.Kind switch
                {
                    KeywordTokenKind.Nil => Expression.Constant(null, typeof(Object)),
                    KeywordTokenKind.True => Expression.Constant(true),
                    KeywordTokenKind.False => Expression.Constant(true),
                    _ => throw new InvalidOperationException("Internal: unrecognized keyword literal constant."),
                };
            }
            else
            {
                var param = FindIdDef(expression.IdToken.Name, scope);

                return param ?? Expression.Dynamic(scope.GetRuntime().GetGetMemberBinder(expression.IdToken.Name), typeof(Object), scope.GetModuleExpr());
            }
        }

        /// <summary>
        /// Returns the ParameterExpr for the name by searching the scopes, or it returns None.
        /// </summary>
        static Expression? FindIdDef(String name, AnalysisScope scope)
        {
            AnalysisScope? currentScope = scope;
            while (currentScope is { } && !currentScope.IsModule)
            {
                if (currentScope.Names.TryGetValue(name, out var res))
                    return res;

                currentScope = currentScope.Parent;
            }

            if (scope is null)
                throw new InvalidOperationException("Got bad AnalysisScope chain with no module at end.");

            return null;
        }

        /// <summary>
        /// Returns a Block with vars, each initialized in the order they appear. Each var's init
        /// expression can refer to vars initialized before it. The Block's body is the Let*'s body.
        /// </summary>
        public static Expression AnalyzeLetStar(SymplLetStar expression, AnalysisScope scope)
        {
            var letscope = new AnalysisScope(scope, "let*");
            // Analyze bindings.
            var bindings = expression.Bindings;
            var inits = new Expression[bindings.Length + expression.Body.Length];
            var varsInOrder = new ParameterExpression[bindings.Length];
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                // Need richer logic for mvbind
                var v = Expression.Parameter(typeof(Object), binding.Variable.Name);
                varsInOrder[i] = v;
                inits[i] = Expression.Assign(v, Expression.Convert(AnalyzeExpression(binding.Value, letscope), v.Type));
                // Add var to scope after analyzing init value so that init value references to the
                // same ID do not bind to his uninitialized var.
                letscope.Names[binding.Variable.Name] = v;
            }

            var body = Array.ConvertAll(expression.Body, e => AnalyzeExpression(e, letscope));

            // Order of vars to BlockExpr don't matter semantically, but may as well keep them in the
            // order the programmer specified in case they look at the expression Trees in the debugger or
            // for meta-programming.
            body.CopyTo(inits.AsSpan(bindings.Length));
            return Expression.Block(typeof(Object), varsInOrder, inits);
        }

        /// <summary>
        /// Returns a Block with the body expressions.
        /// </summary>
        public static Expression AnalyzeBlock(SymplBlock expression, AnalysisScope scope) => Expression.Block(
            typeof(Object),
            Array.ConvertAll(expression.Body, e => AnalyzeExpression(e, scope)));

        /// <summary>
        /// Converts a list, literal, or id expression to a runtime quoted literal and returns the Constant
        /// expression for it.
        /// </summary>
        public static Expression AnalyzeQuote(SymplQuote expression, AnalysisScope scope) =>
            Expression.Constant(MakeQuoteConstant(expression.Expression, scope.GetRuntime()));

        static Object? MakeQuoteConstant(Object expression, SymplRuntime runtime) => expression switch
        {
            SymplList list => Cons._List(Array.ConvertAll(list.Elements, e => MakeQuoteConstant(e, runtime))),
            IdOrKeywordToken token => runtime.MakeSymbol(token.Name),
            LiteralToken token => token.Value,
            _ => throw new InvalidOperationException($"Internal: quoted list has -- {expression}"),
        };

        public static Expression AnalyzeEq(SymplEq expression, AnalysisScope scope) => Expression.Call(WellKnownSymbols.Eq,
                Expression.Convert(AnalyzeExpression(expression.Left, scope), typeof(Object)),
                Expression.Convert(AnalyzeExpression(expression.Right, scope), typeof(Object)));

        public static Expression AnalyzeCons(SymplCons expression, AnalysisScope scope) => Expression.Call(WellKnownSymbols.MakeCons,
                Expression.Convert(AnalyzeExpression(expression.Left, scope), typeof(Object)),
                Expression.Convert(AnalyzeExpression(expression.Right, scope), typeof(Object)));

        public static Expression AnalyzeListCall(SymplListCall expression, AnalysisScope scope) => Expression.Call(
            WellKnownSymbols._List,
            Expression.NewArrayInit(typeof(Object),
                Array.ConvertAll(expression.Elements, e => Expression.Convert(AnalyzeExpression(e, scope), typeof(Object)))));

        public static Expression AnalyzeIf(SymplIf expression, AnalysisScope scope) => Expression.Condition(WrapBooleanTest(AnalyzeExpression(expression.Test, scope)),
                Expression.Convert(AnalyzeExpression(expression.Consequent, scope), typeof(Object)),
                Expression.Convert(expression.Alternative is { } ? AnalyzeExpression(expression.Alternative, scope) : Expression.Constant(false), typeof(Object)));

        static Expression WrapBooleanTest(Expression expression)
        {
            var tmp = Expression.Parameter(typeof(Object), "testtmp");
            return Expression.Block(new[] { tmp },
                Expression.Assign(tmp, Expression.Convert(expression, typeof(Object))), Expression.Condition(
                    Expression.TypeIs(tmp, typeof(Boolean)),
                    Expression.Convert(tmp, typeof(Boolean)),
                    Expression.NotEqual(tmp, Expression.Constant(null, typeof(Object)))));
        }

        public static Expression AnalyzeLoop(SymplLoop expression, AnalysisScope scope)
        {
            var loopScope = new AnalysisScope(scope, "loop ")
            {
                IsLoop = true, // needed for break and continue
                LoopBreak = Expression.Label(typeof(Object), "loop break")
            };

            return Expression.Loop(
                Expression.Block(typeof(Object),
                Analyze(expression.Body, loopScope)),
                loopScope.LoopBreak);
        }

        public static Expression AnalyzeBreak(SymplBreak expression, AnalysisScope scope)
        {
            var loopScope = FindFirstLoop(scope);
            if (loopScope is null)
                throw new InvalidOperationException("Call to Break not inside loop.");

            var value = expression.Value is null
                ? Expression.Constant(null, typeof(Object))
                : AnalyzeExpression(expression.Value, loopScope);

            // Need final type=object arg because the Goto is in a value returning position, and the
            // Break factory doesn't set the GotoExpression.Type property to the type of the LoopBreak
            // label target's type. For example, removing this would cause the Convert to object for
            // an If branch to throw because the Goto is void without this last argument.
            return Expression.Break(loopScope.LoopBreak, value, typeof(Object));
        }

        /// <summary>
        /// Returns the first loop AnalysisScope, or None.
        /// </summary>
        static AnalysisScope? FindFirstLoop(AnalysisScope scope)
        {
            AnalysisScope? currentScope;
            for (currentScope = scope;
                currentScope is AnalysisScope { IsLoop: false };
                currentScope = currentScope.Parent) { }

            return currentScope;
        }

        public static Expression AnalyzeNew(SymplNew expression, AnalysisScope scope) => Expression.Dynamic(
            scope.GetRuntime().GetCreateInstanceBinder(new CallInfo(expression.Arguments.Length)),
            typeof(Object),
            Analyze(expression.Arguments, scope, AnalyzeExpression(expression.Type, scope)));

        public static Expression AnalyzeBinary(SymplBinary expression, AnalysisScope scope)
        {
            switch (expression.Operation)
            {
                // The language has the following special logic to handle And and Or x And y == if x then
                // y x Or y == if x then x else (if y then y)
                case ExpressionType.And:
                    return AnalyzeIf(new SymplIf(expression.Left, expression.Right, null), scope);
                case ExpressionType.Or:
                    // Use (LetStar (tmp expression) (if tmp tmp)) to represent (if expression expression) to remove
                    // duplicate evaluation. So x Or y is translated into (Let* (tmp1 x) (If tmp1 tmp1
                    // (Let* (tmp2 y) (If tmp2 tmp2))))

                    // TODO: Figure out location of synthesized tokens

                    var tmp2 = new IdOrKeywordToken(
                        // Real implementation needs to ensure unique ID in scope chain.
                        "__tmpLetVariable2", default);
                    var tmpExpr2 = new SymplIdentifier(tmp2);
                    var binding2 = new SymplLetStar.LetBinding(tmp2, expression.Right);
                    SymplExpression ifExpr2 = new SymplIf(tmpExpr2, tmpExpr2, null);
                    var letExpr2 = new SymplLetStar(new[] { binding2 }, new[] { ifExpr2 });

                    var tmp1 = new IdOrKeywordToken(
                        // Real implementation needs to ensure unique ID in scope chain.
                        "__tmpLetVariable1", default);
                    var tmpExpr1 = new SymplIdentifier(tmp1);
                    var binding1 = new SymplLetStar.LetBinding(tmp1, expression.Left);
                    SymplExpression ifExpr1 = new SymplIf(tmpExpr1, tmpExpr1, letExpr2);
                    return AnalyzeLetStar(new SymplLetStar(new[] { binding1 }, new[] { ifExpr1 }), scope);
                default:
                    return Expression.Dynamic(
                        scope.GetRuntime().GetBinaryOperationBinder(expression.Operation),
                        typeof(Object),
                        AnalyzeExpression(expression.Left, scope),
                        AnalyzeExpression(expression.Right, scope));
            }
        }

        public static Expression AnalyzeUnary(SymplUnary expression, AnalysisScope scope)
        {
            if (expression.Operation == ExpressionType.Not)
            {
                return Expression.Not(WrapBooleanTest(AnalyzeExpression(expression.Operand, scope)));
            }

            // Example purposes only, we should never get here since we only have Not.
            return Expression.Dynamic(scope.GetRuntime().GetUnaryOperationBinder(expression.Operation),
                typeof(Object),
                AnalyzeExpression(expression.Operand, scope));
        }

        /// <summary>
        /// Returns an Expression for accessing an element of an aggregate structure.
        /// </summary>
        /// <devdoc>
        /// This also works for .NET objects with indexer Item properties. We handle analyzing Elt for
        /// assignment in <see cref="AnalyzeAssignment(SymplAssignment, AnalysisScope)"/>.
        /// </devdoc>
        public static Expression AnalyzeElt(SymplElt expression, AnalysisScope scope) => Expression.Dynamic(
                scope.GetRuntime().GetGetIndexBinder(new CallInfo(expression.Indexes.Length)),
                typeof(Object),
                Analyze(expression.Indexes, scope, AnalyzeExpression(expression.ObjectExpr, scope)));
    }
}