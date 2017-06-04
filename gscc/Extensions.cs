using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GameScriptCompiler
{
	public static partial class Extensions
	{
		public static String Fmt(this String format, params Object[] args)
		{
			return String.Format(format, args);
		}

		public static Byte[] ToBytes(this String input)
		{
			return Encoding.UTF8.GetBytes(input);
		}

		public static String Abridge(this String input, int maxCharacters = 10)
		{
			if (maxCharacters < 3)
				maxCharacters = 3;
			if (input.Length <= 3)
				return input;
			return input.Length > maxCharacters ? input.Substring(0, maxCharacters - 3) + "..." : input;
		}

		public static String RemoveExcessiveWhitespace(this String input)
		{
			var r = new Regex("\\x09|\\x0a|\\x0b|\\x08|\\x0c", RegexOptions.Multiline);
			return r.Replace(input, m =>
			{
				switch (m.Value)
				{
					case "\t":
						return "\\t";
					case "\n":
						return "\\n";
					case "\v":
						return "\\v";
					case "\b":
						return "\\b";
					case "\f":
						return "\\f";
				}
				return m.Value;
			});
		}

		public static String Indent(this String str, int indentLevel)
		{
			StringBuilder sb = new StringBuilder();
			sb.Indent(indentLevel);
			sb.Append(str);
			return sb.ToString();
		}

		public static void Indent(this StringBuilder stringBuilder, int indentLevel)
		{
			stringBuilder.Append('\t', indentLevel);
		}

		public static List<TDest> CreateFromList<TSource, TDest>(this IEnumerable<TSource> target, Func<TSource, TDest> pred)
		{
			List<TDest> nl = new List<TDest>();
			foreach (TSource t in target)
				nl.Add(pred(t));
			return nl;
		}
		public static String JoinExt<T>(this IEnumerable<T> list, String separator, Func<T, String> getString)
		{
			List<String> strlist = new List<String>();
			foreach (T t in list)
				strlist.Add(getString(t));
			return String.Join(separator, strlist);
		}
	}
}
