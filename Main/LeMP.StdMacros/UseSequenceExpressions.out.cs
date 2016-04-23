// Generated from UseSequenceExpressions.ecs by LeMP custom tool. LeMP version: 1.7.5.0
// Note: you can give command-line arguments to the tool via 'Custom Tool Namespace':
// --no-out-header       Suppress this message
// --verbose             Allow verbose messages (shown by VS as 'warnings')
// --timeout=X           Abort processing thread after X seconds (default: 10)
// --macros=FileName.dll Load macros from FileName.dll, path relative to this file 
// Use #importMacros to use macros in a given namespace, e.g. #importMacros(Loyc.LLPG);
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Loyc;
using Loyc.Syntax;
using Loyc.Collections;
using S = Loyc.Syntax.CodeSymbols;
using Loyc.Ecs;
namespace LeMP
{
	partial class StandardMacros
	{
		static readonly Symbol __runSequence = (Symbol) "#runSequence";
		static readonly Symbol _useSequenceExpressionsIsRunning = (Symbol) "#useSequenceExpressionsIsRunning";
		[LexicalMacro("#runSequence { Stmts; };", "Allows #runSequence at brace-scope without the use of #useSequenceExpressions", "#runSequence", Mode = MacroMode.Passive)]
		public static LNode runSequence(LNode node, IMacroContext context)
		{
			if (context.Parent.Calls(S.Braces))
				return node.With(S.Splice, MaybeRemoveNoOpFromRunSeq(node.Args));
			if (!context.ScopedProperties.ContainsKey(_useSequenceExpressionsIsRunning))
				Reject(context, node, "#useSequenceExpressions is required to make #runSequence work");
			return null;
		}
		public static VList<LNode> MaybeRemoveNoOpFromRunSeq(VList<LNode> runSeq)
		{
			if (runSeq.Count > 1 && runSeq.Last.IsId)
				return runSeq.WithoutLast(1);
			return runSeq;
		}
		[LexicalMacro("#useSequenceExpressions; ... if (Foo.Bar()::b.Baz != null) b.Baz.Method(); ...", "Enables the use of variable-declaration and #runSequence expressions, including the quick-binding operator `::` and the `with` expression, in the code that follows." + "Technically this allows any executable code in an expression context, such as while and for-loops, " + "but its name comes from the fact that it is usually used to allow variable declarations. " + "#useSequenceExpressions expects to be used in a declaration context, " + "e.g. at class or namespace level, not within a function.", "#useSequenceExpressions", Mode = MacroMode.NoReprocessing)]
		public static LNode useSequenceExpressions(LNode node, IMacroContext context)
		{
			var tmp_0 = context.GetArgsAndBody(true);
			var args = tmp_0.Item1;
			var body = tmp_0.Item2;
			if (args.Count > 0)
				context.Write(Severity.Error, node[1], "#useSequenceExpressions does not support arguments.");
			{
				context.ScopedProperties[_useSequenceExpressionsIsRunning] = G.BoxedTrue;
				try {
					;
					body = context.PreProcess(body);
				} finally {
					context.ScopedProperties.Remove(_useSequenceExpressionsIsRunning);
				}
			}
			var ers = new EliminateRunSequences(context);
			return ers.EliminateSequenceExpressions(body, true).AsLNode(S.Splice);
		}
		class EliminateRunSequences
		{
			public IMacroContext Context;
			public EliminateRunSequences(IMacroContext context)
			{
				Context = context;
			}
			LNode[] _arrayOf1 = new LNode[1];
			public VList<LNode> EliminateSequenceExpressions(VList<LNode> stmts, bool isDeclContext)
			{
				return stmts.SmartSelectMany(stmt => {
					LNode result = EliminateSequenceExpressions(stmt, isDeclContext);
					if (result != stmt) {
						VList<LNode> results;
						if (result.Calls(__runSequence)) {
							results = MaybeRemoveNoOpFromRunSeq(result.Args);
							return results;
						}
					}
					_arrayOf1[0] = result;
					return _arrayOf1;
				});
			}
			public LNode EliminateSequenceExpressions(LNode stmt, bool isDeclContext)
			{
				LNode retType, name, argList, bases, body, initValue;
				if (EcsValidators.SpaceDefinitionKind(stmt, out name, out bases, out body) != null) {
					return body == null ? stmt : stmt.WithArgChanged(2, EliminateSequenceExpressions(body, true));
				} else if (EcsValidators.MethodDefinitionKind(stmt, out retType, out name, out argList, out body, true) != null) {
					return body == null ? stmt : stmt.WithArgChanged(3, EliminateSequenceExpressions(body, false));
				} else if (EcsValidators.IsPropertyDefinition(stmt, out retType, out name, out argList, out body, out initValue)) {
					stmt = stmt.WithArgChanged(3, body.WithArgs(part => {
						if (part.ArgCount == 1 && part[0].Calls(S.Braces))
							part = part.WithArgChanged(0, EliminateSequenceExpressions(part[0], false));
						return part;
					}));
					if (initValue != null) {
						var initMethod = EliminateRunSeqFromInitializer(retType, name, ref initValue);
						if (initMethod != null) {
							stmt = stmt.WithArgChanged(4, initValue);
							return LNode.Call((Symbol) "#runSequence", LNode.List(stmt, initMethod));
						}
					}
					return stmt;
				} else if (!isDeclContext) {
					return EliminateSequenceExpressionsInExecStmt(stmt);
				} else if (stmt.CallsMin(S.Var, 2)) {
					var results = new List<LNode> { 
						stmt
					};
					var vars = stmt.Args;
					var varType = vars[0];
					for (int i = 1; i < vars.Count; i++) {
						var @var = vars[i];
						if (@var.Calls(CodeSymbols.Assign, 2) && (name = @var.Args[0]) != null && (initValue = @var.Args[1]) != null) {
							var initMethod = EliminateRunSeqFromInitializer(varType, name, ref initValue);
							if (initMethod != null) {
								results.Add(initMethod);
								vars[i] = vars[i].WithArgChanged(1, initValue);
							}
						}
					}
					if (results.Count > 1) {
						results[0] = stmt.WithArgs(vars);
						return LNode.List(results).AsLNode(__runSequence);
					}
					return stmt;
				} else
					return stmt;
			}
			LNode EliminateSequenceExpressionsInExecStmt(LNode stmt)
			{
				{
					LNode cond, initValue, name, tmp_1, type;
					VList<LNode> attrs, blocks;
					if (stmt.Calls(CodeSymbols.Braces))
						return stmt.WithArgs(EliminateSequenceExpressions(stmt.Args, false));
					else if (stmt.CallsMin(CodeSymbols.If, 1) && (cond = stmt.Args[0]) != null) {
						blocks = new VList<LNode>(stmt.Args.Slice(1));
						return ProcessBlockCallStmt(stmt, 1);
					} else if ((attrs = stmt.Attrs).IsEmpty | true && stmt.Calls(CodeSymbols.Var, 2) && (type = stmt.Args[0]) != null && (tmp_1 = stmt.Args[1]) != null && tmp_1.Calls(CodeSymbols.Assign, 2) && (name = tmp_1.Args[0]) != null && (initValue = tmp_1.Args[1]) != null) {
						initValue = BubbleUpBlocks(initValue);
						{
							LNode last;
							VList<LNode> stmts;
							if (initValue.CallsMin((Symbol) "#runSequence", 1) && (last = initValue.Args[initValue.Args.Count - 1]) != null) {
								stmts = initValue.Args.WithoutLast(1);
								return LNode.Call((Symbol) "#runSequence", LNode.List().AddRange(stmts).Add(LNode.Call(LNode.List(attrs), CodeSymbols.Var, LNode.List(type, LNode.Call(CodeSymbols.Assign, LNode.List(name, last))))));
							}
						}
					} else if (stmt.HasSpecialName && stmt.ArgCount >= 1 && stmt.Args.Last.Calls(S.Braces)) {
						return ProcessBlockCallStmt(stmt, stmt.ArgCount - 1);
					} else {
						return BubbleUpBlocks(stmt, isStmtLevel: true);
					}
				}
				return stmt;
			}
			LNode ProcessBlockCallStmt(LNode stmt, int childStmtsStartAt)
			{
				List<LNode> childStmts = stmt.Slice(childStmtsStartAt).ToList();
				LNode partialStmt = stmt.WithArgs(stmt.Args.First(childStmtsStartAt));
				VList<LNode> advanceSequence;
				if (ProcessBlockCallStmt2(ref partialStmt, out advanceSequence, childStmts)) {
					stmt = partialStmt.PlusArgs(childStmts);
					if (advanceSequence.Count != 0)
						return LNode.Call(CodeSymbols.Braces, LNode.List().AddRange(advanceSequence).Add(stmt)).SetStyle(NodeStyle.Statement);
					return stmt;
				} else
					return stmt;
			}
			bool ProcessBlockCallStmt2(ref LNode partialStmt, out VList<LNode> advanceSequence, List<LNode> childStmts)
			{
				bool childChanged = false;
				for (int i = 0; i < childStmts.Count; i++) {
					var oldChild = childStmts[i];
					childStmts[i] = EliminateSequenceExpressionsInChildStmt(oldChild);
					childChanged |= (oldChild != childStmts[i]);
				}
				var BubbleUp_GeneralCall2_2 = BubbleUp_GeneralCall2(partialStmt);
				advanceSequence = BubbleUp_GeneralCall2_2.Item1;
				partialStmt = BubbleUp_GeneralCall2_2.Item2;
				return childChanged || !advanceSequence.IsEmpty;
			}
			LNode EliminateSequenceExpressionsInChildStmt(LNode stmt)
			{
				stmt = EliminateSequenceExpressionsInExecStmt(stmt);
				if (stmt.Calls(__runSequence))
					return stmt.With(S.Braces, MaybeRemoveNoOpFromRunSeq(stmt.Args));
				return stmt;
			}
			LNode EliminateRunSeqFromInitializer(LNode retType, LNode fieldName, ref LNode expr)
			{
				expr = BubbleUpBlocks(expr);
				if (expr.CallsMin(__runSequence, 1)) {
					var statements = expr.Args.WithoutLast(1);
					var finalResult = expr.Args.Last;
					LNode methodName = F.Id(KeyNameComponentOf(fieldName).Name + "_initializer");
					expr = LNode.Call(methodName);
					return LNode.Call(LNode.List(LNode.Id(CodeSymbols.Static)), CodeSymbols.Fn, LNode.List(retType, methodName, LNode.Call(CodeSymbols.AltList), LNode.Call(CodeSymbols.Braces, LNode.List().AddRange(statements).Add(LNode.Call(CodeSymbols.Return, LNode.List(finalResult)))).SetStyle(NodeStyle.Statement)));
				} else
					return null;
			}
			LNode BubbleUpBlocks(LNode expr, bool isStmtLevel = false)
			{
				if (!expr.IsCall)
					return expr;
				LNode result = null;
				if (!isStmtLevel) {
					{
						LNode tmp_3 = null, value, varName, varType = null;
						VList<LNode> attrs;
						if (expr.Calls(CodeSymbols.Braces)) {
							Context.Write(Severity.Error, expr, "A braced block is not supported directly within an expression. Did you mean to use `#runSequence {...}`?");
							result = expr;
						} else if ((attrs = expr.Attrs).IsEmpty | true && attrs.NodeNamed(S.Out) != null && expr.Calls(CodeSymbols.Var, 2) && (varType = expr.Args[0]) != null && (varName = expr.Args[1]) != null && varName.IsId) {
							if (varType.IsIdNamed(S.Missing))
								Context.Write(Severity.Error, expr, "#useSequenceExpressions: the data type of this variable declaration cannot be inferred and must be stated explicitly.");
							result = LNode.Call((Symbol) "#runSequence", LNode.List(expr.WithoutAttrNamed(S.Out), varName));
						} else if ((attrs = expr.Attrs).IsEmpty | true && expr.Calls(CodeSymbols.Var, 2) && (varType = expr.Args[0]) != null && (tmp_3 = expr.Args[1]) != null && tmp_3.Calls(CodeSymbols.Assign, 2) && (varName = tmp_3.Args[0]) != null && (value = tmp_3.Args[1]) != null || (attrs = expr.Attrs).IsEmpty | true && expr.Calls(CodeSymbols.ColonColon, 2) && (value = expr.Args[0]) != null && IsQuickBindLhs(value) && (varName = expr.Args[1]) != null && varName.IsId)
							result = ConvertVarDeclToRunSequence(attrs, varType ?? F.Missing, varName, value);
					}
				}
				if (result == null) {
					if (expr.Calls(__runSequence))
						result = expr;
					else
						result = BubbleUp_GeneralCall(expr);
				}
				if (result.Calls(__runSequence))
					return result.WithArgs(EliminateSequenceExpressions(result.Args, false));
				else
					return result;
			}
			LNode BubbleUp_GeneralCall(LNode expr)
			{
				var BubbleUp_GeneralCall2_4 = BubbleUp_GeneralCall2(expr);
				var combinedSequence = BubbleUp_GeneralCall2_4.Item1;
				expr = BubbleUp_GeneralCall2_4.Item2;
				if (combinedSequence.Count != 0)
					return LNode.Call((Symbol) "#runSequence", LNode.List().AddRange(combinedSequence).Add(expr));
				else
					return expr;
			}
			Pair<VList<LNode>,LNode> BubbleUp_GeneralCall2(LNode expr)
			{
				var target = expr.Target;
				var args = expr.Args;
				var combinedSequence = LNode.List();
				target = BubbleUpBlocks(target);
				if (target.CallsMin(__runSequence, 1)) {
					combinedSequence = target.Args.WithoutLast(1);
					expr = expr.WithTarget(target.Args.Last);
				}
				args = args.SmartSelect(arg => BubbleUpBlocks(arg));
				int lastRunSeq = args.LastIndexWhere(a => a.CallsMin(__runSequence, 1));
				if (lastRunSeq >= 0) {
					if (lastRunSeq > 0 && (args.Count == 2 && (target.IsIdNamed(S.And) || target.IsIdNamed(S.Or)) || args.Count == 3 && target.IsIdNamed(S.QuestionMark))) {
						Context.Write(Severity.Error, target, "#useSequenceExpressions is not designed to support sequences or variable declarations on the right-hand side of the `&&`, `||` or `?` operators. The generated code will be incorrect.");
					}
					var argsW = args.ToList();
					for (int i = 0; i <= lastRunSeq; i++) {
						LNode arg = argsW[i];
						if (!arg.IsLiteral) {
							if (arg.CallsMin(__runSequence, 1)) {
								combinedSequence.AddRange(arg.Args.WithoutLast(1));
								argsW[i] = arg = arg.Args.Last;
							}
							if (i < lastRunSeq) {
								LNode tmpVarName, tmpVarDecl = TempVarDecl(arg, out tmpVarName);
								combinedSequence.Add(tmpVarDecl);
								argsW[i] = tmpVarName;
							}
						}
					}
					expr = expr.WithArgs(LNode.List(argsW));
				}
				return Pair.Create(combinedSequence, expr);
			}
			LNode ConvertVarDeclToRunSequence(VList<LNode> attrs, LNode varType, LNode varName, LNode initValue)
			{
				initValue = BubbleUpBlocks(initValue);
				varType = varType ?? F.Missing;
				LNode @ref;
				attrs = attrs.WithoutNodeNamed(S.Ref, out @ref);
				{
					LNode resultValue;
					VList<LNode> stmts;
					if (initValue.CallsMin((Symbol) "#runSequence", 1) && (resultValue = initValue.Args[initValue.Args.Count - 1]) != null) {
						stmts = initValue.Args.WithoutLast(1);
						var newVarDecl = LNode.Call(LNode.List(attrs), CodeSymbols.Var, LNode.List(varType, LNode.Call(CodeSymbols.Assign, LNode.List(varName, resultValue))));
						return initValue.WithArgs(stmts.Add(newVarDecl).Add(varName));
					} else {
						var newVarDecl = LNode.Call(LNode.List(attrs), CodeSymbols.Var, LNode.List(varType, LNode.Call(CodeSymbols.Assign, LNode.List(varName, initValue))));
						return LNode.Call((Symbol) "#runSequence", LNode.List(newVarDecl, varName));
					}
				}
			}
			static bool IsQuickBindLhs(LNode value)
			{
				if (!value.IsId)
					return true;
				return char.IsUpper(value.Name.Name.TryGet(0, '\0'));
			}
		}
	}
}
