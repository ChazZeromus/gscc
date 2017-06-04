using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameScriptCompiler
{

	namespace CodeDOM
	{
		public abstract class Expressable
		{
			public abstract String Express(int indentLevel);
		}

		/// <summary>
		/// A null expression class to represent to absense of
		/// any expressions. Not used by the DOMParser but
		/// can be used by any facilities that make use
		/// of Expressable type objects.
		/// </summary>
		public class NullExpression : Expressable
		{
			public override string Express(int indentLevel)
			{
				return "".Indent(indentLevel);
			}
		}

		public abstract class Expression : Expressable
		{
			public abstract ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b);
			[Obsolete("You should never instantiate an empty expression", true)]
			public Expression()
			{
				throw new NotImplementedException();
			}

			public enum ExpressionTypes
			{
				Reference,// (var)
				Constant,// (1), ("yeah")
				InlineArray,//a = [as,4];
				BinaryOperator,//(a > 55)
				UnaryOperator,//(!a), (-a)
				Group,//(a+g)
				Statement,//a=4, a=4+g
				Truple,//(a,3,5)
				Case
			}
			protected ExpressionTypes _ExpressionType;
			public Boolean IsResolved = false;
			public static int BaseId = 0;
			public int ID;

			private Text.DocLocation _Location = new Text.DocLocation();
			public Text.DocLocation Location
			{
				get
				{
					return _Location;
				}
				set
				{
					//Perfect for breakpoints
					_Location = value;
				}
			}

			public Expression(ExpressionTypes type)
			{
				this._ExpressionType = type;
				this.ID = ++BaseId;
			}

			public ExpressionTypes GetExpressionType()
			{
				return this._ExpressionType;
			}
			public abstract Boolean IsComplete();
			public abstract void Add(Object expression);
			public Boolean IsParamAllowed(Expression expression)
			{
				throw new NotImplementedException();
			}

			/// <summary>
			/// Group type expressions are ones that are unary parameter
			/// type expressions. There are only two, groups and indexers.
			/// 
			/// Unary calls do not count because they do not require
			/// opening/closing tokens to group.
			/// </summary>
			/// <returns></returns>
			public Boolean IsGroupingType()
			{
				return this._ExpressionType == ExpressionTypes.Group
					|| this.IsReference(Reference.ReferenceTypes.Indexer)
					|| this.IsInvocation()
					|| this._ExpressionType == ExpressionTypes.InlineArray;
			}
			public Boolean IsGroup()
			{
				return this._ExpressionType == ExpressionTypes.Group;
			}
			public Boolean IsPrimitivable()
			{
				return (this.IsBinaryOperation() || this.IsConstant() || this.IsGroup()
					|| (this.IsUnaryOperation() && !this.AsUnaryOperation().OperationType.IsPostfixUnary())
					|| (this.IsTruple()));
			}
			/// <summary>
			/// Checks whether expression is a unary or binary operations. This does
			/// not include assignments.
			/// </summary>
			/// <returns></returns>
			public Boolean IsOperation()
			{
				return this.IsBinaryOperation() || this.IsUnaryOperation();
			}


			public Boolean IsInvocation()
			{
				return IsStatement(Statement.StatementTypes.FunctionCall) || IsStatement(Statement.StatementTypes.PointerCall);
			}
			public Statement AsStatement() { return this as Statement; }
			public Reference AsReference() { return this as Reference; }
			public Constant AsConstant() { return this as Constant; }
			public Boolean IsBiConditionalStatement()
			{
				return this.IsConditionalStatement(ConditionalTypes.BiConditional);
			}
			public Boolean IsConverseStatement()
			{
				return this.IsConditionalStatement(ConditionalTypes.Converse);
			}
			public Boolean	IsConditionalStatement(ConditionalTypes type)
			{
				return this.IsStatement(Statement.StatementTypes.Conditional)
					&& this.AsConditionalStatement().GetConditionType() == type;
			}
			public Boolean IsAssignment()
			{
				return this.IsStatement(Statement.StatementTypes.Assignment);
			}
			public Boolean IsConditionalStatement()
			{
				return this.IsStatement(Statement.StatementTypes.Conditional);
			}
			public Boolean IsPostFixOperator()
			{
				return this.IsUnaryOperation() && this.AsUnaryOperation().OperationType.IsPostfixUnary();
			}
			public Boolean IsStatement()
			{
				return this._ExpressionType == ExpressionTypes.Statement || this.IsPostFixOperator();
			}
			public Boolean IsStatement(Statement.StatementTypes type)
			{
				return this._ExpressionType == ExpressionTypes.Statement
					&& this.AsStatement().GetStatementType() == type;
			}
			public Boolean IsReference(Reference.ReferenceTypes type)
			{
				return this._ExpressionType == ExpressionTypes.Reference
					&& this.AsReference().GetReferenceType() == type;
			}
			public Boolean IsVariableReference()
			{
				return IsReference(Reference.ReferenceTypes.Variable);
			}
			public Boolean IsReference()
			{
				return this._ExpressionType == ExpressionTypes.Reference;
			}
			public Boolean IsConstant(Runtime.VarType type)
			{
				return this._ExpressionType == ExpressionTypes.Constant
					&& this.AsConstant().ConstantType == type;
			}
			public Boolean IsConstant() { return this._ExpressionType == ExpressionTypes.Constant; }
			public Boolean IsBinaryOperation() { return this._ExpressionType == ExpressionTypes.BinaryOperator; }
			public Boolean IsUnaryOperation() { return this._ExpressionType == ExpressionTypes.UnaryOperator;}
			public Boolean IsUnaryOperation(UnaryOperationType type) { return this.IsUnaryOperation() && this.AsUnaryOperation().OperationType == type; }
			public Boolean IsUnaryCall() { return this.IsStatement(Statement.StatementTypes.UnaryCall); }
			public Boolean IsTruple() { return this._ExpressionType == ExpressionTypes.Truple; }
			public Boolean IsInlineArray() { return this._ExpressionType == ExpressionTypes.InlineArray; }
			public Boolean IsAccessor()
			{
				return this._ExpressionType == ExpressionTypes.Reference &&
					this.AsReference().GetReferenceType() == Reference.ReferenceTypes.Accessor;
			}
			public Boolean IsIndexer() { return this.IsReference(Reference.ReferenceTypes.Indexer); }
			public Boolean IsFunctionCall() { return this.IsStatement(Statement.StatementTypes.FunctionCall); }
			public Boolean IsPointerCall() { return this.IsStatement(Statement.StatementTypes.PointerCall); }
			public Boolean IsReturn() { return this.IsUnaryCall() && this.AsUnaryCall().UnaryType == UnaryCall.UnaryTypes.Return; }
			public Boolean IsFlowControl() { return this.IsStatement(Statement.StatementTypes.FlowControl); }
			public Boolean IsFlowControl(FlowControlTypes type) { return this.IsFlowControl() && this.AsFlowControl().FlowControlType == type; }
			public Boolean IsCaseExpression() { return this._ExpressionType == ExpressionTypes.Case; }
			public Boolean IsStatementBlock() { return this.IsStatement(Statement.StatementTypes.Block); }

			public Boolean IsIndexable()
			{
				return this.IsConstant() && this.AsConstant().ConstantType.IsIndexable() ||
					this.IsInlineArray() || this.IsTruple();
			}

			public AssignmentStatement AsAssignment() { return this as AssignmentStatement; }
			public Indexer AsIndexer() { return this as Indexer; }
			public VariableReference AsVariableReference() { return this as VariableReference; }
			public FunctionReference AsFunctionReference() { return this as FunctionReference; }
			public BinaryOperation AsBinaryOperation() { return this as BinaryOperation; }
			public UnaryOperation AsUnaryOperation() { return this as UnaryOperation; }
			public Truple AsTruple() { return this as Truple; }
			public Accessor AsAccessor() { return this as Accessor; }
			public FunctionCall AsFunctionCall() { return this as FunctionCall; }
			public FunctionInvocation AsFunctionInvocation() { return this as FunctionInvocation; }
			public PointerCall AsPointerFunctionCall() { return this as PointerCall; }
			public Return AsReturn() { return this as Return; }
			public Group AsGroup() { return this as Group; }
			public FunctionInvocation AsInvocation() { return this as FunctionInvocation; }
			public InlineArray AsInlineArray() { return this as InlineArray; }
			public ConditionalStatement AsConditionalStatement() { return this as ConditionalStatement; }
			public StatementBlock AsStatementBlock() { return this as StatementBlock; }
			public BiConditionalStatement AsBiConditionalStatement() { return this as BiConditionalStatement; }
			public ConditionalLoopExStatement AsConditionalLoopExStatement() { return this as ConditionalLoopExStatement; }
			public ConditionalLoopStatement AsConditionalLoopStatement() { return this as ConditionalLoopStatement; }
			public ConditionalObjectLoopStatement AsConditionalObjectLoop() { return this as ConditionalObjectLoopStatement; }
			public MultiConditionalStatement AsMultiConditional() { return this as MultiConditionalStatement; }
			public UnaryCall AsUnaryCall() { return this as UnaryCall; }
			public FlowControlStatement AsFlowControl() { return this as FlowControlStatement; }
			public CaseExpression AsCaseExpression() { return this as CaseExpression; }
			public PostFixStatement AsPostfixStatement() { return this as PostFixStatement; }

			public override string ToString()
			{
				return this._ExpressionType.ToString();
			}

			public override string Express(int indentLevel)
			{
				throw new NotImplementedException();
			}

			public abstract IEnumerable<Expression> GetChildren();
		}

		/// <summary>
		/// Doesn't actually hold anything. Just used as a Es placeholder.
		/// </summary>
		public class CaseExpression : Expression
		{
			public Boolean IsDefault = false;
			public CaseExpression(Boolean isDefault)
				: base(ExpressionTypes.Case)
			{
				this.IsDefault = isDefault;
			}

			public override void Add(object expression)
			{
				throw new NotImplementedException();
			}

			public override bool IsComplete()
			{
				throw new NotImplementedException();
			}

			public override string Express(int indentLevel)
			{
				throw new NotImplementedException();
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				throw new NotImplementedException();
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return null;
			}
		}

		public abstract class Statement : Expression
		{
			internal ContextAnalyzation.LocalFrame SourceFrame = null;

			public enum StatementTypes
			{
				UnaryCall,
				FunctionCall,
				PointerCall,
				Assignment,
				Conditional,
				FlowControl,
				FixOperator,
				Block //Not used like the others
			}
			public Statement(StatementTypes type)
				: base(ExpressionTypes.Statement)
			{
				this._StatementType = type;
			}
			protected StatementTypes _StatementType;
			public StatementTypes GetStatementType()
			{
				return this._StatementType;
			}

			public override string ToString()
			{
				return "{0}.{1}".Fmt(base.ToString(), this._StatementType.ToString());
			}

			public String ConditionalExpress(int indentLevel)
			{
				return this.IsStatementBlock() ? this.Express(indentLevel) :
					(this.IsConditionalStatement() ? "{0}" : "{0};").Fmt(this.Express(indentLevel + 1));
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var s = b.AsStatement();
				if (!b.IsStatement())
					return ecd.Raise("Not an assignment.", this, b);
				if (s.GetStatementType() != this._StatementType)
					return ecd.Raise("Not the same assignment type.", this, b);
				return ExprCompareData.Result.True;
			}
		}

		//NOTE: Don't forget to allow this as an expression always.
		//TODO: Turn this into an expression type with its own enum set.
		//Problem with doing that is that we prefer to be part statement
		//because how well it already plays with everything else.
		public class PostFixStatement : Statement
		{
			public Expression Operand;
			public UnaryOperationType PostfixOperation;
			public PostFixStatement(UnaryOperationType postfixOperation)
				: base(StatementTypes.FixOperator)
			{
				PostfixOperation = postfixOperation;
			}

			public override void Add(object expression)
			{
				if (Operand == null)
					Operand = expression as Expression;
				else
					throw new NotImplementedException();
			}

			public override bool IsComplete()
			{
				return Operand != null;
			}

			public override string Express(int indentLevel)
			{
				return "{0}{1}".ExprFmt(Operand, StringMap.UnaryOpKeys[PostfixOperation]).Indent(indentLevel);
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				if (base.ExprCompare(ecd, b) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (b.AsPostfixStatement().PostfixOperation != this.PostfixOperation)
					return ecd.Raise("Not the same postfix operation", this, b);
				if (Operand.ExprCompare(ecd, b.AsPostfixStatement().Operand) == ExprCompareData.Result.False)
					return ecd.Raise("Not the same operand", this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return new Expression[] { Operand };
			}
		}

		public class FlowControlStatement : Statement
		{
			public FlowControlTypes FlowControlType;
			public int Target;

			public FlowControlStatement(FlowControlTypes type, int target)
				: base(StatementTypes.FlowControl)
			{
				FlowControlType = type;
				Target = target;
			}

			public override void Add(object expression)
			{
				throw new NotImplementedException();
			}

			public override bool IsComplete()
			{
				throw new NotImplementedException();
			}

			public override string Express(int indentLevel)
			{
				return StringMap.LoopControlKeys[FlowControlType].ToString().Indent(indentLevel);
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var fs = b.AsFlowControl();
				if (b.IsFlowControl())
					return ecd.Raise("Not a flow control statement", this, b);
				if (FlowControlType != fs.FlowControlType)
					return ecd.Raise("Not the same flow control.", this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return null;
			}
		}

		public abstract class ConditionalStatement : Statement
		{
			public Statement PrimaryStatement = null;
			ConditionalTypes _ConditionType;
			/// <summary>
			/// If we're parsing for a condition. i.e. already parsed opening parenthesis.
			/// </summary>
			public Boolean StartedCondition = false;
			/// <summary>
			/// If we already have a condition.
			/// </summary>
			public Boolean FinishedConditional = false;

			public Boolean HasStatement = false;

			public ConditionalStatement(ConditionalTypes type)
				: base(StatementTypes.Conditional)
			{
				this._ConditionType = type;
			}


			public void SetStatement(Statement statement)
			{
				this.HasStatement = true;
				this.PrimaryStatement = statement;
			}
			public abstract void SetCondition(Expression condition);
			
			public ConditionalTypes GetConditionType()
			{
				return _ConditionType;
			}

			public override string ToString()
			{
				return "{0}:{1}".Fmt(base.ToString(), this._ConditionType.ToString());
			}

			public override void Add(object expression)
			{
				throw new NotImplementedException("Conditionals do not suppose Add()");
			}

			public override bool IsComplete()
			{
				throw new NotImplementedException();
			}

			public override string Express(int indentLevel)
			{
				throw new NotImplementedException();
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var cs = b.AsConditionalStatement();
				if (base.ExprCompare(ecd, b) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (_ConditionType != cs.GetConditionType())
					return ecd.Raise("Different conditional type", this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return new Expression[] { PrimaryStatement };
			}
		}

		public class ConverseStatementPlaceHolder : ConditionalStatement
		{
			public ConverseStatementPlaceHolder()
				: base(ConditionalTypes.Converse)
			{
				this.FinishedConditional = true;
				this.StartedCondition = true;
			}

			public override void SetCondition(Expression condition)
			{
				throw new NotImplementedException();
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				throw new NotImplementedException();
			}
		}

		public class BiConditionalStatement : ConditionalStatement
		{
			public Statement ConverseStatement = null;
			public Expression Condition = null;
			public Boolean HasConverseStatement = false;
			public BiConditionalStatement()
				: base(ConditionalTypes.BiConditional)
			{
			}

			public override bool IsComplete()
			{
				return PrimaryStatement != null && ConverseStatement != null;
			}

			public override string Express(int indentLevel)
			{
				StringBuilder s = new StringBuilder();
				s.Append("if ({0})".ExprFmt(Condition, PrimaryStatement == null ? ";" : "").Indent(indentLevel));
				if (PrimaryStatement != null)
				{
					s.AppendLine();
					s.Append(PrimaryStatement.ConditionalExpress(indentLevel));
				}
				else
					s.Append(";");
				if (HasConverseStatement)
				{
					s.AppendLine();
					s.Append("else".Indent(indentLevel));
					if (ConverseStatement != null)
					{
						s.AppendLine();
						s.Append(ConverseStatement.ConditionalExpress(indentLevel));
					}
					else
						s.Append(";");
				}
				return s.ToString();
			}

			public override void SetCondition(Expression condition)
			{
				this.Condition = condition;
			}

			public void SetConverseStatement(Statement statement)
			{
				this.ConverseStatement = statement;
				this.HasConverseStatement = true;
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				BiConditionalStatement bc = b.AsBiConditionalStatement();
				if (base.ExprCompare(ecd, b) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (Condition.ExprCompare(ecd, bc.Condition) == ExprCompareData.Result.False)
					return ecd.Raise("Different condition expression", this, b);
				if (PrimaryStatement.ExprCompare(ecd, bc.PrimaryStatement) == ExprCompareData.Result.False)
					return ecd.Raise("Different primary statement", this, b);
				if (ecd.NullCompare(ConverseStatement, bc.ConverseStatement) == ExprCompareData.Result.False)
					return ecd.Raise("Different converse statement", this, b);
				return ExprCompareData.Result.True; 
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return new Expression[] { Condition, PrimaryStatement, ConverseStatement };
			}
		}

		public class ConditionalLoopStatement : ConditionalStatement
		{
			public Expression Condition = null;
			public ConditionalLoopStatement()
				: base(ConditionalTypes.Loop)
			{
			}

			public override string Express(int indentLevel)
			{
				StringBuilder s = new StringBuilder();
				s.Append("while ({0})".ExprFmt(this.Condition).Indent(indentLevel));
				if (PrimaryStatement != null)
				{
					s.AppendLine();
					s.Append(PrimaryStatement.ConditionalExpress(indentLevel));
				}
				else
					s.Append(";");
				return s.ToString();
			}

			public override void SetCondition(Expression condition)
			{
				this.Condition = condition;
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				ConditionalLoopStatement cl = b.AsConditionalLoopStatement();
				if (base.ExprCompare(ecd, b) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (Condition.ExprCompare(ecd, cl.Condition) == ExprCompareData.Result.False)
					return ecd.Raise("Different condition expression", this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return new Expression[] { Condition, PrimaryStatement };
			}
		}

		public class ConditionalObjectLoopStatement : ConditionalStatement
		{
			public VariableReference Iterator = null, Key = null;
			public Expression Collection = null;
			public ConditionalObjectLoopStatement()
				: base(ConditionalTypes.ObjectLoop)
			{
			}

			public override void SetCondition(Expression condition)
			{
				throw new NotImplementedException();
			}

			public override bool IsComplete()
			{
				return Iterator != null && Collection != null && PrimaryStatement != null;
			}

			public override string Express(int indentLevel)
			{
				StringBuilder s = new StringBuilder();
				s.Append("foreach ({0}{1} in {2})".ExprFmt(Key == null ? "" : "{0}, ".ExprFmt(Key),
					Iterator, Collection).Indent(indentLevel));
				if (PrimaryStatement != null)
				{
					s.AppendLine();
					s.Append(this.PrimaryStatement.ConditionalExpress(indentLevel));
				}
				else
					s.Append(";");
				return s.ToString();
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var ol = b.AsConditionalObjectLoop();
				if (base.ExprCompare(ecd, b) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (Iterator.ExprCompare(ecd, ol.Iterator) == ExprCompareData.Result.False)
					return ecd.Raise("Different iterators", this, b);
				if (ecd.NullCompare(Key, ol.Key) == ExprCompareData.Result.False)
					return ecd.Raise("Different indexers", this, b);
				if (Collection.ExprCompare(ecd, ol.Collection) == ExprCompareData.Result.False)
					return ecd.Raise("Different collection", this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return new Expression[] { Iterator, Key, Collection, PrimaryStatement };
			}
		}

		public class ConditionalLoopExStatement : ConditionalStatement
		{
			public Expression Init = null, Condition = null, Repeater = null;

			public ConditionalLoopExStatement()
				: base(ConditionalTypes.LoopEx)
			{
			}

			public enum ConditionProgress
			{
				NeedInit,
				NeedCondition,
				NeedRepeater,
				Done
			}

			public override void SetCondition(Expression condition)
			{
				throw new NotImplementedException();
			}

			public void AddPart(Expression expression)
			{
				if (Progress == ConditionProgress.NeedInit)
				{
					Init = expression;
					Progress = ConditionProgress.NeedCondition;
				}
				else if (Progress == ConditionProgress.NeedCondition)
				{
					Condition = expression;
					Progress = ConditionProgress.NeedRepeater;
				}
				else if (Progress == ConditionProgress.NeedRepeater)
				{
					Repeater = expression;
					Progress = ConditionProgress.Done;
				}
				else
					throw new NotImplementedException("Too many expression parts for \"for\" statement.");
			}

			public ConditionProgress Progress = ConditionProgress.NeedInit;

			public override bool IsComplete()
			{
				return Progress == ConditionProgress.Done;
			}

			public override string Express(int indentLevel)
			{
				StringBuilder s = new StringBuilder();
				s.Indent(indentLevel);
				s.Append("for ({0}; {1}; {2})".ExprFmt(Init, Condition, Repeater));
				if (PrimaryStatement != null)
				{
					s.AppendLine();
					s.Append(PrimaryStatement.ConditionalExpress(indentLevel));
				}
				else
					s.Append(";");
				return s.ToString();
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var le = b.AsConditionalLoopExStatement();
				if (base.ExprCompare(ecd, b) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (ecd.NullCompare(Init, le.Init) == ExprCompareData.Result.False)
					return ecd.Raise("Different Initializers", this, b);
				if (ecd.NullCompare(Condition, le.Condition) == ExprCompareData.Result.False)
					return ecd.Raise("Different conditions", this, b);
				if (ecd.NullCompare(Repeater, le.Repeater) == ExprCompareData.Result.False)
					return ecd.Raise("Different repeaters", this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return new Expression[] { Init, Condition, Repeater, PrimaryStatement };
			}
		}

		//TODO [HIGH]: Since we can group default with other cases, find a way
		//to preserve the location of the default case in the group
		//instead of using a flag
		//[done]
		public class Case : Expressable
		{
			public class CaseEntry
			{
				public CaseEntry(CaseTypes type)
				{
					CaseType = type;
				}
				public enum CaseTypes
				{
					Value,
					Default
				}
				public CaseValue AsValue() { return this as CaseValue; }
				public CaseTypes CaseType;
				public virtual String Express(int indentLevel)
				{
					if (CaseType != CaseTypes.Default)
						throw new Exception("Calling express on base CaseEntry when it's not even default.");
					return "{0}:".Fmt(StringMap.SwitchKeywordKeys[SwitchKeywords.Default]).Indent(indentLevel);
				}
				public virtual Boolean CompareEntry(CaseEntry ce)
				{
					if (ce.CaseType != CaseType)
						return false;
					return ce.CaseType == CaseTypes.Value && AsValue().Value.EqualsConstant(ce.AsValue().Value);
				}
			}

			public class CaseValue : CaseEntry
			{
				public Constant Value;
				public CaseValue(Constant constant)
					: base(CaseTypes.Value)
				{
					Value = constant;
				}
				public override string Express(int indentLevel)
				{
					return "{0} {1}:".ExprFmt(StringMap.SwitchKeywordKeys[SwitchKeywords.Case], Value).Indent(indentLevel);
				}
			}

			public List<CaseEntry> Values;
			public int Offset;
			public Boolean HasDefault;

			public Case(int offset, CaseEntry @case)
			{
				Offset = offset;
				Values = new List<CaseEntry>();
				Values.Add(@case);
			}

			public static Case CreateDefault(int offset)
			{
				return new Case(offset, new CaseEntry(CaseEntry.CaseTypes.Default)) { HasDefault = true };
			}

			public override String Express(int indentLevel)
			{
				StringBuilder s = new StringBuilder();
				foreach (CaseEntry c in Values)
					s.AppendLine(c.Express(indentLevel));
				return s.ToString();
			}

			public override int GetHashCode()
			{
				return base.GetHashCode();
			}

			public override bool Equals(object obj)
			{
				if (!(obj is Case))
					return false;
				Case b = obj as Case;
				if (this.Offset != b.Offset)
					return false;
				if (this.HasDefault != b.HasDefault)
					return false;
				if (this.HasDefault)
					return true;
				if (this.Values.Count != b.Values.Count)
					return false;
				IEnumerator<CaseEntry> bvals = (obj as Case).Values.AsEnumerable().GetEnumerator();
				foreach (CaseEntry ce in Values)
				{
					bvals.MoveNext();
					if (!ce.CompareEntry(bvals.Current))
						return false;
				}
				return true;
			}
		}

		public class MultiConditionalStatement : ConditionalStatement
		{
			public List<Case> Cases = new List<Case>();
			public Expression CaseExpression;
			public Boolean HasDefault = false;
			internal Boolean InsideCase = false;

			public class Segment
			{
				public Case[] Cases;
				public StatementBlock Statements;
			}

			public MultiConditionalStatement()
				: base(ConditionalTypes.MultiConditional)
			{
			}

			//TODO: Include segments that are the last case that has no
			//statements. We seem to skip them...
			public Segment[] GetSegments()
			{
				List<Segment> segList = new List<Segment>();
				List<Case> tempCases = new List<Case>();
				List<Statement> tempStmts = new List<Statement>();
				Return lastReturn = null;
				Segment tempSeg = null;
				int c = 0;
				foreach (var s in PrimaryStatement.AsStatementBlock().AsIndexable())
				{
					if (c < Cases.Count && Cases[c].Offset <= s.Index)
					{
						if (tempSeg != null)
						{
							tempSeg.Statements = new StatementBlock()
							{
								Statements = tempStmts.ToStatementCollection(),
								LastReturn = lastReturn,
								Location = tempStmts.Count > 0 ? tempStmts.First().Location : this.PrimaryStatement.Location
								/*not sure if I should give it this loc*/
							};
							tempStmts.Clear();
							segList.Add(tempSeg);
							tempSeg = null;
							lastReturn = null;
						}
						while (c < Cases.Count && Cases[c].Offset <= s.Index)
							tempCases.Add(Cases[c++]);
						tempSeg = new Segment() { Cases = tempCases.ToArray() };
						tempCases.Clear();
					}
					tempStmts.Add(s.Value);
					if (s.Value.IsReturn())
						lastReturn = s.Value.AsReturn();
				}
				if (tempSeg != null)
				{
					tempSeg.Statements = new StatementBlock()
					{
						Statements = tempStmts.ToStatementCollection(),
						LastReturn = lastReturn,
						Location = tempStmts.Count > 0 ? tempStmts.First().Location : this.PrimaryStatement.Location
					};
					tempStmts.Clear();
					segList.Add(tempSeg);
				}
				if (c < Cases.Count)
				{
					while (c < Cases.Count)
						tempCases.Add(Cases[c++]);
					segList.Add(new Segment()
					{
						Cases = tempCases.ToArray(),
						Statements = new StatementBlock()
						{
							Statements = tempStmts.ToStatementCollection()
						}
					});
				}
				return segList.ToArray();
			}

			/// <summary>
			/// Adds a case based on current statement list. Case will proceed
			/// the last statement.
			/// </summary>
			/// <param name="constant"></param>
			/// <returns>False if the constant already exists in the case list, true if otherwise.</returns>
			public Boolean AddCase(Constant constant, int offset, out Case targetcase)
			{
				foreach (var c in Cases)
					if (!c.HasDefault)
						foreach (var con in c.Values)
							if (con.Equals(constant))
							{
								targetcase = null;
								return false;
							}
				if (Cases.Count <= 0 || Cases.Last().Offset < offset)
					Cases.Add(targetcase = new Case(offset, new Case.CaseValue(constant)));
				else
				{
					targetcase = Cases.Last();
					targetcase.Values.Add(new Case.CaseValue(constant));
				}
				return true;
			}

			public void AddDefault(int offset, out Case targetcase)
			{
				if ((Cases.Count > 0 && Cases.Last().Offset > offset)
						|| (Cases.Count == 0 && offset != 0))
					throw new Exception("Attempted to create case behind statements.");
				if (HasDefault)
					throw new Exception("Attempted to add default case in switch statement that already has case.");
				else
					if (Cases.Count > 0 && Cases.Last().Offset == offset)
					{
						targetcase = Cases.Last();
						targetcase.Values.Add(new Case.CaseEntry(Case.CaseEntry.CaseTypes.Default));
						targetcase.HasDefault = true;
					}
					else //No gawd damn reason why we should be 'inserting' a case.
					{
						targetcase = Case.CreateDefault(offset);
						Cases.Add(targetcase);
					}
				HasDefault = true;
			}

			public override bool IsComplete()
			{
				return CaseExpression != null;
			}

			public override string Express(int indentLevel)
			{
				StringBuilder sb = new StringBuilder();
				//We don't have to worry about null primaries because we have to find
				sb.AppendLine("switch({0})".ExprFmt(this.CaseExpression).Indent(indentLevel));
				sb.AppendLine("{".Indent(indentLevel));
				int i = 0, c = 0;
				foreach (Statement s in this.PrimaryStatement.AsStatementBlock().Statements.Collection)
				{
					/*We really only need to loop when there's a proceeding default case.
					 * Because defaults dont stack with value, and that's the premise
					 * of Case.Values
					 */
					while (c < Cases.Count && i == Cases[c].Offset)
					{
						sb.Append(Cases[c].Express(indentLevel + 1));
						c++;
					}
					sb.AppendLine(StatementBlock.ExpressSingleStatement(s, indentLevel + 1));
					i++;
				}
				if (c < Cases.Count && i == Cases[c].Offset)
					sb.Append(Cases[c].Express(indentLevel + 1));
				sb.AppendLine("}".Indent(indentLevel));
				return sb.ToString();
			}

			public override void SetCondition(Expression condition)
			{
				this.CaseExpression = condition;
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var mc = b.AsMultiConditional();
				if (base.ExprCompare(ecd, b) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (CaseExpression.ExprCompare(ecd, mc.CaseExpression) == ExprCompareData.Result.False)
					return ecd.Raise("Different case condition", this, b);
				if (Cases.Count != mc.Cases.Count)
					return ecd.Raise("Different case count.", this, b);
				for (int i = 0; i < Cases.Count; ++i)
					if (!Cases[i].Equals(mc.Cases[i]))
						return ecd.Raise("Different cases ({0}) != ({1})".Fmt(Cases[i].Express(0), mc.Cases[i].Express(0)),
							this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return new Expression[] { CaseExpression, PrimaryStatement };
			}
		}

		public class StatementBlock : Statement, IEnumerable<Statement>, ICollection<Statement>
		{
			public StatementCollection Statements = null;
			internal Return LastReturn = null;
			public StatementBlock()
				: base(StatementTypes.Block)
			{
			}

			public override bool IsComplete()
			{
				throw new NotImplementedException();
			}

			public override void Add(object expression)
			{
				throw new NotImplementedException();
			}

			public static String ExpressSingleStatement(Statement s, int indentLevel)
			{
				if (s != null)
					return ((!s.IsConditionalStatement() && !s.IsStatementBlock()) ?
						"{0};" : "{0}").Fmt(s.Express(indentLevel + 1));
				else
					return ";".Indent(indentLevel + 1);
			}

			public static String ExpressCollection(IEnumerable<Statement> statements, int indentLevel)
			{
				StringBuilder sb = new StringBuilder();
				sb.AppendLine("{".Indent(indentLevel));
				foreach (var s in statements)
					sb.AppendLine(ExpressSingleStatement(s, indentLevel));
				sb.Append("}".Indent(indentLevel));
				return sb.ToString();
			}

			public override string Express(int indentLevel)
			{
				return ExpressCollection(this.Statements.Collection, indentLevel);
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				StatementBlock s = b.AsStatementBlock();
				if (base.ExprCompare(ecd, b) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (Statements.Length != s.Statements.Length)
					return ecd.Raise("Different statement sequence length.", this, b);
				IEnumerator<Statement> e = s.Statements.AsEnumerable<Statement>().GetEnumerator();
				foreach (var stmt in Statements)
				{
					e.MoveNext();
					if (stmt == null && e.Current == null)
						continue;
					if ((stmt == null || e.Current == null) || stmt.ExprCompare(ecd, e.Current) == ExprCompareData.Result.False)
						return ecd.Raise("Different statement", this, b);
				}
				return ExprCompareData.Result.True;
			}

			public IEnumerator<Statement> GetEnumerator()
			{
				return Statements.Collection.AsEnumerable().GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return Statements.Collection.GetEnumerator();
			}

			/// <summary>
			/// DO NOT USE
			/// </summary>
			/// <param name="item"></param>
			public void Add(Statement item)
			{
				throw new NotImplementedException();
			}

			/// <summary>
			/// DO NOT USE
			/// </summary>
			public void Clear()
			{
				throw new NotImplementedException();
			}

			public bool Contains(Statement item)
			{
				return Statements.Collection.Contains(item);
			}

			public void CopyTo(Statement[] array, int arrayIndex)
			{
				Statements.Collection.CopyTo(array, arrayIndex);
			}

			public int Count
			{
				get { return Statements.Collection.Length; }
			}

			public bool IsReadOnly
			{
				get { return Statements.Collection.IsReadOnly; }
			}

			/// <summary>
			/// DO NOT USE
			/// </summary>
			/// <param name="item"></param>
			/// <returns></returns>
			public bool Remove(Statement item)
			{
				throw new NotImplementedException();
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return Statements;
			}
		}

		public class StatementCollection : System.Collections.Generic.IEnumerable<Statement>
		{
			public Statement[] Collection;
			public StatementCollection(Statement[] statements)
			{
				this.Collection = statements;
			}
			public int Length
			{
				get
				{
					return Collection.Length;
				}
			}
			System.Collections.Generic.IEnumerator<Statement> System.Collections.Generic.IEnumerable<Statement>.GetEnumerator()
			{
				return Collection.AsEnumerable().GetEnumerator();
			}
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return Collection.GetEnumerator();
			}
		}

		public abstract class FunctionInvocation : Statement
		{
			public FunctionInvocation()
				: base(StatementTypes.PointerCall)
			{
			}

			public Expression Self = null;
			public List<Expression> Arguments = new List<Expression>();
			public Boolean StartedParameterList = false;
			public List<CallModifiers> Modifiers = new List<CallModifiers>();

			public abstract Boolean HasFunction();

			public override bool IsComplete()
			{
				return StartedParameterList;
			}

			public override void Add(object expression)
			{
				throw new NotImplementedException();
			}

			public String ExpressPreFunc()
			{
				StringBuilder sb = new StringBuilder();
				foreach (CallModifiers cm in Modifiers)
				{
					sb.Append(StringMap.CallModKeys[cm].ToString());
					sb.Append(" ");
				}
				String o = "{0}{1}".Fmt(Self == null ? "" : Self.Express(0) + " ", sb.ToString());
				return o;
			}

			public String ExpressParams()
			{
				StringBuilder sb = new StringBuilder("(");
				int i = 0;
				foreach (var e in this.Arguments)
				{
					sb.Append(e.Express(0));
					if (++i < this.Arguments.Count)
						sb.Append(", ");
				}
				sb.Append(")");
				return sb.ToString();
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var fi = b.AsFunctionInvocation();
				if (base.ExprCompare(ecd, b) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (ecd.NullCompare(this.Self, fi.Self) == ExprCompareData.Result.False)
					return ecd.Raise("Different self expression", this, b);
				if (Modifiers.Count != fi.Modifiers.Count)
					return ecd.Raise("Different modifier count.", this, b);
				IEnumerator<CallModifiers> ce = fi.Modifiers.GetEnumerator();
				foreach (var cm in Modifiers)
				{
					ce.MoveNext();
					if (cm != ce.Current)
						return ecd.Raise("Different call modifiers.", this, b);
				}

				if (Arguments.Count != fi.Arguments.Count)
					return ecd.Raise("Different param count.", this, b);

				IEnumerator<Expression> ee = fi.Arguments.GetEnumerator();

				foreach (var p in Arguments)
				{
					ee.MoveNext();
					if (p.ExprCompare(ecd, ee.Current) == ExprCompareData.Result.False)
						return ecd.Raise("Different parameter", this, b);
				}
				return ExprCompareData.Result.True;
			}

			public Boolean IsSelfASelfCall()
			{
				return Self != null && Self.IsInvocation() && Self.AsInvocation().Self != null;
			}
		}

		public class FunctionCall : FunctionInvocation
		{
			/// <summary>
			/// Self can be null to imply the current self object.
			/// </summary>
			
			public FunctionReference Function;
			public FunctionCall(CallModifiers[] modifiers = null)
				: base()
			{
				this._StatementType = StatementTypes.FunctionCall;
			}

			public override bool HasFunction()
			{
				return Function != null;
			}

			public override bool IsComplete()
			{
				return Function != null;
			}

			public override void Add(object expression)
			{
				throw new NotImplementedException();
			}

			public override string Express(int indentLevel)
			{
				return "{0}{1}{2}".ExprFmt(this.ExpressPreFunc(), this.Function.ScopeType == Reference.ScopeTypes.Local
					? this.Function.Name : this.Function.Express(0), this.ExpressParams()).Indent(indentLevel);
			}

			public PointerCall ConvertToPointerCall()
			{
				return new PointerCall() { Self = Self, Modifiers = Modifiers, Location = Location };
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var fc = b.AsFunctionCall();
				if (base.ExprCompare(ecd, b) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (Function.ExprCompare(ecd, fc.Function) == ExprCompareData.Result.False)
					ecd.Raise("Different function", this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return (new Expression[] { Self }).Concat(Arguments);
			}
		}

		public class PointerCall : FunctionInvocation
		{
			public PointerCall()
				: base()
			{
				this._StatementType = StatementTypes.PointerCall;
			}

			public Expression FunctionPointer;
			public override bool IsComplete()
			{
				return FunctionPointer != null && base.IsComplete();
			}

			public override bool HasFunction()
			{
				return FunctionPointer != null;
			}

			public override void Add(object expression)
			{
				if (FunctionPointer == null)
					FunctionPointer = expression as Expression;
				else
					throw new NotImplementedException();
			}

			public override string Express(int indentLevel)
			{
				return "{0}[[{1}]]{2}"
					.ExprFmt(this.ExpressPreFunc(), this.FunctionPointer, this.ExpressParams()).Indent(indentLevel);
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var pc = b.AsPointerFunctionCall();
				if (base.ExprCompare(ecd, b) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (FunctionPointer.ExprCompare(ecd, pc.FunctionPointer) == ExprCompareData.Result.False)
					return ecd.Raise("Different pointer expression", this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return (new Expression[] { Self, FunctionPointer }).Concat(Arguments);
			}
		}

		/// <summary>
		/// Unary calls are invocations of builtin functions that do not have
		/// parameter lists, but have optional arguments following its keyword.
		/// </summary>
		public class UnaryCall : Statement
		{
			public UnaryFunctionTypes UnaryFunctionType;
			public Expression Arg = null;
			public UnaryTypes UnaryType = UnaryTypes.Other;
			public enum UnaryTypes
			{
				Return,
				Other
			}

			public UnaryCall(UnaryFunctionTypes type)
				: base(StatementTypes.UnaryCall)
			{
				this.UnaryFunctionType = type;
			}

			public override string ToString()
			{
				return "{0}({1})".Fmt(base.ToString(), this.UnaryType.ToString());
			}

			public override bool IsComplete()
			{
				throw new NotImplementedException();
			}

			public override void Add(object expression)
			{
				if (Arg == null)
					Arg = expression as Expression;
				else
					throw new NotImplementedException();
			}

			public override string Express(int indentLevel)
			{
				return "{0}{1}".ExprFmt(StringMap.UnaryFuncKeys[UnaryFunctionType], Arg != null ? " " + Arg.Express(0) : "").Indent(indentLevel);
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var uc = b.AsUnaryCall();
				if (base.ExprCompare(ecd, b) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (UnaryType != uc.UnaryType)
					return ecd.Raise("Different unary type.", this, b);
				if (UnaryFunctionType != uc.UnaryFunctionType)
					return ecd.Raise("Different unary call.", this, b);
				if (ecd.NullCompare(Arg, uc.Arg) == ExprCompareData.Result.False)
					return ecd.Raise("Different unary arguments.", this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return new Expression[] { Arg };
			}
		}

		public class Return : UnaryCall
		{
			public Return()
				: base(UnaryFunctionTypes.Return)
			{
				this.UnaryType = UnaryTypes.Return;
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				return base.ExprCompare(ecd, b);
			}
		}

		public class AssignmentStatement : Statement
		{
			public AssignmentStatement(BinaryOperationType operationType)
				: base(StatementTypes.Assignment)
			{
				this.OperationType = operationType;
			}
			public readonly BinaryOperationType OperationType;
			public Expression Left = null, Right = null;
			public override bool IsComplete()
			{
				return Left != null && Right != null;
			}
			public override void Add(object expression)
			{
				if (Left == null)
					Left = expression as Expression;
				else
					if (Right == null)
						Right = expression as Expression;
					else
						throw new NotImplementedException();
			}
			public override string ToString()
			{
				return "{0}:({1})".Fmt(base.ToString(), this.OperationType.ToString());
			}
			public override string Express(int indentLevel)
			{
				return "{0} {1}= {2}".ExprFmt(Left, OperationType != BinaryOperationType.Assign ?
					StringMap.OpKeys[this.OperationType] : "", Right).Indent(indentLevel);
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var ass = b.AsAssignment();
				if (base.ExprCompare(ecd, b) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (OperationType != ass.OperationType)
					return ecd.Raise("Different operation type.", this, b);
				if (Left.ExprCompare(ecd, ass.Left) == ExprCompareData.Result.False)
					return ecd.Raise("Different left value", this, b);
				if (Right.ExprCompare(ecd, ass.Right) == ExprCompareData.Result.False)
					return ecd.Raise("Different right value", this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return new Expression[] { Left, Right };
			}
		}

		public class Indexer : Reference
		{
			public Indexer()
			{
				this._ReferenceType = ReferenceTypes.Indexer;
			}
			public Expression Array, Index;

			public override void Add(object expression)
			{
				if (Array == null)
					Array = expression as Expression;
				else
					if (Index == null)
						Index = expression as Expression;
					else
						throw new NotImplementedException();
			}

			public override bool IsComplete()
			{
				return Array != null && Index != null;
			}

			public override string Express(int indentLevel)
			{
				return "{0}[{1}]".ExprFmt(Array, Index).Indent(indentLevel);
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				Indexer i = b.AsIndexer();
				if (base.ExprCompare(ecd, b) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (Array.ExprCompare(ecd, i.Array) == ExprCompareData.Result.False)
					return ecd.Raise("Different array objects", this, b);
				if (Index.ExprCompare(ecd, i.Index) == ExprCompareData.Result.False)
					return ecd.Raise("Different index expressions", this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return new Expression[] { Array, Index };
			}
		}

		public class BinaryOperation : Expression
		{
			public BinaryOperation(Expression left)
				: base(ExpressionTypes.BinaryOperator)
			{
				this.Left = left;
			}

			//TODO: Move OperationType init to constructor
			//and assert that we aren't assignments.
			public BinaryOperationType OperationType;
			public Expression Left, Right;

			public override void Add(object expression)
			{
				if (Left == null)
					Left = expression as Expression;
				else
					if (Right == null)
						Right = expression as Expression;
					else
						throw new NotImplementedException();
			}

			public override bool IsComplete()
			{
				return Left != null && Right != null;
			}

			public override string Express(int indentLevel)
			{
				return "{0} {1} {2}".ExprFmt(Left, StringMap.OpKeys[this.OperationType], Right)
					.Indent(indentLevel);
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var bo = b.AsBinaryOperation();
				if (!b.IsBinaryOperation())
					return ecd.Raise("Not a binary operation", this, b);
				if (OperationType != bo.OperationType)
					return ecd.Raise("Not the same operation type", this, b);
				if (Left.ExprCompare(ecd, bo.Left) == ExprCompareData.Result.False)
					return ecd.Raise("Left expression different", this, b);
				if (Right.ExprCompare(ecd, bo.Right) == ExprCompareData.Result.False)
					return ecd.Raise("Right expression different", this, b);
				return ExprCompareData.Result.True;
			}

			public override string ToString()
			{
				return base.ToString() + ": " + StringMap.OpKeys[OperationType];
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return new Expression[] { Left, Right };
			}
		}

		public class UnaryOperation : Expression
		{
			public UnaryOperation(UnaryOperationType type)
				:base(ExpressionTypes.UnaryOperator)
			{
				this.OperationType = type;
			}

			public UnaryOperationType OperationType;
			public Expression Operand;

			public override void Add(object expression)
			{
				if (Operand == null)
					Operand = expression as Expression;
				else
					throw new NotImplementedException();
			}

			public override bool IsComplete()
			{
				return Operand != null;
			}

			public override string Express(int indentLevel)
			{
				return  (OperationType.IsPostfixUnary() ? "{0}{1}" : "{1}{0}").ExprFmt(Operand,
					StringMap.UnaryOpKeys[OperationType]).Indent(indentLevel);
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var uo = b.AsUnaryOperation();
				if (!b.IsUnaryOperation())
					return ecd.Raise("Not unary operation", this, b);
				if (OperationType != uo.OperationType)
					return ecd.Raise("Unary operation different", this, b);
				if (Operand.ExprCompare(ecd, uo.Operand) == ExprCompareData.Result.False)
					return ecd.Raise("Different operands", this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return new Expression[] { Operand };
			}

			public override string ToString()
			{
				return base.ToString() + ": " + StringMap.UnaryOpKeys[OperationType];
			}
		}

		public class InlineArray : Expression
		{
			public List<Expression> Items = new List<Expression>();
			public InlineArray()
				: base(ExpressionTypes.InlineArray)
			{
			}

			public override void Add(object expression)
			{
				Items.Add(expression as Expression);
			}

			public override bool IsComplete()
			{
				throw new NotImplementedException("Inline array is never complete");
			}

			public override string Express(int indentLevel)
			{
				StringBuilder sb = new StringBuilder("[".Indent(indentLevel));
				int i = 0;
				foreach (var e in this.Items)
				{
					sb.Append("{0}".ExprFmt(e));
					if (++i < this.Items.Count)
						sb.Append(", ");
				}
				sb.Append("]");
				return sb.ToString();
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var ia = b.AsInlineArray();
				if (!b.IsInlineArray())
					return ecd.Raise("Is not inline array", this, b);
				if (Items.Count != ia.Items.Count)
					return ecd.Raise("Different inline array item count", this, b);
				IEnumerator<Expression> e = ia.Items.GetEnumerator();
				foreach (var i in Items)
				{
					e.MoveNext();
					if (i.ExprCompare(ecd, e.Current) == ExprCompareData.Result.False)
						return ecd.Raise("Different inline array item", this, b);
				}
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return Items;
			}
		}

		public class Truple : Expression
		{
			public const int MaxItems = 3;
			public List<Expression> Items = new List<Expression>();

			public Truple()
				: base(ExpressionTypes.Truple)
			{
			}

			public override bool IsComplete()
			{
				if (Items.Count > MaxItems)
					throw new NotImplementedException("Parser gave truple more than 3 items");
				return Items.Count == MaxItems;
			}

			public override void Add(Object expression)
			{
				if (Items.Count < MaxItems)
					Items.Add(expression as Expression);
				else
					throw new NotImplementedException();
			}

			public override string Express(int indentLevel)
			{
				StringBuilder sb = new StringBuilder("(".Indent(indentLevel));
				int i = 0;
				foreach (var e in this.Items)
				{
					sb.Append("{0}".ExprFmt(e));
					if (++i < this.Items.Count)
						sb.Append(", ");
				}
				sb.Append(")");
				return sb.ToString();
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var t = b.AsTruple();
				if (!t.IsTruple())
					return ecd.Raise("Is not truple", this, b);
				//Not needed, but we may support different size truples later
				if (Items.Count != t.Items.Count)
					return ecd.Raise("Different truple item count", this, b);
				IEnumerator<Expression> e = t.Items.GetEnumerator();
				foreach (var i in Items)
				{
					e.MoveNext();
					if (i.ExprCompare(ecd, e.Current) == ExprCompareData.Result.False)
						return ecd.Raise("Different tuple item", this, b);
				}
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return Items;
			}
		}

		public class Constant : Expression
		{
			public Runtime.VarType ConstantType;
			public Object InternalValue = null;
			public String Value = null;

			public static Constant Undefined = new Constant(Runtime.VarType.Undefined, "undefined");

			public Constant(Runtime.VarType type, String value)
				: base(ExpressionTypes.Constant)
			{
				if (!type.IsPrimitive())
					throw new Exception("Attempted to generate constant as non-primitive");
				Value = value;
				ConstantType = type;
			}

			public enum EvaluateContentResult
			{
				Success,
				HexNotAllowed,
				InvalidFloat,
				InvalidInteger,
				InfinityNotAllowed
			}

			public enum EvaluateContentWarnings
			{
				None,
				IntegerExceedsMaximum,
				IntegerExceedsMinimum
			}

			public EvaluateContentResult EvaluateContent(CompilerOptions options, out EvaluateContentWarnings warnings)
			{
				warnings = EvaluateContentWarnings.None;
				switch (ConstantType)
				{
					case Runtime.VarType.Integer:
						{
							Int64 @out = 0;
							String target = Value;
							System.Globalization.NumberStyles style;
							if (Value.StartsWith(StringMap.HexSpecifier, StringComparison.OrdinalIgnoreCase))
								if (options.Allow_HexSpecifier)
								{
									style = System.Globalization.NumberStyles.AllowHexSpecifier;
									target = Value.Remove(0, StringMap.HexSpecifier.Length);
								}
								else
									return EvaluateContentResult.HexNotAllowed;
							else
							{
								target = Value;
								style = System.Globalization.NumberStyles.Integer;
							}

							if (Int64.TryParse(target, style, System.Globalization.CultureInfo.CurrentCulture, out @out))
							{
								if (@out > int.MaxValue)
									warnings = EvaluateContentWarnings.IntegerExceedsMaximum;
								else if (@out < int.MinValue)
									warnings = EvaluateContentWarnings.IntegerExceedsMinimum;
								InternalValue = (int)@out;
								return EvaluateContentResult.Success;
							}
							else
								return EvaluateContentResult.InvalidInteger;
						}
					case Runtime.VarType.Float:
						{
							if (StringMap.NamedConstantKeys.Contains(Value)
								&& StringMap.NamedConstantKeys.Get() == NamedConstantTypes.Infinity)
								if (options.Allow_Infinity)
								{
									InternalValue = float.PositiveInfinity;
									return EvaluateContentResult.Success;
								}
								else
									return EvaluateContentResult.InfinityNotAllowed;
							float @out = 0;
							if (float.TryParse(Value, out @out))
							{
								InternalValue = @out;
								return EvaluateContentResult.Success;
							}
							else
								return EvaluateContentResult.InvalidFloat;
						}
					case Runtime.VarType.Bool:
						InternalValue = Boolean.Parse(Value);
						break;
				}
				return EvaluateContentResult.Success;
				//We're something else here, but that's okay I suppose.
			}

			public override bool IsComplete()
			{
				return Value != null;
			}

			public override void Add(Object expression)
			{
				if (Value != null)
					Value = expression as String; 
				else
					throw new NotImplementedException();
			}

			public bool EqualsConstant(object obj)
			{
				Constant b = obj as Constant;
				return b != null && ConstantType == b.ConstantType && Value == b.Value;
			}

			public static String GetFormattedString(String str)
			{
				StringBuilder sb = new StringBuilder();
				Char C;
				foreach (Char c in str)
					if (StringMap.CharToEscape.TryGetValue(c, out C))
						sb.Append("\\{0}".Fmt(C));
					else
						sb.Append(c);
				return sb.ToString();
			}

			public override string Express(int indentLevel)
			{
				if (!ConstantType.IsPrimitive())
					throw new Exception("Attempted to express non primitive constant.");
				switch (ConstantType)
				{
					case Runtime.VarType.DirectiveVar:
						return "#{0}".Fmt(Value);
					case Runtime.VarType.MetaString:
					case Runtime.VarType.String:
						{
							StringBuilder sb = new StringBuilder();
							if (ConstantType == Runtime.VarType.String || ConstantType == Runtime.VarType.MetaString)
							{
								if (ConstantType == Runtime.VarType.MetaString)
									sb.Append("&");
								sb.Append("\"{0}\"".ExprFmt(Constant.GetFormattedString(Value)));
							}
							else
								sb.Append(Value);
							return sb.ToString().Indent(indentLevel);
						}
					case Runtime.VarType.ResourceVar:
						return "%{0}".ExprFmt(Value);
				}
				return Value;
			}

			//public class ResolveResult
			//{
			//    public enum ResultTypes
			//    {
			//        InvalidOperation,
			//        /// <summary>
			//        /// Uses lastconstant
			//        /// </summary>
			//        MalformedNumber,
			//        UnknownConstant,
			//        DivisionByZero,
			//        Success
			//    }
			//    public ResultTypes ResultType = ResultTypes.Success;
			//    public String LastConstant = null;
			//}

			//public static Expression ResolveConstant(Expression expr, IEnumerable<DeclaredConstant> lookUp, ref ResolveResult rr)
			//{
			//    Expression cur = expr;
			//    while (cur != null && !cur.IsConstant())
			//    {
			//        switch (cur.GetExpressionType())
			//        {
			//            case ExpressionTypes.Truple:
			//                {
			//                    List<Expression> resolved = new List<Expression>();
			//                    foreach (var e in cur.AsTruple().Items)
			//                    {
			//                        var r = ResolveConstant(e, lookUp, ref rr);
			//                        if (rr.ResultType != ResolveResult.ResultTypes.Success)
			//                            return null;
			//                        resolved.Add(r);
			//                    }
			//                    cur.AsTruple().Items = resolved;
			//                    return cur;
			//                }
			//            case ExpressionTypes.Reference:
			//                if (cur.AsReference().IsVariableReference())
			//                {
			//                    String varname = cur.AsVariableReference().Name;
			//                    var con = lookUp.LookUpConstant(varname);
			//                    if (con == null)
			//                    {
			//                        rr.LastConstant = varname;
			//                        rr.ResultType = ResolveResult.ResultTypes.UnknownConstant;
			//                        return null;
			//                    }
			//                    cur = con.Value;
			//                }
			//                break;
			//            case ExpressionTypes.Group:
			//                cur = cur.AsGroup().Value;
			//                break;
			//            case ExpressionTypes.BinaryOperator:
			//                var bo = cur.AsBinaryOperation();
			//                Expression a = Constant.ResolveConstant(bo.Left, lookUp, ref rr), b;
			//                if (rr.ResultType != ResolveResult.ResultTypes.Success)
			//                    return null;
			//                b = Constant.ResolveConstant(bo.Right, lookUp, ref rr);
			//                if (rr.ResultType != ResolveResult.ResultTypes.Success)
			//                    return null;

			//                if (!a.IsConstant() || !b.IsConstant())
			//                {
			//                    if (a.IsTruple() || b.IsTruple())
			//                        throw new NotSupportedException("Are we suppose to support truple arithmetic? You tell me.");
			//                    rr.ResultType = ResolveResult.ResultTypes.InvalidOperation;
			//                    return null;
			//                }

			//                var ac = a.AsConstant();
			//                var bc = b.AsConstant();
			//                switch (a.AsConstant().ConstantType)
			//                {
			//                    case Runtime.VarType.String:
			//                        if (bc.ConstantType == Runtime.VarType.String || bc.ConstantType == Runtime.VarType.Integer ||
			//                            bc.ConstantType == Runtime.VarType.Float || bc.ConstantType == Runtime.VarType.Bool)
			//                            cur = new Constant(Runtime.VarType.String, ac.Value + ac.Value);
			//                        else
			//                        {
			//                            rr.ResultType = ResolveResult.ResultTypes.InvalidOperation;
			//                            return null;
			//                        }
			//                        break;
			//                    case Runtime.VarType.Float:
			//                        {
			//                            float fa, fb, fc;
			//                            if (!float.TryParse(ac.Value, out fa))
			//                            {
			//                                rr.LastConstant = ac.Value;
			//                                rr.ResultType = ResolveResult.ResultTypes.MalformedNumber;
			//                                return null;
			//                            }
			//                            else if (!float.TryParse(bc.Value, out fb))
			//                            {
			//                                rr.LastConstant = bc.Value;
			//                                rr.ResultType = ResolveResult.ResultTypes.MalformedNumber;
			//                                return null;
			//                            }
			//                            else if (bo.OperationType.IsBitWise())
			//                            {
			//                                rr.ResultType = ResolveResult.ResultTypes.InvalidOperation;
			//                                return null;
			//                            }
			//                            else
			//                                switch (bo.OperationType)
			//                                {
			//                                    case BinaryOperationType.Addition:
			//                                        fc = fa + fb;
			//                                        break;
			//                                    case BinaryOperationType.Subtraction:
			//                                        fc = fa - fb;
			//                                        break;
			//                                    case BinaryOperationType.Multiplication:
			//                                        fc = fa * fb;
			//                                        break;
			//                                    case BinaryOperationType.Division:
			//                                        if (fb == 0)
			//                                        {
			//                                            rr.ResultType = ResolveResult.ResultTypes.DivisionByZero;
			//                                            return null;
			//                                        }
			//                                        else
			//                                            fc = fa / fb;
			//                                        break;
			//                                    default:
			//                                        rr.ResultType = ResolveResult.ResultTypes.InvalidOperation;
			//                                        return null;
			//                                }
			//                            cur = new Constant(Runtime.VarType.Float, fc.ToString());
			//                        }
			//                        break;
			//                    case Runtime.VarType.Integer:
			//                        int ia, ib, ic;
			//                        if (!int.TryParse(ac.Value, out ia))
			//                        {
			//                            rr.LastConstant = ac.Value;
			//                            rr.ResultType = ResolveResult.ResultTypes.MalformedNumber;
			//                            return null;
			//                        }
			//                        else if (!int.TryParse(bc.Value, out ib))
			//                            return ResolveConstant(new BinaryOperation(new Constant(Runtime.VarType.Float, ac.Value))
			//                            {
			//                                OperationType = bo.OperationType,
			//                                Right =new Constant(Runtime.VarType.Float, bc.Value)
			//                            }, lookUp, ref rr); //Whole thing is float
			//                        else if (bo.OperationType.IsBitWise())
			//                            return Undefined;
			//                        else
			//                            switch (bo.OperationType)
			//                            {
			//                                case BinaryOperationType.Addition:
			//                                    ic = ia + ib;
			//                                    break;
			//                                case BinaryOperationType.Subtraction:
			//                                    ic = ia - ib;
			//                                    break;
			//                                case BinaryOperationType.Multiplication:
			//                                    ic = ia * ib;
			//                                    break;
			//                                case BinaryOperationType.Division:
			//                                    if (ib == 0)
			//                                    {
			//                                        rr.ResultType = ResolveResult.ResultTypes.DivisionByZero;
			//                                        return null;
			//                                    }
			//                                    else
			//                                        ic = ia / ib;
			//                                    break;
			//                                case BinaryOperationType.BitAND:
			//                                    ic = ia & ib;
			//                                    break;
			//                                case BinaryOperationType.BitOR:
			//                                    ic = ia | ib;
			//                                    break;
			//                                case BinaryOperationType.BitShiftLeft:
			//                                    ic = ia << ib;
			//                                    break;
			//                                case BinaryOperationType.BitShiftRight:
			//                                    ic = ia >> ib;
			//                                    break;
			//                                case BinaryOperationType.BitXOR:
			//                                    ic = ia ^ ib;
			//                                    break;
			//                                default:
			//                                    return Undefined;
			//                            }
			//                        cur = new Constant(Runtime.VarType.Integer, ic.ToString());
			//                        break;
			//                    default:
			//                        rr.ResultType = ResolveResult.ResultTypes.InvalidOperation;
			//                        return null;
			//                }
			//                break;
			//            default:
			//                rr.ResultType = ResolveResult.ResultTypes.InvalidOperation;
			//                return null;
			//        }
			//    }
			//    if (cur != null && cur.IsConstant())
			//        rr.ResultType = ResolveResult.ResultTypes.Success;
			//    return rr.ResultType == ResolveResult.ResultTypes.Success ? cur.AsConstant() : null;
			//}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				Constant c = b.AsConstant();
				if (!b.IsConstant())
					return ecd.Raise("Not a constant", this, b);
				if (ConstantType != c.ConstantType)
					return ecd.Raise("Different constant type", this, b);
				if (Value != c.Value)
					return ecd.Raise("Different value", this, b);
				return ExprCompareData.Result.True;
			}

			public override string ToString()
			{
				return base.ToString() + ": " + Express(0);
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return null;
			}
		}

		public class Group : Expression
		{
			public Expression Value = null;
			public Group()
				: base(ExpressionTypes.Group)
			{
			}

			public override void Add(object expression)
			{
				if (Value == null)
					Value = expression as Expression;
				else
					throw new NotImplementedException();
			}

			public override bool IsComplete()
			{
				return Value != null;
			}

			public override string Express(int indentLevel)
			{
				return "({0})".ExprFmt(Value).Indent(indentLevel);
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var g = b.AsGroup();
				if (!g.IsGroup())
					return ecd.Raise("Not a group", this, b);
				if (Value.ExprCompare(ecd, g.Value) == ExprCompareData.Result.False)
					return ecd.Raise("Different group value", this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return new Expression[] { Value };
			}
		}

		public abstract class Reference : Expression
		{
			public Reference()
				: base(ExpressionTypes.Reference)
			{
			}
			public enum ScopeTypes
			{
				Local,
				External
			}
			public enum ReferenceTypes
			{
				Accessor,
				Variable,
				Indexer,
				Function
			}
			protected ReferenceTypes _ReferenceType;
			public ModuleReference Scope;

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				Reference r = b.AsReference();
				if (!b.IsReference())
					return ecd.Raise("Not a reference", this, b);
				if (r.GetReferenceType() != _ReferenceType)
					return ecd.Raise("Different reference types", this, b);
				if (ScopeType != r.ScopeType)
					return ecd.Raise("Different scope type", this, b);
				if (Scope == null && r.Scope == null)
					return ExprCompareData.Result.True;
				if (Scope == null || r.Scope == null)
					return ecd.Raise("Different scopes (A or B is null)", this, b);
				if (Scope.CompareReference(r.Scope) == ExprCompareData.Result.False)
					return ecd.Raise("Different scopes", this, b);
					
				return ExprCompareData.Result.True;
			}

			public ScopeTypes ScopeType
			{
				get
				{
					return Scope == null ? ScopeTypes.Local : ScopeTypes.External;
				}
			}

			public ReferenceTypes GetReferenceType()
			{
				return this._ReferenceType;
			}

			public override bool IsComplete()
			{
				throw new NotImplementedException();
			}

			public override void Add(Object expression)
			{
				throw new NotImplementedException();
			}

			public override string ToString()
			{
				return _ReferenceType.ToString();
			}

			public override string Express(int indentLevel)
			{
				throw new NotImplementedException();
			}
		}

		public class Accessor : Reference
		{
			public String PropertyName = null;
			public Expression TargetObject;

			public Accessor()
			{
				this._ReferenceType = ReferenceTypes.Accessor;
			}

			public static Accessor Create(Expression target)
			{
				return new Accessor() { TargetObject = target };
			}

			public override bool IsComplete()
			{
				return PropertyName != null && TargetObject != null;
			}

			public override void Add(Object expression)
			{
				if (PropertyName == null)
					PropertyName = expression as String;
				else
					if (TargetObject == null)
						TargetObject = expression as Expression;
			}

			public override string Express(int indentLevel)
			{
				return "{0}.{1}".ExprFmt(TargetObject, PropertyName).Indent(indentLevel);
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var a = b.AsAccessor();
				if (!b.IsAccessor())
					return ecd.Raise("Not an accessor", this, b);
				if (TargetObject.ExprCompare(ecd, a.TargetObject) == ExprCompareData.Result.False)
					return ecd.Raise("Different accessor target object", this, b);
				if (PropertyName != a.PropertyName)
					return ecd.Raise("Different property name", this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return new Expression[] { TargetObject  };
			}
		}

		//::<function_name>
		public class FunctionReference : Reference
		{
			public ContextAnalyzation.ReferenceSourceFunction Source = null;
			public FunctionReference(ModuleReference scope = null)
			{
				this.Scope = scope;
				this._ReferenceType = ReferenceTypes.Function;
			}
			/// <summary>
			/// Whether this was locally explicit.
			/// </summary>
			public Boolean LocalExplicit = false;
			public String Name;
			public override void Add(object expression)
			{
				if (!(expression is ModuleReference))
					throw new Exception("Attempted to add non-module-reference in FunctionReference.Add");
				if (Scope == null)
					Scope = expression as ModuleReference;
				else
					throw new Exception();
			}

			public override string Express(int indentLevel)
			{
				return "{0}::{1}".ExprFmt(Scope == null ? "" : Scope.ToString(), Name).Indent(indentLevel);
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var fr = b.AsFunctionReference();
				if (base.ExprCompare(ecd, b) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (Name != fr.Name)
					return ecd.Raise("Different function names", this, b);
				if (LocalExplicit != fr.LocalExplicit)
					return ecd.Raise("Different scope explicitness", this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return null;
			}
		}

		//TODO: Add a field for VRs that indicate reference for other VRs/Constants/Globals/Functions.
		public class VariableReference : Reference
		{
			public ContextAnalyzation.ReferenceSource Source = null;
			public VariableReference(String name)
			{
				this.Name = name;
				this._ReferenceType = ReferenceTypes.Variable;
			}

			public String Name;
			//TODO: Do something about external variable-references.
			//[done i guess]
			public static VariableReference Create(String name, ModuleReference scope = null)
			{
				return new VariableReference(name)
				{
					Scope = scope
				};
			}

			public override string ToString()
			{
				return "{0}: {1}".Fmt(base.ToString(), Name);
			}

			public override string Express(int indentLevel)
			{
				return (Scope == null ? Name : "{0}::{1}".ExprFmt(Scope.ToString(), Name)).Indent(indentLevel);
			}

			public override ExprCompareData.Result ExprCompare(ExprCompareData ecd, Expression b)
			{
				var vr = b.AsVariableReference();
				if (base.ExprCompare(ecd, b) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (Name != vr.Name)
					return ecd.Raise("Different variable names", this, b);
				return ExprCompareData.Result.True;
			}

			public override IEnumerable<Expression> GetChildren()
			{
				return null;
			}
		}

		public class Param : Expressable
		{
			public Text.DocLocation Location;
			public String Name;
			private List<ParamModifiers> Modifiers = new List<ParamModifiers>();
			public Boolean this[ParamModifiers modifier]
			{
				get
				{
					return Modifiers.Contains(modifier);
				}
				set
				{
					if (value)
					{
						if (this[modifier])
							throw new Exception("Already added modifier {0}".Fmt(modifier.ToString()));
						Modifiers.Add(modifier);
					}
					else
					{
						if (!this[modifier])
							throw new Exception("No modifier {0} exists.".Fmt(modifier.ToString()));
						Modifiers.Remove(modifier);
					}
				}
			}
			public override string ToString()
			{
				return Name;
			}
			public override string Express(int indentLevel)
			{
				StringBuilder sb = new StringBuilder();
				Modifiers.Sort((pmA, pmB) => pmA - pmB);
				foreach (var modifier in Modifiers)
					sb.Append(StringMap.ParamModKeys[modifier] + " ");
				sb.Append(Name);
				return sb.ToString().Indent(indentLevel);
			}
			public Boolean IsParamEqual(Param b)
			{
				return b != null && b.Name == Name && b.Modifiers == Modifiers;
			}
		}

		public class FunctionDefinition : Declaration
		{
			public String Name;
			public Param[] Params;
			public StatementBlock RootStatement = null;
			public Boolean HasVariableArguments = false;
			/// <summary>
			/// The calculated maximum of explicit locals, however more may be needed for implicit computations.
			/// </summary>
			public int? MaxExplicitLocals = null;
			public int? MinimumArgs = null;
			public ContextAnalyzation.CompleteFrames AllFrames = null;

			public FunctionDefinition()
				: base(DeclarationTypes.Function)
			{
			}

			public override string ToString()
			{
				return "{0}() Params:{1} Statements:{2}".Fmt(Name, Params.Length, RootStatement.Statements.Length);
			}

			internal FunctionCall CreateFunctionCall()
			{
				return new FunctionCall()
				{
					StartedParameterList = true,
					Arguments = Params.CreateFromList<Param, Expression>(p => new VariableReference(p.Name)),
					Function = new FunctionReference() { Name = Name },
					Location = Location
				};
			}

			public override String Express(int indentLevel)
			{
				StringBuilder sb = new StringBuilder();
				sb.Append("{0}(".Fmt(Name).Indent(indentLevel));
				int c = 0;
				foreach (Param p in Params)
				{
					if (c++ > 0)
						sb.Append(", ");
					sb.Append(p.Express(0));
				}

				if (RootStatement != null)
				{
					sb.AppendLine(")");
					sb.AppendLine(RootStatement.Express(indentLevel));
				}
				else
					sb.AppendLine(");");
				return sb.ToString();
			}

			public override ExprCompareData.Result CompareDecl(ExprCompareData ecd, Declaration decl)
			{
				if (base.CompareDecl(ecd, decl) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				var d = decl.AsFunctionDefinition();

				if (Name != d.Name)
					return ecd.Raise("Different function name", Location);

				if (Params.Length != d.Params.Length)
					return ecd.Raise("Different parameter count", Location);

				IEnumerator<Param> pe = Params.AsEnumerable().GetEnumerator();

				foreach (var p in Params)
				{
					pe.MoveNext();
					if (!p.IsParamEqual(pe.Current))
						return ecd.Raise("Different parameters", Location);
				}

				if (RootStatement != null && RootStatement.ExprCompare(ecd, d.RootStatement) == ExprCompareData.Result.False)
					return ecd.Raise("Different definition", Location);

				return ExprCompareData.Result.True;
			}
		}

		public class ModuleReference : Expressable
		{
			public String[] Paths;
			public String ModuleName;

			public static ModuleReference Create(List<String> path)
			{
				if (path.Count == 0)
					throw new Exception("ModuleReference.Create needs more than one string elements.");
				var m = new ModuleReference();
				m.ModuleName = path[path.Count - 1];
				m.Paths = path.GetRange(0, path.Count - 1).ToArray();
				return m;
			}

			public override String ToString()
			{
				StringBuilder sb = new StringBuilder();
				foreach (String s in Paths)
				{
					sb.Append(s);
					sb.Append("\\");
				}
				sb.Append(ModuleName);
				return sb.ToString();
			}

			public ExprCompareData.Result CompareReference(ModuleReference b)
			{
				if (!ModuleName.Equals(b.ModuleName, StringComparison.OrdinalIgnoreCase))
					return ExprCompareData.Result.False;
				if (Paths.Length != b.Paths.Length)
					return ExprCompareData.Result.False;
				IEnumerator<String> e = b.Paths.AsEnumerable().GetEnumerator();
				foreach (String p in Paths)
				{
					e.MoveNext();
					if (!p.Equals(e.Current, StringComparison.OrdinalIgnoreCase))
						return ExprCompareData.Result.False;
				}
				return ExprCompareData.Result.True;
			}

			public override String Express(int indentLevel)
			{
				return this.ToString().Indent(indentLevel);
			}
		}

		public class DeclaredConstant : Expressable
		{
			public String Name;
			/// <summary>
			/// This value is the final evaluation result
			/// </summary>
			public Expression Value;
			/// <summary>
			/// Expressive value is the value regurgitated
			/// </summary>
			public Expression ExpressiveValue;
			public Runtime.VarType? ResolvedType = null;
			public Boolean IsTruple = false;
			public Text.DocLocation Location;

			public override string Express(int indentLevel)
			{
				return "{0} = {1}".ExprFmt(Name, ExpressiveValue).Indent(indentLevel);
			}

			public Boolean CompareDeclConstant(ExprCompareData ecd, DeclaredConstant b)
			{
				return b.Name == Name && Value.ExprCompare(ecd, b.Value) == ExprCompareData.Result.True;
			}
		}


		public abstract class Directive : Declaration
		{
			public readonly String Name;
			public DirectiveTypes DirectiveType;
			public enum DirectiveTypes
			{
				Import,
				Declare,
				ListlessDeclare
			}

			public Directive()
			{
				throw new NotImplementedException();
			}

			public Directive(String name, DirectiveTypes type)
				: base(DeclarationTypes.Directive)
			{
				this.Name = name;
				this.DirectiveType = type;
			}

			public abstract String ExpressInternal();
			public sealed override string Express(int indentLevel)
			{
				return "#{0} {1};".Fmt(this.Name, this.ExpressInternal()).Indent(indentLevel);
			}

			public ImportDirective AsImport() { return this as ImportDirective; }
			public DeclarativeDirective AsDeclarative() { return this as DeclarativeDirective; }
			public ListlessDelcarativeDirective AsListless() { return this as ListlessDelcarativeDirective; }

			public override ExprCompareData.Result CompareDecl(ExprCompareData ecd, Declaration decl)
			{
				var d = decl.AsDirective();
				if (base.CompareDecl(ecd, decl) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				if (DirectiveType != d.DirectiveType)
					return ecd.Raise("Different directive types", Location);
				if (Name != d.Name)
					return ecd.Raise("Different directive name", Location);
				return ExprCompareData.Result.True;
			}
		}

		public class ImportDirective : Directive
		{
			public ModuleReference Reference;
			public ImportDirective()
			{
				throw new NotImplementedException();
			}
			public ImportDirective(String name, ModuleReference reference)
				: base(name, DirectiveTypes.Import)
			{
				this.Reference = reference;
			}

			public override string ExpressInternal()
			{
				return Reference.Express(0);
			}

			public override ExprCompareData.Result CompareDecl(ExprCompareData ecd, Declaration decl)
			{
				if (base.CompareDecl(ecd, decl) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				var id = decl.AsDirective().AsImport();
				if (Reference.CompareReference(id.Reference) == ExprCompareData.Result.False)
					return ecd.Raise("Different references", Location);
				return ExprCompareData.Result.True;
			}
		}

		public class DeclarativeDirective : Directive
		{
			public List<Constant> Data = new List<Constant>();
			public DeclarativeDirective(String name)
				: base(name, DirectiveTypes.Declare)
			{
			}

			public override string ExpressInternal()
			{
				return "({0})".Fmt(Data.JoinExt(", ", c => c.Express(0)));
			}

			public override ExprCompareData.Result CompareDecl(ExprCompareData ecd, Declaration decl)
			{
				if (base.CompareDecl(ecd, decl) == ExprCompareData.Result.False)
					return ExprCompareData.Result.False;
				var dd = decl.AsDirective().AsDeclarative();
				if (Data.Count != dd.Data.Count)
					return ecd.Raise("Different parameter list count", Location);
				IEnumerator<Constant> e = dd.Data.AsEnumerable().GetEnumerator();
				foreach (var d in Data)
				{
					e.MoveNext();
					if (!d.EqualsConstant(e.Current))
						return ecd.Raise("Different parameter list items", Location);
				}
				return ExprCompareData.Result.True;
			}
		}

		public class ListlessDelcarativeDirective : Directive
		{
			public ListlessDelcarativeDirective(String name)
				: base(name, DirectiveTypes.ListlessDeclare)
			{
			}

			public override string ExpressInternal()
			{
				return "";
			}
		}

		public abstract class Declaration : Expressable
		{
			public Text.DocLocation Location;
			public DeclarationTypes DeclarationType;
			public enum DeclarationTypes
			{
				Directive,
				Function
			}
			public Declaration()
			{
				throw new NotImplementedException();
			}
			public Declaration(DeclarationTypes type)
			{
				DeclarationType = type;
			}

			public virtual ExprCompareData.Result CompareDecl(ExprCompareData ecd, Declaration decl)
			{
				if (decl.DeclarationType != DeclarationType)
					return ecd.Raise("Different declaration type", Location);
				return ExprCompareData.Result.True;
			}

			public FunctionDefinition AsFunctionDefinition()
			{
				return this as FunctionDefinition;
			}

			public Directive AsDirective()
			{
				return this as Directive;
			}
		}

		public class FunctionEnumerator : IEnumerator<FunctionDefinition>, IEnumerable<FunctionDefinition>
		{
			public Declaration[] Definitions;
			public FunctionDefinition _Current = null;
			public int CurrentIndex = -1;

			public void Dispose()
			{
			}

			public IEnumerator<FunctionDefinition> GetEnumerator()
			{
				return this;
			}

			IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return this;
			}

			public FunctionEnumerator(Declaration[] decls)
			{
				Definitions = decls;
			}

			Boolean System.Collections.IEnumerator.MoveNext()
			{
				while (++CurrentIndex < Definitions.Length
					&& Definitions[CurrentIndex].DeclarationType != Declaration.DeclarationTypes.Function) ;
				if (CurrentIndex >= Definitions.Length)
					return false;
				_Current = Definitions[CurrentIndex] as FunctionDefinition;
				return true;
			}

			Object System.Collections.IEnumerator.Current
			{
				get
				{
					return _Current;
				}
			}

			public FunctionDefinition Current
			{
				get
				{
					return _Current;
				}
			}

			void System.Collections.IEnumerator.Reset()
			{
				CurrentIndex = -1;
			}
		}

		public class UnaryFunctionDef
		{
			public String Keyword;
			public Boolean HasArg;
			public UnaryFunctionTypes UnaryType;
		}

		//TODO: Derive this class and made a declarative version
		//Extra members will include global variables.
		//Oh and dont forget that the global variable directives
		//must not be used in non-declarative files.
		public class CodeDOMModule : Expressable
		{
			public String Name, FullPath = null;
			public ImportDirective[] Imports;
			public Declaration[] Declarations;
			public DeclaredConstant[] Constants;
			public VariableReference[] GlobalVariables;
			public Boolean IsDeclarative;

			public FunctionEnumerator GetDefinitions()
			{
				return new FunctionEnumerator(Declarations);
			}

			public override String Express(int indentLevel)
			{
				StringBuilder sb = new StringBuilder();
				sb.AppendLine("// Automatically generated from CheeseFAKKU GSCCompiler CodeDOM.");
				sb.AppendLine("// Script file: {0}".Fmt(Name));

				sb.AppendLine("// ##### Import Directives #####");
				foreach (var import in Imports)
					sb.AppendLine(import.Express(indentLevel));
				sb.AppendLine();
				sb.AppendLine();
				sb.AppendLine("// ##### Declared Constants #####");
				foreach (var c in Constants)
					sb.AppendLine(c.Express(indentLevel) + ";");
				sb.AppendLine();
				sb.AppendLine();
				sb.AppendLine("// ##### Function/Directive Declarations #####");
				foreach (var fd in Declarations)
					sb.AppendLine(fd.Express(indentLevel));

				sb.AppendLine("// ##### End of File #####");
				return sb.ToString();
			}

			public override string ToString()
			{
				int funcs = 0;
				foreach (var d in Declarations)
					if (d.DeclarationType == Declaration.DeclarationTypes.Function)
						++funcs;
				return "Module:{0} Includes: {1} Constants: {2} Functions: {3} Misc Decl: {4}"
					.Fmt(Name, Imports.Length, Constants.Length, funcs, Declarations.Length - funcs);
			}
		}

		#region Enumerations

		public enum ConditionalTypes
		{
			BiConditional,
			Loop,
			ObjectLoop,
			LoopEx,
			MultiConditional,
			Converse,
			/*ConverseConditional,*/
		}

		public enum ConditionKeyword
		{
			If,
			Else,
			For,
			ForEeach,
			While
		}

		public enum NamedConstantTypes
		{
			True,
			False,
			Infinity,
			Undefined
		}

		public enum DirectiveType
		{
			Include
		}

		public enum BinaryOperationType
		{
			BoolAnd,//a && b
			BoolOr,//a || b
			BoolEquals,//a == b
			BoolNotEquals,//a != b
			BoolLess,//a < b
			BoolGreater,//a > b
			BoolLessEquals,//a <= b
			BoolGreaterEquals,//a >= a
			Assign,// a = b
			Addition, //a + b
			Subtraction,//a - b
			Division,//a / b
			Multiplication,//a * b
			BitAND,//a & b
			BitOR,//a | b
			BitXOR,//a ^ b
			BitShiftRight, // a >> b
			BitShiftLeft, // a << b
			Modulo,//a % b
		}

		public enum UnaryOperationType
		{
			Positive,//+a
			Negative,//-b
			Invert,//~
			Not,//!b
			PostIncrement,
			PostDecrement
		}

		public enum UnaryFunctionTypes
		{
			Wait,
			WaitTillFrameEnd,
			Return
		}

		public enum FlowControlTypes
		{
			Break,
			Continue
		}

		public enum CallModifiers
		{
			Thread,
			ChildThread,
			Volatile,
			Call
		}

		public enum SwitchKeywords
		{
			Case,
			Default
		}

		public enum InternalDirectives
		{
			DeclarativeGlobal,
			AddGlobalVariable
		}

		public enum ParamModifiers
		{
			Reference,
			VariableArguments,
			Minimum
		}

		#endregion
	}
}
