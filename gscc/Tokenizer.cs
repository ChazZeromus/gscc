using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.Specialized;
using GameScriptCompiler.Text;
using GameScriptCompiler.CodeDOM;

namespace GameScriptCompiler
{
	namespace Tokenization
	{
		public enum TokenType
		{
			ResourceVar,
			MetaString,
			String,
			Symbol,
			Comment,
			Identifier,
			Number,
			WhiteSpace,
			EOF
		}

		public class Token : BackingObject
		{
			public TokenType Type;
			public DocLocation Location;
			public String Content;


			public static String AsResourceVar(String tok)
			{
				return "%{0}".Fmt(tok);
			}

			public static String AsString(String tok)
			{
				return "\"{0}\"".Fmt(Constant.GetFormattedString(tok));
			}

			public static String AsMetaString(String tok)
			{
				return "&{0}".Fmt(AsString(tok));
			}

			public static String AsCommentSingle(String tok)
			{
				return "//{0}\n".Fmt(tok);
			}

			public static String AsCommentRange(String tok)
			{
				return "/*{0}*/".Fmt(tok);
			}

			public static String AsRaw(String tok)
			{
				return tok;
			}


			public override string ToString()
			{
				return "\"{1}\" ({0}) {2}".Fmt(Type.ToString(), Content.RemoveExcessiveWhitespace(), Location);
			}

			public Boolean IsIdentifier()
			{
				return Type == TokenType.Identifier;
			}

			public Boolean IsIdentifier(String identifier)
			{
				return IsIdentifier() && this.Content == identifier;
			}

			public Boolean IsSymbol(String symbol)
			{
				return Type == TokenType.Symbol && Content == symbol;
			}

			public Boolean IsSymbol()
			{
				return Type == TokenType.Symbol;
			}

			public Boolean IsString(String str)
			{
				return Type == TokenType.String && Content == str;
			}
		}

		public class NumberToken : Token
		{
			public Boolean HasNegative, HasDecimalPoint, IsDecimalPointLeading;
		}

		public enum FeedResultType
		{
			NotDone,
			NewToken
		}

		public class FeedResult
		{
			public FeedResultType Type = FeedResultType.NotDone;
			public Token Token = null;
			public FeedResult Set(FeedResultType type)
			{
				this.Type = type;
				return this;
			}
		};
		public class Tokenizer
		{
			private Context context;
			private Dictionary<Context.ModeType, Action<CharType>> modeEvents = new Dictionary<Context.ModeType,Action<CharType>>();
			CompilerOptions Options = new CompilerOptions();

			class Context
			{
				public TextCharReader Tcr = null;
				//If error is ever true, then IsDone is invalid.
				public Boolean Error = false, IsDone = false;
				public String ErrorMessage = "";
				public DocLocation ErrorLocation;
				public ModeType Mode = ModeType.New;
				public StringBuilder CurrentString = new StringBuilder();
				//Do not perform a read for the next feed, this is will use the same Char.
				public Boolean Defer = false;
				public Boolean StoreNextRead = false;
				public Boolean InDebug = false;
				public DocLocation CurrentTokenLocation;
				public Dictionary<String, Object> ConVar = new Dictionary<string,object>();
				public Char QuoteChar;

				public Boolean InComment
				{
					get
					{
						return Mode == ModeType.CommentRange || Mode == ModeType.CommentSingle;
					}
				}

				public Object this[String name]
				{
					get
					{
						return ConVar.ContainsKey(name) ? ConVar[name] : null;
					}
					set
					{
						if (value == null)
						{
							if (ConVar.ContainsKey(name))
								ConVar.Remove(name);
						}
						else
							ConVar[name] = value;
					}
				}

				public Char ch
				{
					get
					{
						return Tcr.Current.Character;
					}
				}

				public TextChar cc
				{
					get
					{
						return Tcr.Current;
					}
				}

				public String ClearString()
				{
					String c = CurrentString.ToString();
					CurrentString.Clear();
					return c;
				}

				public void SetTokenLocation()
				{
					CurrentTokenLocation = Tcr.Current.Location;
				}

				/// <summary>
				/// Adds a character to the current string
				/// for tokenizing.
				/// </summary>
				/// <param name="str"></param>
				public void Add()
				{
					CurrentString.Append(Tcr.Current.Character);
				}

				public void Add(Char ch)
				{
					CurrentString.Append(ch);
				}

				public enum ModeType
				{
					New,
					Number,
					Identifier,
					String,
					CommentRange,
					CommentSingle,
					Whitespace
				}
				public void StopError(String Error)
				{
					this.ErrorLocation = CurrentTokenLocation;
					this.IsDone = false;
					this.Error = true;
					this.ErrorMessage = Error;
				}

