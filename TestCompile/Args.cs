using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using GameScriptCompiler;

namespace TestCompile
{
	public class Args
	{
		public enum ArgResult
		{
			IncompleteOrInvalid,
			Success
		}

		public class ArgDesc : Attribute
		{
			public String Desc;
			public String ArgName;
			public Boolean MustBeSet = false;
		}

		public static Dictionary<String, FieldInfo> OrganizeFields(Dictionary<String, FieldInfo> fields)
		{
			return fields.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
		}

		public static Dictionary<String, FieldInfo> GetFieldsFromPrefix<T>(String prefix)
		{
			return OrganizeFields(GetFieldsFromPrefix(prefix, typeof(T)));
		}

		public static Dictionary<String, FieldInfo> GetFieldsFromPrefix(String prefix, Type type)
		{
			Dictionary<String, FieldInfo> fields = new Dictionary<string, FieldInfo>();
			foreach (var fi in type.GetFields())
				if (fi.Name.StartsWith(prefix))
					fields.Add(fi.Name.Substring(prefix.Length), fi);
			return OrganizeFields(fields);
		}

		public static Dictionary<String, FieldInfo> GetFieldsFromAttr<T>(Type type)
			where T : Attribute
		{
			Dictionary<String, FieldInfo> fields = new Dictionary<string, FieldInfo>();
			foreach (var fi in type.GetFields())
				if (fi.GetCustomAttributes(typeof(T), true).Length > 0)
					fields.Add(fi.Name, fi);
			return OrganizeFields(fields);
		}

		public static ArgResult SetArgsFromPrefix(String prefix, Type type, Object obj, String[] args, ref List<String> unaccounted)
		{
			return SetArgs(obj, args, ref unaccounted,
				GetFieldsFromPrefix(prefix, type));
		}

		public static ArgResult SetArgsFromAttr<T>(Type type, Object obj, String[] args, ref List<String> unaccounted)
			where T : Attribute
		{
			return SetArgs(obj, args, ref unaccounted,
				GetFieldsFromAttr<T>(type));
		}

		public static ArgResult SetArgs(Object obj, String[] args, ref List<String> unaccounted,
			Dictionary<String, FieldInfo> fields)
		{
			List<String> mbs = new List<string>();
			foreach (var f in fields)
				if (GetMustBeSet(f.Value))
					mbs.Add(f.Key);
			FieldInfo @for = null;
			foreach (var a in args)
			{
				if (@for == null)
					if (a.StartsWith("/"))
					{
						String p = a.Substring(1);
						if (fields.ContainsKey(p))
						{
							if (mbs.Contains(p))
								mbs.Remove(p);
							if (fields[p].FieldType == typeof(Boolean))
								fields[p].SetValue(obj, true);
							else
								@for = fields[p];
						}
						else
							return ArgResult.IncompleteOrInvalid;
					}
					else
						unaccounted.Add(a);
				else
				{
					@for.SetValue(obj, a);
					@for = null;
				}
			}
			return @for != null ? ArgResult.IncompleteOrInvalid : (mbs.Count == 0 ? ArgResult.Success : ArgResult.IncompleteOrInvalid);
		}

		private static String GetDesc(FieldInfo fi)
		{
			var res = fi.GetCustomAttributes(typeof(ArgDesc), true);
			if (res.Length <= 0)
				return "";
			return (res[0] as ArgDesc).Desc;
		}

		private static String GetArgName(FieldInfo fi)
		{
			var res = fi.GetCustomAttributes(typeof(ArgDesc), true);
			if (res.Length <= 0)
				return "value";
			var name = (res[0] as ArgDesc).ArgName;
			return name == "" ? "value" : name;
		}

		private static Boolean GetMustBeSet(FieldInfo fi)
		{
			var res = fi.GetCustomAttributes(typeof(ArgDesc), true);
			if (res.Length <= 0)
				return false;
			return (res[0] as ArgDesc).MustBeSet;
		}

