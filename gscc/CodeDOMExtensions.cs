using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using GameScriptCompiler.CodeDOM;

namespace GameScriptCompiler
{
	public static partial class Extensions
	{
		public static Boolean IsPrimitive(this Runtime.VarType type)
		{
			return type <= Runtime.VarType.Bool;
		}

		public static String ExprFmt(this String format, params Object[] args)
		{
			List<Object> tempList = new List<object>();
			foreach (Object obj in args)
				if (obj != null)
					if (obj is Expressable)
						tempList.Add((obj as Expressable).Express(0));
					else
						tempList.Add(obj);
				else
					tempList.Add("");
			return String.Format(format, tempList.ToArray());
		}

		/// <summary>
		/// Note that this method treats identifiers as potential named constants.
		/// So extra checking must be done in the identifier handler.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static Boolean IsTokenConstant(this Tokenization.TokenType type)
		{
			return type == Tokenization.TokenType.Identifier || type == Tokenization.TokenType.MetaString
				|| type == Tokenization.TokenType.Number || type == Tokenization.TokenType.String ||
				type == Tokenization.TokenType.ResourceVar;
		}

		public static Boolean IsBooleanWise(this BinaryOperationType type)
		{
			return type <= BinaryOperationType.BoolGreaterEquals;
		}

		public static Boolean IsBitWise(this BinaryOperationType type)
		{
			return type >= BinaryOperationType.BitAND && type <= BinaryOperationType.BitShiftLeft;
		}

		/// <summary>
		/// Whether it's not an else.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static Boolean IsConditionConstruct(this ConditionalTypes type)
		{
			return type <= ConditionalTypes.MultiConditional;
		}

		public static Boolean IsSingleConditionConstruct(this ConditionalTypes type)
		{
			return type.IsConditionConstruct() && type != ConditionalTypes.ObjectLoop && type != ConditionalTypes.LoopEx;
		}

		public static Runtime.VarType GetNamedConstantType(this NamedConstantTypes type)
		{
			if (type == NamedConstantTypes.False || type == NamedConstantTypes.True)
				return Runtime.VarType.Bool;
			if (type == NamedConstantTypes.Infinity)
				return Runtime.VarType.Float;
			if (type == NamedConstantTypes.Undefined)
				return Runtime.VarType.Undefined;
			throw new NotImplementedException("Could not return the VarType of request NamedConstantType.");
		}

		public static Boolean IsModifierExclusive(this CallModifiers type)
		{
			switch (type)
			{
				case CallModifiers.Thread:
					return true;
				case CallModifiers.Volatile:
					return true;
				case CallModifiers.ChildThread:
					return true;
				case CallModifiers.Call:
					return true;
			}
			return false;
		}

		public static Boolean AvailableInParseMode(this CallModifiers type, ParseModes mode)
		{
			switch (type)
			{
				case CallModifiers.Thread:
					return true;
				case CallModifiers.ChildThread:
					return mode == ParseModes.Singleplayer;
				case CallModifiers.Volatile:
					return true;
				case CallModifiers.Call:
					return mode == ParseModes.Singleplayer;
			}
			throw new NotImplementedException();
		}

		public static Boolean IsPostfixUnary(this UnaryOperationType type)
		{
			return type >= UnaryOperationType.PostIncrement;
		}

		public static Boolean IsConditionalStatement(this ConditionalTypes type)
		{
			return type < ConditionalTypes.Converse;
		}

		public static Boolean IsFlowable(this ConditionalTypes type, Boolean noMulti)
		{
			return type == ConditionalTypes.Loop || type == ConditionalTypes.LoopEx || type == ConditionalTypes.ObjectLoop
				 || (!noMulti && type == ConditionalTypes.MultiConditional);
		}

		public static Boolean IsIndexable(this Statement.StatementTypes type)
		{
			return type == Statement.StatementTypes.Assignment ||
				type == Statement.StatementTypes.FunctionCall ||
				type == Statement.StatementTypes.PointerCall;
		}

		public static Boolean IsIndexable(this Expression.ExpressionTypes type)
		{
			return type != Expression.ExpressionTypes.UnaryOperator;
		}

		public static Boolean IsIndexable(this Reference.ReferenceTypes type)
		{
			return type != Reference.ReferenceTypes.Function;
		}

		public static Boolean IsIndexable(this Runtime.VarType type)
		{
			return type == Runtime.VarType.String || type == Runtime.VarType.Array || type == Runtime.VarType.Truple;
		}

		public static Boolean IsNumber(this Runtime.VarType type)
		{
			return type == Runtime.VarType.Integer ||
				type == Runtime.VarType.Float;
		}

		public static Boolean IsConstantResolvable(this Expression.ExpressionTypes type)
		{
			return type == Expression.ExpressionTypes.Group || type == Expression.ExpressionTypes.Constant ||
				type == Expression.ExpressionTypes.BinaryOperator;
		}

		public static StatementCollection ToStatementCollection(this List<Statement> statements)
		{
			return new StatementCollection(statements.ToArray());
		}

		public static Runtime.VarType GetTokenConstantVarType(this Tokenization.TokenType type, Runtime.VarType preferredNumber = Runtime.VarType.Integer)
		{
			if (type == Tokenization.TokenType.MetaString)
				return Runtime.VarType.MetaString;
			else if (type == Tokenization.TokenType.Number)
				return preferredNumber;
			else if (type == Tokenization.TokenType.String)
				return Runtime.VarType.String;
			else if (type == Tokenization.TokenType.ResourceVar)
				return Runtime.VarType.ResourceVar;
			else
				throw new NotImplementedException();
		}

		/// <summary>
		/// Finds it or null!
		/// </summary>
		/// <param name="constants"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static DeclaredConstant LookUpConstant(this IEnumerable<DeclaredConstant> constants, String name)
		{
			return constants.FirstOrDefault(dc => dc.Name == name);
		}

		public static Boolean IsAssignable(this Expression expr)
		{
			return (expr.IsReference() && !expr.IsReference(Reference.ReferenceTypes.Function));
		}

		public static IndexEnumerator<T> AsIndexable<T>(this ICollection<T> c)
		{
			return new IndexEnumerator<T>(c);
		}
	}

	public struct IndexTruple<T>
	{
		public T Value;
		public int Index, Total;
	}

	public class IndexEnumerator<T> : IEnumerator<IndexTruple<T>>, IEnumerable<IndexTruple<T>>
	{
		IEnumerator<T> _Internal;
		IndexTruple<T> _Current ;
		readonly int Total;

		public IndexEnumerator(ICollection<T> c)
		{
			_Internal = c.AsEnumerable().GetEnumerator();
			Total = c.Count;
			_Current.Index = -1;
			_Current.Total = Total;
			_Current.Value = default(T);
		}

		public IndexTruple<T> Current
		{
			get { return _Current; }
		}

		public void Dispose()
		{
		}

		object System.Collections.IEnumerator.Current
		{
			get { return _Current; }
		}

		public bool MoveNext()
		{
			if (!_Internal.MoveNext())
				return false;
			++_Current.Index;
			_Current.Value = _Internal.Current;
			return true;
		}

		public void Reset()
		{
			_Current.Index = -1;
			_Current.Value = default(T);
			_Internal.Reset();
		}

		public IEnumerator<IndexTruple<T>> GetEnumerator()
		{
			return this;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this;
		}
	}

}