				/// <summary>
				/// Switching the mode also clears the mode specific context variables.
				/// For example: in a quote you want to keep track of backslashes just
				/// in case for special characters. These need to be reset when the current
				/// mode switches.
				/// </summary>
				/// <param name="newMode"></param>
				public void SwitchMode(Context.ModeType newMode, Boolean setTokenLocation)
				{
					this.Mode = newMode;
					if (setTokenLocation)
						SetTokenLocation();
				}
			}

			public float Progress
			{
				get
				{
					return context.Tcr.Progress;
				}
			}

			private enum CharType
			{
				Letter,
				Number,
				Symbol,
				WhiteSpace,
				EOF,
				Error
			}

			private CharType GetCharType(Char ch)
			{
				if (Char.IsLetter(ch))
					return CharType.Letter;
				if (Char.IsNumber(ch))
					return CharType.Number;
				if (Char.IsPunctuation(ch) || Char.IsSymbol(ch))
					return CharType.Symbol;
				if (Char.IsWhiteSpace(ch))
					return CharType.WhiteSpace;
				return Options.Enable_TreatUnknownCharsAsLetters ? CharType.Letter : CharType.Error;
			}

			private Char ch
			{
				get
				{
					return context.ch;
				}
			}

			private FeedResult result;
			public Tokenizer(StreamReader stream, CompilerOptions options)
			{
				Options = options;
				context = new Context() { Tcr = new TextCharReader(stream) };
				result = new FeedResult();
				result.Token = null;
				result.Type = FeedResultType.NotDone;

				modeEvents.Add(Context.ModeType.New, new Action<CharType>(this.onMode_New));
				modeEvents.Add(Context.ModeType.Number, new Action<CharType>(this.onMode_Number));
				modeEvents.Add(Context.ModeType.String, new Action<CharType>(this.onMode_String));
				modeEvents.Add(Context.ModeType.Identifier, new Action<CharType>(this.onMode_Identifier));
				modeEvents.Add(Context.ModeType.CommentSingle, new Action<CharType>(this.onMode_CommentSingle));
				modeEvents.Add(Context.ModeType.CommentRange, new Action<CharType>(this.onMode_CommentRange));
				modeEvents.Add(Context.ModeType.Whitespace, new Action<CharType>(this.onMode_WhiteSpace));
			}

			public void Reset()
			{
				context.Defer = false;
				context.ConVar.Clear();
				context.CurrentTokenLocation.Reset();
				context.Error = false;
				context.ErrorLocation.Reset();
				context.ErrorMessage = "";
				context.Mode = Context.ModeType.New;
				context.ClearString();
				context.IsDone = false;
				result.Type = FeedResultType.NotDone;
				result.Token = null;
			}

			public int cLine
			{
				get
				{
					return context.Tcr.CurrentLocation.Line;
				}
			}

			public int cCol
			{
				get
				{
					return context.Tcr.CurrentLocation.Column;
				}
			}

			public Boolean Step()
			{
				CharType charType;
				result.Token = null;
				result.Type = FeedResultType.NotDone;

				if (context.IsDone || context.Error)
					return false;

				if (!context.Defer)
				{
					context.Tcr.Read(context.StoreNextRead);
					if (context.StoreNextRead)
						context.StoreNextRead = false;
				}
				else
					context.Defer = false;

				charType = context.Tcr.Current == null ? CharType.EOF : GetCharType(context.Tcr.Current.Character);
				if (context.Mode == Context.ModeType.String && charType == CharType.Error)
					charType = CharType.Letter;//Anything can go in a string

				if (charType == CharType.Error)
				{
					context.StopError("Invalid foreign character: " + context.Tcr.Current.Character);
					result.Set(FeedResultType.NotDone);
					return false;
				}
				modeEvents[context.Mode].Invoke(charType);
				return true;
			}

			//Sets the token in the result by using the currentString, then resetting the current string,
			//then readies the result for grabbing.
			private void SetToken(TokenType type)
			{
				String tok = context.ClearString();
				if (context.InDebug && !Options.Enable_DebugCode)
					return;
				result.Token = new Token() { Content = tok,
					Location = context.CurrentTokenLocation, Type = type };
				result.Type = FeedResultType.NewToken;
			}


