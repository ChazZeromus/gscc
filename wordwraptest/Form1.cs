using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace wordwraptest
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		public static String FormatWordWrapIndent(String paragraph, int maxChars, int indentLevel)
		{
			StringBuilder final = new StringBuilder();
			StringBuilder currentLine = new StringBuilder(), currentWord = new StringBuilder();
			int currentWidth = 0;
			if (maxChars <= 3)
				throw new Exception("Attempted to word wrap to a width of less than three.");
			int wordsAdded = 0;
			Boolean success = true, ignoreWhitespace = true, manualBreak = false;
			for (CharEnumerator ce = paragraph.GetEnumerator(); ; )
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

		private void textBox1_TextChanged(object sender, EventArgs e)
		{
			textBox2.Text = FormatWordWrapIndent(textBox1.Text, 35, 1);
		}
	}

	public static class Extensions
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
