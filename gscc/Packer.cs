using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameScriptCompiler.Tokenization;
using System.IO;

namespace GameScriptCompiler
{
	public interface Testtest<in T> {

	}
	public class Packer
	{
		private delegate String TokenStrFunc(String tok);
		private static Dictionary<TokenType, TokenStrFunc> map = null;
		private static void InitMap()
		{
			if (map != null)
				return;
			map = new Dictionary<TokenType, TokenStrFunc>();
			map[TokenType.String] = Token.AsString;
			map[TokenType.Identifier] = Token.AsRaw;
			map[TokenType.MetaString] = Token.AsMetaString;
			map[TokenType.Number] = Token.AsRaw;
			map[TokenType.ResourceVar] = Token.AsResourceVar;
			map[TokenType.Symbol] = Token.AsRaw;
			foreach (TokenType tt in Enum.GetValues(typeof(TokenType)))
				if (!map.ContainsKey(tt))
					map[tt] = null;
		}
		private static String TokenToStr(Token t)
		{
			TokenStrFunc tsf = map[t.Type];
			if (tsf == null)
				return "";
			return tsf(t.Content);
		}
		/// <summary>
		/// Packs tokens from source and minimizes whitespace. Yes it will exclude comments.
		/// </summary>
		/// <param name="tokens"></param>
		/// <param name="output"></param>
		public static void PackToMinimumWhitespace(Token[] tokens, StreamWriter output)
		{
			InitMap();
			/*
			 * In skeptic mode, the last token was an identifier and we're on some whitespace.
			 * If the next token is an identifier or number, we mandatorily have to space it.
			 */
			Boolean skeptic = false, skepticsymbol = false;
			Token last = null; //Last token
			Token lastsig = null;//Last significant non-whitespace/non-comment token
			foreach (Token t in tokens)
			{
				if (!skeptic)
					if (t.Type == TokenType.WhiteSpace || t.Type == TokenType.Comment)
					{
						if (lastsig != null && lastsig.IsSymbol() && "+-".Contains(lastsig.Content))
							skepticsymbol = true;
						if (lastsig != null && lastsig.IsIdentifier() || skepticsymbol)
							skeptic = true;
					}
					else
						if (!t.IsIdentifier() || lastsig == null || !lastsig.IsSymbol("%"))
							output.Write(TokenToStr(t));
						else
							output.Write(" {0}".Fmt(t.Content)); //resource vars need a space.
				else//We had a nonsig already and lastsig should be the last sig token.
					if (t.Type != TokenType.WhiteSpace && t.Type != TokenType.Comment)
					{
						//wait 0.5
						if (!skepticsymbol && (t.Type == TokenType.Identifier || t.Type == TokenType.Number)
							|| skepticsymbol && t != null && t.IsSymbol() && "+-".Contains(t.Content))
							output.Write(" {0}", TokenToStr(t));
						else
							output.Write(TokenToStr(t));
						skeptic = skepticsymbol =false;
					}
				last = t;
				if (t.Type != TokenType.WhiteSpace && t.Type != TokenType.Comment)
					lastsig = t;
			}
		}
	}
}
