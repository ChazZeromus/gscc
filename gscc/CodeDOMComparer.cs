using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameScriptCompiler.CodeDOM;

namespace GameScriptCompiler
{
	public class ExprCompareData
	{
		public enum Result
		{
			True,
			False
		}
		public List<Set> UneqList = new List<Set>();
		public class Set
		{
			public Boolean NoOperands = false;
			public String Info;
			public Expression A = null, B = null;
			public Text.DocLocation Location;

			public override string ToString()
			{
				if (NoOperands)
					return "{0} {1}".Fmt(Info, Location);
				return "{0} ({1} {2})!=({3} {4})".Fmt(Info, A, A.Location, B, B.Location);
			}
		}

		public List<Set> RaiseSingle(String info)
		{
			UneqList.Add(new Set()
			{
				Info = info,
				A = null,
				B = null,
				NoOperands = true
			});
			return UneqList;
		}

		public List<Set> RaiseSingle(String info, Text.DocLocation location)
		{
			UneqList.Add(new Set()
			{
				Info = info,
				A = null,
				B = null,
				NoOperands = true,
				Location = location
			});
			return UneqList;
		}

		public Result Raise(String info, Text.DocLocation location)
		{
			UneqList.Add(new Set()
			{
				Info = info,
				A = null,
				B = null,
				NoOperands = true,
				Location = location
			});
			return Result.False;
		}
		public Result Raise(String info, Expression a, Expression b)
		{
			UneqList.Add(new Set() { Info = info, A = a, B = b });
			return Result.False;
		}
		public Result Raise(Expression a, Expression b)
		{
			UneqList.Add(new Set() { Info = "{0} != {1}".Fmt(a, b), A = a, B = b });
			return Result.False;
		}
		public static Boolean AreNull(Object a, Object b)
		{
			return a == null && b == null;
		}
		public Result NullCompare(Expression a, Expression b)
		{
			if (AreNull(a, b))
				return Result.True;
			if (a == null || b == null)
				return this.Raise("A or B is null.", a, b);
			return a.ExprCompare(this, b);
		}
	}
	public class CodeDOMComparer
	{
		/// <summary>
		/// Compares two CodeDOM graphs. Will list differences according to scope
		/// of scope. i.e. root, declaration, function definition, statement, constant, etc.
		/// </summary>
		/// <param name="a">Operand A</param>
		/// <param name="b">Operand B</param>
		/// <returns>Returns a list of differences, an empty list means both modules are equal in code graphs.</returns>
		public static List<ExprCompareData.Set> CompareDOM(CodeDOMModule a, CodeDOMModule b)
		{
			ExprCompareData ecd = new ExprCompareData();
			/*if (!a.Name.Equals(b.Name, StringComparison.OrdinalIgnoreCase))
				ecd.RaiseSingle("Different module name");*/

			if (a.Imports.Length != b.Imports.Length)
				return ecd.RaiseSingle("Different import count ({0}) ({1})".Fmt(a.Imports.Length, b.Imports.Length));

			IEnumerator<ImportDirective> ie = b.Imports.AsEnumerable().GetEnumerator();
			foreach (var i in a.Imports)
			{
				ie.MoveNext();
				if (i.CompareDecl(ecd, ie.Current) == ExprCompareData.Result.False)
					return ecd.RaiseSingle("Different import directive ({0}) ({1})"
						.Fmt(i.Reference, ie.Current.Reference));
			}

			if (a.Imports.Length != b.Imports.Length)
				return ecd.RaiseSingle("Different import lengths ({0}) ({1})"
					.Fmt(a.Constants.Length, b.Imports.Length));

			IEnumerator<DeclaredConstant> de = b.Constants.AsEnumerable().GetEnumerator();

			foreach (var c in a.Constants)
			{
				de.MoveNext();
				if (!c.CompareDeclConstant(ecd, de.Current))
					return ecd.RaiseSingle("Different constants ({0}) ({1})"
						.Fmt(c.Value, de.Current.Value), c.Location);
			}

			if (a.Declarations.Length != b.Declarations.Length)
				return ecd.RaiseSingle("Different declaration lengths ({0}) ({1})"
					.Fmt(a.Declarations.Length, b.Declarations.Length));

			IEnumerator<Declaration> ed = b.Declarations.AsEnumerable().GetEnumerator();

			foreach (var d in a.Declarations)
			{
				ed.MoveNext();
				if (d.CompareDecl(ecd, ed.Current) == ExprCompareData.Result.False)
					return ecd.RaiseSingle("Different function/directive declarations");
			}

			return ecd.UneqList;
		}
	}
}