			//Defers the character to be processed again and switches to normal parsing mode.
			private void NewAndDefer()
			{
				context.Defer = true;
				context.SwitchMode(Context.ModeType.New, false);
			}
			private void New(Boolean setTokenLocation)
			{
				context.SwitchMode(Context.ModeType.New, setTokenLocation);
			}
			private void SetTokenNew(TokenType type, Boolean setTokenLocation)
			{
				New(setTokenLocation);
				SetToken(type);
			}
			private void SetTokenNewAndDefer(TokenType type, Boolean clearContext = false)
			{
				NewAndDefer();
				SetToken(type);
				if (clearContext)
					context.ConVar.Clear();
				//Clear backing store so we start without any faux's
			}

			private void onMode_New(CharType charType)
			{
				if (context["%"] != null)
					if (charType == CharType.Letter || ch == '_')
					{
						//context["%"] = null; Keep it so onMode_Identifier can use it
						context.Tcr.DiscardBackingStore(2);
						context.Add();
						context.SwitchMode(Context.ModeType.Identifier, false);
						return;
					}
					else
					{
						context["%"] = null;
						return;
					}

				if (context["&"] != null)
				{
					if (ch == '"')
						context.Tcr.DiscardBackingStore(2);
					else {
						//Not a meta string
						context["&"] = null;
						return;
					}
				}
				
				if (context["."] != null)
				{
					if (charType == CharType.Number)
					{
						context.Tcr.DiscardBackingStore(2);
						context.Add('.');
						context.Add();
						context.SwitchMode(Context.ModeType.Number, false); //We've already set it
					}
					else
						context["."] = null; //Keep it set because it's a decimal point
					return;
				}

				if (context.InDebug && !context.Tcr.On("#"))
					if (context["#"] == null)
					{
						if (ch == '#')
						{
							context.Tcr.StartStore("#");//Save just incase
							context.StoreNextRead = true;//Make sure to advance to the next char
							context["#"] = true;//We have indeed encountered a pound
							return;
						}
					}
					else
					{
						context["#"] = null;
						if (ch == '/')
						{
							context.Tcr.DiscardBackingStore(2);
							context.InDebug = false;
							return;
						}	
					}

				if (context["/"] != null)
				{
					context["/"] = null;
					if (ch == '*' || ch == '/')
					{
						context.Tcr.DiscardBackingStore(2);
						context.SwitchMode(ch == '*' ? Context.ModeType.CommentRange : Context.ModeType.CommentSingle, false);
					}
					else if (ch == '#')
					{
						context.Tcr.DiscardBackingStore(2);
						if (context.InDebug)
							context.StopError("Already inside debug block");
						else
						{
							context.InDebug = true;
						}
					}
				}
				else
					switch (charType)
					{
						case CharType.Letter:
							context.Add();
							context.SwitchMode(Context.ModeType.Identifier, true);
							break;
						case CharType.Number:
							context.Add();
							context.SwitchMode(Context.ModeType.Number, true);
							break;
						case CharType.Symbol:
							if (!context.Tcr.On("/")) //If we're not doing non-comment parsing
								if (ch == '/')//Is a slash?
								{
									context.Tcr.StartStore("/");//Save just incase
									context.StoreNextRead = true;//Make sure to advance to the next char
									context.SetTokenLocation();
									context["/"] = true;//We have indeed encountered a slash
									return;
								}
							//TODO: Fix metastring detection
							/*We basically want to look ahead and see
							 if the next token is a quote character.
							 We check this is an ampersand,
							 and that the read is not a stored read.*/
							if (ch == '&' && !context.Tcr.On("&"))
							{
								context["&"] = true;
								context.Tcr.StartStore("&");
								context.StoreNextRead = true;
								return;
							}

							if (ch == '"' || ch == '\'')
							{
								if (ch == '\'' && !Options.Allow_SingleQuoteStrings)
								{
									context.StopError("Single quote strings are not allowed.");
									return;
								}
								context.QuoteChar = ch;
								context.SwitchMode(Context.ModeType.String, true);
								return;
							}

							if (ch == '_')
							{
								context.Add();
								context.SwitchMode(Context.ModeType.Identifier, true);
								return;
							}

							if (ch == '.' && !context.Tcr.On("."))
							{
								context.Tcr.StartStore(".");
								context["."] = true;
								context.SetTokenLocation();
								context.StoreNextRead = true;
								return;
							}

							if (ch == '%' && Options.ParsingMode == ParseModes.Singleplayer && !context.Tcr.On("%"))
							{
								context.Tcr.StartStore("%");
								context["%"] = true;
								context.SetTokenLocation();
								context.StoreNextRead = true;
								return;
							}

							/*if (ch == '-' && !context.Tcr.On("-"))
							{
								context.Tcr.PutStore("-");
								context["-"] = true;
								context.SetTokenLocation();
								context.StoreNextRead = true;
								return;
							}*/

							context.Add();

							SetTokenNew(TokenType.Symbol, true);
							break;
						case CharType.WhiteSpace:
							context.Add();
							context.SwitchMode(Context.ModeType.Whitespace, true);
							break;
						case CharType.EOF:
							if (context.InDebug)
								context.StopError("End of file before debug block ended");
							else
							{
								//It's null so we can't set it through the other means.
								context.CurrentTokenLocation = context.Tcr.CurrentLocation;
								SetToken(TokenType.EOF);
								context.IsDone = true;
							}
							break;
					}
			}