		public static String GetUsage(Dictionary<String, FieldInfo> fields, Object obj)
		{
			StringBuilder sb = new StringBuilder("Usage:");
			foreach (var f in fields)
			{
				var mbs = GetMustBeSet(f.Value);
				sb.Append(" ");
				sb.Append("{0}/{1}".Fmt(mbs ? "" : "[", f.Key));
				if (f.Value.FieldType != typeof(Boolean))
					sb.Append(" <{0}>{1}".Fmt(GetArgName(f.Value), mbs ? "" : "]"));
				else
					sb.Append(mbs ? "" : "]");
			}

			sb.AppendLine();
			sb.AppendLine();

			foreach (var f in fields)
			{
				sb.Append("/{0}".Fmt(f.Key).Indent(1));
				if (f.Value.FieldType != typeof(Boolean))
					sb.Append(" <{0}>".Fmt(GetArgName(f.Value)));
				sb.AppendLine();
				sb.Append(WordWrapString(GetDesc(f.Value), 40, 2));
				if (f.Value.FieldType != typeof(Boolean))
				{
					Object v = f.Value.GetValue(obj);
					if (v != null)
						sb.AppendLine("Default: {0}".Fmt(v).Indent(2));
				}
				sb.AppendLine();
			}

			return sb.ToString();
		}

		/// <summary>
		/// Word wraps a string based on the maximum character lengther
		/// per line with optional indentation.
		/// </summary>
		/// <param name="str">String to wordwrap and indent.</param>
		/// <param name="maxChars">Maximum characters per line, must be greater than 1.</param>
		/// <param name="indentLevel">Amount of tab indentations.</param>
		/// <returns></returns>
		public static String WordWrapString(String str, int maxChars, int indentLevel = 0)
		{
			StringBuilder final = new StringBuilder();
			StringBuilder currentLine = new StringBuilder(), currentWord = new StringBuilder();
			int currentWidth = 0;
			if (maxChars <= 1)
				throw new Exception("Attempted to word wrap with invalid length.");
			int wordsAdded = 0;
			Boolean success = true, ignoreWhitespace = true, manualBreak = false;
			for (CharEnumerator ce = str.GetEnumerator(); ; )
			{
				if (success && (success = ce.MoveNext()))
				{
					if (!Char.IsWhiteSpace(ce.Current) || !ignoreWhitespace)
					{
						if (ignoreWhitespace)
							ignoreWhitespace = false;
						if (ce.Current != '\n')
						{
							//Append current word
							currentWord.Append(ce.Current);
							++currentWidth;
							//Commit current word to current line and clear word
							if (Char.IsWhiteSpace(ce.Current) || ce.Current == '-')
							{
								currentLine.Append(currentWord);
								++wordsAdded;
								currentWord.Clear();
							}
						}
						else//Forcefully commit the current line and words
						{
							manualBreak = true;
							if (currentWidth < maxChars && currentWord.Length > 0)
							{
								currentLine.Append(currentWord);
								++wordsAdded;
								currentWord.Clear();
							}
						}
					}
				}
				else
					if (currentWidth < maxChars && currentWord.Length > 0)
					{
						currentLine.Append(currentWord);
						++wordsAdded;
						currentWord.Clear();
					}

				if (currentWidth >= maxChars || !success || manualBreak)
				{
					//Add the conformant line, then trim any whitespace of the current word.
					if (wordsAdded > 0)
					{
						final.AppendLine(currentLine.ToString().TrimEnd().Indent(indentLevel));
						currentLine.Clear();
						String oldword = currentWord.ToString().TrimStart();
						currentWord.Clear();
						currentWord.Append(oldword);
						currentWidth = currentWord.Length;
						if (currentWord.Length == 0)
							ignoreWhitespace = true;
						wordsAdded = 0;
					}

					//Break up any huge words
					while (currentWidth >= maxChars)
					{
						int maxpt = Math.Min(maxChars - 1, currentWord.Length);
						String left = currentWord.ToString().Substring(0, maxpt),
							right = currentWord.ToString().Substring(maxpt);
						currentWord.Clear();
						currentWord.Append(right);
						currentWidth = right.Length;
						final.AppendLine("{0}-".Fmt(left).Indent(indentLevel));
					}

					if (manualBreak)
					{
						manualBreak = false;
						final.AppendLine();
					}
				}

				if (!success && wordsAdded == 0)
					break;
			}

			if (currentWord.Length > 0)
				final.AppendLine(currentWord.ToString());

			return final.ToString();
		}
	}
}