			//TODO: Tokenizer is seriously fucked up around the debug regions
			//[fixed, i think]
			private void onMode_WhiteSpace(CharType charType)
			{
				if (charType != CharType.WhiteSpace)
					SetTokenNewAndDefer(TokenType.WhiteSpace);
				else
					context.Add();
			}

			private void onMode_Number(CharType charType)
			{
				if (charType == CharType.Letter && (ch == 'x' || ch == 'X'))
					if (context["x"] == null)
						if (context.CurrentString.ToString() != "0")
							SetTokenNewAndDefer(TokenType.Number, true);
						else
						{
							context.Add();
							context["x"] = true;
						}
					else
						SetTokenNewAndDefer(TokenType.Number, true);
				else
					if (charType == CharType.Symbol && ch == '.')
						if (context["."] == null)
							context.Add();
						else
							SetTokenNewAndDefer(TokenType.Number, true);
					else
						if (charType == CharType.Letter)
							if (context["x"] != null)
								if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z'))
									context.Add();
								else
									SetTokenNewAndDefer(TokenType.Number, true);
							else
							{
								if (ch == 'f')
									context.Add();
								SetTokenNewAndDefer(TokenType.Number, true);
							}
						else
							if (charType == CharType.Number)
								context.Add();
							else
								SetTokenNewAndDefer(TokenType.Number, true);

			}

			private void onMode_String(CharType charType)
			{
				if (charType == CharType.EOF)
				{
					context.StopError("Unexpected end of file inside of string.");
					return;
				}

				if (context["\\"] == null)
					if (ch != '\\')
						if (ch != context.QuoteChar)
							context.Add();
						else
						{
							SetTokenNew(context["&"] == null ? TokenType.String : TokenType.MetaString, false);
							context["&"] = null;
						}
					else
						context["\\"] = true;
				else
				{
					Char c;
					if (StringMap.EscapeToChar.TryGetValue(ch, out c))
						context.Add(c);
					else
						context.Add(ch);
					context["\\"] = null;
				}
			}

			private void onMode_Identifier(CharType charType)
			{
				switch (charType)
				{
					case CharType.Letter:
					case CharType.Number:
						context.Add();
						break;
					default:
						if (charType == CharType.Symbol && context.ch == '_')
							context.Add();
						else
							if (context["%"] == null)
								SetTokenNewAndDefer(TokenType.Identifier);
							else
							{
								context["%"] = null;
								SetTokenNewAndDefer(TokenType.ResourceVar);
							}
						break;
				}
			}

			private void onMode_CommentSingle(CharType charType)
			{
				if (charType != CharType.EOF && ch != '\n')
					context.Add();
				else
					SetTokenNewAndDefer(TokenType.Comment);
			}

			private void onMode_CommentRange(CharType charType)
			{
				if (charType == CharType.EOF)
				{
					context.StopError("Unexpected end of file while in comment.");
					return;
				}
				if (!context.Tcr.On("*"))
					if (context["*"] == null)
						if (ch != '*')
							context.Add();
						else
						{
							context["*"] = true;
							context.Tcr.StartStore("*");
							context.StoreNextRead = true;
						}
					else
					{
						if (ch == '/')
						{
							context.Tcr.DiscardBackingStore(2);
							SetTokenNew(TokenType.Comment, false);
						}
						context["*"] = null;
					}
				else
					context.Add();
			}

			public String ErrorMessage
			{
				get
				{
					return !context.Error ? "" : "{0} {1}".Fmt(context.ErrorMessage, context.ErrorLocation.ToString());
				}
			}

			public DocLocation ErrorLocation
			{
				get
				{
					return context.ErrorLocation;
				}
			}

			public Boolean Error
			{
				get
				{
					return context.Error;
				}
			}

			public FeedResult Result
			{
				get
				{
					return result;
				}
			}

			public Boolean IsFinished
			{
				get
				{
					return context.Error ? false : context.IsDone;
				}
			}

			public Token[] GetTokens()
			{
				Reset();
				List<Token> Tokens = new List<Token>();
				while (this.Step())
					if (result.Type == FeedResultType.NewToken)
						Tokens.Add(result.Token);
				return Tokens.ToArray();
			}
		}
	}
}
