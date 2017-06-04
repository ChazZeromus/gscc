using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using GameScriptCompiler;
using GameScriptCompiler.CodeDOM;
using GameScriptCompiler.Text;
using GameScriptCompiler.Tokenization;

namespace GameScriptCompiler
{
	public enum ScopeMode
	{
		Module,
		Function,
		Expression
	}

	public enum ParseModes
	{
		/// <summary>
		/// Allows parsing of inline-script variables and declarative directives.
		/// </summary>
		Singleplayer,
		/// <summary>
		/// Disallows aforementioned singleplayer script features.
		/// </summary>
		Multiplayer
	}

	namespace CodeDOM
	{
		public class TokenReader : BackingObjectStream<Token>
		{
			Token[] Tokens;
			long pos = 0;
			public int LastConsecLength { get; private set; }
			public StringBuilder ConsecString = new StringBuilder();

			public TokenReader(Token[] tokens)
			{
				this.Tokens = tokens;
			}

			public String GetCurrentList
			{
				get
				{
					if (StoresEmpty)
						return null;
					StringBuilder sb = new StringBuilder();
					foreach (var c in QPk)
						sb.Append(c.Content);
					return sb.ToString();
				}
			}

			protected override bool EndOfStream_Internal()
			{
				return pos >= Tokens.Length;
			}

			protected override Token ReadNext()
			{
				if (pos >= Tokens.Length)
					return null;
				return Tokens[pos++];
			}

			public float Progress
			{
				get
				{
					return (float)pos / (float)Tokens.Length;
				}
			}

			public void DiscardLastConsec()
			{
				this.DiscardBackingStore(LastConsecLength);
				LastConsecLength = 0;
			}

			/// <summary>
			/// 1) If the consective seqeuence is not desired, are you going to deal with
			/// the current character? For example, on a '[', we consecutively read to
			/// see if we have double brackets. Since we're handling '['s specifically,
			/// if we dont see '[[' we can leave the extra bracket in the store
			/// and deal with the the single bracket on the spot. You must know
			/// this because DiscardLastConsec only discards the stored reads
			/// use to fetch the consecutive symbols, meaning the current character
			/// is not discarded. Routines can use the current character by dealing
			/// with it or deferring it but include additional conditions
			/// to avoid the same exact consecutive read, such as checking
			/// if the current token is stored with On().
			/// 
			/// An instance where we wouldn't able to deal with the initial symbol
			/// is assignment operators. 
			/// </summary>
			/// <param name="combined"></param>
			/// <returns></returns>
			public String ReadConsecutiveSymbols(String charList, int max = 6)
			{
				Queue<Token> newBacking = new Queue<Token>();
				int i = 0, valids = 0;
				Token t = Current;
				ConsecString.Clear();
				if (!t.IsSymbol() || !charList.Contains(t.Content[0]))
					return "";
				ConsecString.Append(t.Content);
				i = 1;
				while (t.IsSymbol() && i++ < max)
				{
					//We want to read from backing whenever we can. But we just end up reading from the
					//real raw stream. That's why instead of accidentily reading from the raw stream
					//and advancing the internal pointer, we just read normally, reading backings
					//if there are any and store them when we're done.
					newBacking.Enqueue(t = Read());
					if (!t.IsSymbol() || !charList.Contains(t.Content[0]))
					{
						LastConsecLength = valids;
						EnqueueNew(newBacking);
						return ConsecString.ToString();
					}
					else
						++valids;
					ConsecString.Append(t.Content);
				}
				LastConsecLength = valids;
				EnqueueNew(newBacking);
				return ConsecString.ToString();
			}
		}

		public class CodeDomParser
		{
			TokenReader Reader;
			Token _CurrentToken;
			String ErrorString;
			Boolean StoreNextRead = false, Defer = false;
			ParseLevel Level = ParseLevel.Root;
			Dictionary<ParseLevel, Action> LevelMap;
			Dictionary<RootModes, Action> RootModeMap;
			ContextStore Context;
			CompilerOptions Options;
			IEqualityComparer<String> StrComp;
			Stack<Token> TokenStore;
			CodeDOMModule FinalDOM = null;
			StatementBlock FinalStatements = null;
			Expression FinalExpression = null;
			RootModes RootMode = RootModes.None;
			FunctionModes FunctionMode = FunctionModes.AfterSignature;
			ModuleContext Mc = new ModuleContext();
			Text.DocLocation SavedLocation;
			InlineEvaluator IE;
			public Boolean IsFatalError, IsDone, IsDeferred = false;


			public CodeDomParser(Token[] tokens, CompilerOptions options, ScopeMode scope = ScopeMode.Module)
			{
				Expression.BaseId = 0;
				Mc.Scope = scope;
				Options = options;
				Reader = new TokenReader(tokens);
				TokenStore = new Stack<Token>();
				Errors = new List<MessageNote>();
				Warnings = new List<MessageNote>();
				FinalDOM = null;
				Context = new ContextStore();
				StrComp = Options.GetComparer();
				IE = new InlineEvaluator(Options,
				new InlineEvaluator.EvaluationOptions()
				{
					Allow_InlineArrays = false,
					Enable_Logical = true,
					Mode_DeclConstantLookupType = InlineEvaluator.EvaluationOptions.NamedConstantModeType.All
				},
				Mc.DeclConstants);

				StringMap.Init();
				LevelMap = new Dictionary<ParseLevel, Action>();
				LevelMap[ParseLevel.Function] = this.OnLevel_Function;
				LevelMap[ParseLevel.Root] = this.OnLevel_Root;
				RootModeMap = new Dictionary<RootModes, Action>();
				RootModeMap[RootModes.Constant] = this.OnRootMode_Constant;
				RootModeMap[RootModes.Directive] = this.OnRootMode_Directive;
				RootModeMap[RootModes.ParameterList] = this.OnRootMode_ParameterList;
				RootModeMap[RootModes.ImportDirective] = this.OnRootMode_ImportDirective;
				RootModeMap[RootModes.None] = this.OnRootMode_None;
				RootModeMap[RootModes.UnknownIdentifier] = this.OnRootMode_UnknownIdentifier;

				InitScope();
			}

			private void InitScope()
			{
				switch (Mc.Scope)
				{
					case ScopeMode.Module:
						RootMode = RootModes.None;
						FunctionMode = FunctionModes.AfterSignature;
						Level = ParseLevel.Root;
						break;
					case ScopeMode.Function:
					case ScopeMode.Expression:
						//Keeping for expression because we do CS checks before we do expression logic.
						Sl = new List<Statement>();
						Cs = new StatementBlock() { Location = new DocLocation() };
						RootMode = RootModes.None;
						FunctionMode = FunctionModes.InFunctionBlock;
						Level = ParseLevel.Function;
						break;
				}
			}

			public CodeDOMModule CreateFinalDOM(String name)
			{
				FinalDOM.Name = name;
				return FinalDOM;
			}

			public FunctionDefinition CreateFinalFunction(String name = null, IEnumerable<String> @params = null)
			{
				return new FunctionDefinition()
				{
					Name = name == null ? "NoName" : name,
					Location = new DocLocation() { Column = 0, Line = 0, Offset = 0 },
					Params = @params == null ? new Param[] {} : @params.CreateFromList<String, Param>(s => new Param() { Name = s }).ToArray(),
					RootStatement = FinalStatements
				};
			}

			public Expression CreateFinalExpression()
			{
				return FinalExpression;
			}

			enum RootModes
			{
				None,
				ImportDirective,
				Directive,
				UnknownIdentifier,
				ParameterList,
				Constant
			}

			enum FunctionModes
			{
				AfterSignature,
				InFunctionBlock
			}
			
			/// <summary>
			/// Since we can't modify Statement.Statements.Statements as
			/// we're working and that we have to assign it an array
			/// conversion from a list type when we're done with the current
			/// block, we'll save both the working-statement-list and the
			/// current statement.
			/// </summary>
			private class WorkingStatement
			{
				public Statement SavedStatement;
				public List<Statement> SavedStatementList;
			}

			public class MessageNote
			{
				public String Message;
				public DocLocation Location;
			}

			public List<MessageNote> Warnings, Errors;

			private class ModuleContext
			{
				public ScopeMode Scope;
				//For storing a list of strings. Useful for things like external references
				public List<String> StringList = new List<string>();
				//List of imports, to be ToArray when module is complete
				public List<ImportDirective> Imports = new List<ImportDirective>();
				//List of functions, to be ToArray when module is complete.
				public List<Declaration> Declarations = new List<Declaration>();
				//List of constants
				public List<DeclaredConstant> DeclConstants = new List<DeclaredConstant>();
				//List of params used to collect parameter list when reading a function definition
				public List<Param> Params = new List<Param>();
				//A statement list that is use to populate a statement block
				public List<Statement> StatementList = new List<Statement>();
				//Expression stack used for nested expressions
				public Stack<Expression> ExpressionStack = new Stack<Expression>();
				//Statement stack used for nested statements
				public Stack<WorkingStatement> StatementStack = new Stack<WorkingStatement>();
				public List<VariableReference> GlobalVariables = new List<VariableReference>();
				public Param CurrentParam = null;
				public FunctionDefinition CurrentFunction = null;
				public Expression CompleteExpression = null;
				public Statement CurrentStatement = null;
				public Directive CurrentDirective = null;
				public Boolean IsDeclarative = false, HasDefinitions = false,
					HasDeclarations = false;
			}

			class ContextStore
			{
				Dictionary<ContextVar, Object> ContextMap;
				public ContextStore()
				{
					ContextMap = new Dictionary<ContextVar, object>();
				}
				public void Clear()
				{
					ContextMap.Clear();
				}
				public Object this[ContextVar contextVar]
				{
					get
					{
						return ContextMap.ContainsKey(contextVar) ? ContextMap[contextVar] : null;
					}

					set
					{
						if (value == null && ContextMap.ContainsKey(contextVar))
							ContextMap.Remove(contextVar);
						else
							ContextMap[contextVar] = value;
					}
				}
			}

			enum SkipMode
			{
				NoWhitespace,
				None
			}

			enum ParseLevel
			{
				Root,
				Function
			}

			enum IdentifierType
			{
				None,
				Modifier,
				NamedConstant,
				Condition,
				Unary,
				FlowControl,
				MultiCase
			}

			IdentifierType GetIdentifierType()
			{
				if (tk.Type != TokenType.Identifier)
					return IdentifierType.None;
				if (StringMap.CallModKeys.Contains(content))
					return IdentifierType.Modifier;
				else if (StringMap.CondKeys.Contains(content))
					return IdentifierType.Condition;
				else if (StringMap.NamedConstantKeys.Contains(content))
					return IdentifierType.NamedConstant;
				else if (StringMap.UnaryFuncKeys.Contains(content))
					return IdentifierType.Unary;
				else if (StringMap.LoopControlKeys.Contains(content))
					return IdentifierType.FlowControl;
				else if (StringMap.SwitchKeywordKeys.Contains(content))
					return IdentifierType.MultiCase;
				return IdentifierType.None;
			}

			enum ContextVar
			{
				/// <summary>
				/// Whether we're wanting an identifier for a directive variable.
				/// </summary>
				ReadyForDirectiveVar,
				/// <summary>
				/// Whether an object loop has an indexer.
				/// </summary>
				ObjectLoopHasIndexer,
				/// <summary>
				/// Whether an object loop as an iterator.
				/// </summary>
				ObjectLoopHasIterator,
				/// <summary>
				/// Building an object loop statement.
				/// </summary>
				ObjectLoopNeedsCollection,
				/// <summary>
				/// A case label needs a constant.
				/// </summary>
				CaseNeedsExpression,
				/// <summary>
				/// Whether we're parsing a scope qualifier.
				/// </summary>
				ScopeQualifier,
				/// <summary>
				/// Whether we already have the identifier name in the scope qualifier.
				/// </summary>
				NeedsFinalReferenceName,
				/// <summary>
				/// Whether we're in a directive.
				/// </summary>
				Directive,
				/// <summary>
				/// Whether the parameter list for a function has started
				/// </summary>
				HasFunctionParameterList,
				/// <summary>
				/// Whether the current directive supports data lists.
				/// </summary>
				IsNonListlessDirective,
				/// <summary>
				/// Whether we're expecting a , or )
				/// </summary>
				ExpectingCommaOrEndList,
				/// <summary>
				/// Whether we're expecting a semi-colon to end a listed directive.
				/// </summary>
				HasDataList
			}

			private Directive Cd
			{
				get { return Mc.CurrentDirective; }
				set { Mc.CurrentDirective = value; }
			}

			private Token tk { get { return _CurrentToken; } }

			public float Progress { get { return Reader.Progress; } }

			private int cLine { get { return tk.Location.Line; } }
			private int cCol { get { return tk.Location.Column; } }

			private String content { get { return tk.Content; } }


			public void AddWarning(String warn, DocLocation? overrideLocation = null)
			{
				Warnings.Add(new MessageNote() { Location = overrideLocation == null ? CurLoc : overrideLocation.Value, Message = warn });
				if (Options.Enable_BreakOnWarn)
					Break();
			}

			public void AddError(String error, Token overrideToken = null, DocLocation? overrideLocation = null)
			{
				this.ErrorString = error;
				Errors.Add(new MessageNote() { Location = overrideLocation == null ? CurLoc : overrideLocation.Value, Message = error });
				//IsFatalError = AddError;
				IsFatalError = true;
				if (Options.Enable_BreakOnError)
					Break();
			}

			public void AddErrorPre(String error, Token overrideToken = null, DocLocation? overrideLocation = null)
			{
				Token ErrorToken;
				if (overrideToken != null)
					ErrorToken = overrideToken;
				else
					ErrorToken = _CurrentToken;
				AddError(error.Fmt(ErrorToken.Type != TokenType.EOF ? "\"{0}\"".Fmt(ErrorToken.Content) : "END OF FILE"), ErrorToken, overrideLocation);
			}

			public void StopErrorStartStatement(String prepend = null, Token token = null)
			{
				if (token == null) token = _CurrentToken;
				AddError("{0} assignment/call/unary-call can be beginning of statement.".Fmt(prepend == null ?
					"Only" :
					"{0}, only".Fmt(prepend.Fmt(token))));
			}

			public Boolean Error { get { return IsFatalError; } }

			public Boolean Done { get { return IsDone; } }

			public Boolean Step()
			{
				if (Error || IsDone)
					return false;
				if (!Defer)
				{
					_CurrentToken = Reader.Read(StoreNextRead);
					if (StoreNextRead)
						StoreNextRead = false;
				}
				else
				{
					Defer = false;
					IsDeferred = true;
				}
				if (tk.Type == TokenType.EOF && RootMode != RootModes.None
					&& Mc.Scope == ScopeMode.Module)
					AddError("Unexpected end of file.");
				else
					LevelMap[Level].Invoke();
				if (IsDeferred)
					IsDeferred = false;
				return true;
			}

			void StoreToken()
			{
				TokenStore.Push(_CurrentToken);
			}

			Token LoadToken()
			{
				return TokenStore.Pop();
			}

			Boolean EmptyToken()
			{
				return TokenStore.Count == 0;
			}

			void ClearToken()
			{
				TokenStore.Clear();
			}

			void Break()
			{
				if (System.Diagnostics.Debugger.IsAttached)
					System.Diagnostics.Debugger.Break();
			}

			void OnFinishDomParse()
			{
				IsDone = true;
				FinalDOM = new CodeDOMModule() 
				{
					IsDeclarative = Mc.IsDeclarative,
					Imports = Mc.Imports.ToArray(),
					Declarations = Mc.Declarations.ToArray(),
					Constants = Mc.DeclConstants.ToArray(),
					GlobalVariables = Mc.GlobalVariables.ToArray()
				};
			}

			void SwitchLevel(ParseLevel level)
			{
				Level = level;
			}

			void SaveLocation()
			{
				SavedLocation = CurLoc;
			}
			void SaveLocation(Text.DocLocation @override)
			{
				SavedLocation = @override;
			}

			void OnDirective(Directive directive)
			{
				if (StringMap.InternalDirs.Contains(directive.Name))
					switch (StringMap.InternalDirs.Get())
					{
						case InternalDirectives.DeclarativeGlobal:
							if (directive.DirectiveType != Directive.DirectiveTypes.ListlessDeclare)
							{
								AddError("#{0} directive does not have arguments.".Fmt(directive.Name));
								break;
							}
							if (Mc.IsDeclarative)
								AddWarning("Module is already declarative global.");
							else
								if (Mc.Declarations.Count == 0 && Mc.DeclConstants.Count == 0)
									Mc.IsDeclarative = true;
								else
									AddError("Declarative-global directive must appear before any declarations.");
							break;
						case InternalDirectives.AddGlobalVariable:
							if (!Mc.IsDeclarative)
								AddError("Cannot declare global variables in a non global-declarative.");
							else if (directive.DirectiveType != Directive.DirectiveTypes.Declare
								|| directive.AsDeclarative().Data.Count != 1 || !directive.AsDeclarative().Data[0].IsConstant(Runtime.VarType.String))
								AddError("#{0} must have one string argument.");
							else
							{
								var targetVar = directive.AsDeclarative().Data[0].AsConstant().Value;
								if (Mc.GlobalVariables.FirstOrDefault(gv => gv.Name == targetVar) != null)
									AddError("Global variable \"{0}\" already declared.");
								else
									Mc.GlobalVariables.Add(new VariableReference(targetVar)
									{
										Location = directive.Location
									});
							}
							break;
					}
			}

			void OnRootMode_None()
			{
				if (Context[ContextVar.Directive] != null)
					if (tk.Type == TokenType.Identifier)
						if (StringMap.DirKeys.Contains(content) && StringMap.DirKeys.Get() == DirectiveType.Include)
							if (!Mc.IsDeclarative)
							{
								RootMode = RootModes.ImportDirective;
								Cd = new ImportDirective(content, null) { Location = SavedLocation };
								Context.Clear();
								Context[ContextVar.ScopeQualifier] = true;
								Mc.StringList.Clear();
							}
							else
								AddErrorPre("Declarative-global modules cannot include other modules.");
						else if (Options.ParsingMode == ParseModes.Singleplayer)
						{
							RootMode = RootModes.Directive;
							Cd = new ListlessDelcarativeDirective(content) { Location = SavedLocation };
							Context.Clear();
							Mc.StringList.Clear();
						}
						else
							AddErrorPre("Only singleplayer parsing mode supports declarative directives.");
					else
					{
						if (IsSignificant)
							AddErrorPre("Unexpected {0}, expecting a directive name.");
					}
				else
					switch (tk.Type)
					{
						case TokenType.Symbol:
							if (tk.Content != "#")
								AddErrorPre("Unexpected symbol {0}.");
							else
							{
								SaveLocation();
								Context[ContextVar.Directive] = true;
							}
							break;
						case TokenType.Identifier:
							RootMode = RootModes.UnknownIdentifier;
							SaveLocation();
							StoreToken();
							break;
						case TokenType.Number:
							AddErrorPre("Unexpected number {0} in beginning declarations.");
							break;
						case TokenType.EOF:
							OnFinishDomParse();
							break;
						default:
							if (IsSignificant)
								AddErrorPre("Unexpected {0} {{0}}.".Fmt(tk.Type.ToString()));
							break;
					}
			}

			void OnRootMode_Directive()
			{
				if (!IsSignificant)
					return;
				if (Context[ContextVar.IsNonListlessDirective] == null)
					if (tk.IsSymbol(";"))
					{
						Context[ContextVar.IsNonListlessDirective] = null;
						RootMode = RootModes.None;
						OnDirective(Cd);
						Mc.Declarations.Add(Cd);
						Cd = null;
					}
					else
						if (tk.IsSymbol("("))
						{
							Context[ContextVar.IsNonListlessDirective] = true;
							Cd = new DeclarativeDirective(Cd.Name) { Location = Cd.Location };
						}
						else
							AddErrorPre("Unexpected {0}, expecting \";\" or data list.");
				else if (Context[ContextVar.HasDataList] == null)
					if (Context[ContextVar.ExpectingCommaOrEndList] == null)
						if (tk.Type.IsTokenConstant())
						{
							if (tk.IsIdentifier())
								if (GetIdentifierType() != IdentifierType.NamedConstant)
									AddErrorPre("Unexpected {0}, Only constants are allowed in data lists.");
								else
									Cd.AsDeclarative().Data
										.Add(new Constant(StringMap.NamedConstantKeys.Get().GetNamedConstantType(),
											content) { Location = CurLoc });
							else
								Cd.AsDeclarative().Data
									.Add(new Constant(tk.Type.GetTokenConstantVarType(),
										content) { Location = CurLoc });
							Context[ContextVar.ExpectingCommaOrEndList] = true;
						}
						else if (tk.IsSymbol(")") && Cd.AsDeclarative().Data.Count == 0)
							Context[ContextVar.HasDataList] = true;
						else
							AddErrorPre("Unexpected {0}, expecting constant for directive data list.");
					else if (tk.IsSymbol(","))
						Context[ContextVar.ExpectingCommaOrEndList] = null;
					else if (tk.IsSymbol(")"))
						Context[ContextVar.HasDataList] = true;
					else
						AddErrorPre("Unexpected {0}, expecting \",\", or \")\".");
				else
					if (tk.IsSymbol(";"))
					{
						Context[ContextVar.HasDataList] = null;
						Context[ContextVar.ExpectingCommaOrEndList] = null;
						Context[ContextVar.IsNonListlessDirective] = null;
						OnDirective(Cd);
						Mc.Declarations.Add(Cd);
						RootMode = RootModes.None;
						Cd = null;

					}
					else
						AddErrorPre("Unexpected {0}, expecting \";\" at end of directive.");
			}

			void OnRootMode_ImportDirective()
			{
				if (Context[ContextVar.ScopeQualifier] != null)
					if (tk.Type == TokenType.Identifier)
					{
						Mc.StringList.Add(tk.Content);
						Context[ContextVar.ScopeQualifier] = null;
					}
					else
					{
						if (IsSignificant)
							AddError("Unexpected {0}, expected path name.".Fmt(tk.Type.ToString()));
					}
				else
					if (tk.Type == TokenType.Symbol && tk.Content == "\\")
						Context[ContextVar.ScopeQualifier] = true;
					else
						if (tk.IsSymbol(";"))
							if (Mc.StringList.Count > 0)
							{
								Context.Clear();
								RootMode = RootModes.None;
								Cd.AsImport().Reference = ModuleReference.Create(Mc.StringList);
								Mc.Imports.Add(Cd.AsImport());
								Mc.StringList.Clear();
								Cd = null;
							}
							else
								AddError("Expected a script path.");
						else
							AddError("Expected '\\' or ';'.");
			}

			void OnRootMode_UnknownIdentifier()
			{
				if (IsSignificant)
					if (tk.IsSymbol("("))
					{
						var t = LoadToken();
						FunctionDefinition fd;
						if ((fd = (Mc.Declarations.Find(f =>
						{
							return f.DeclarationType == Declaration.DeclarationTypes.Function
								&& StrComp.Equals(f.AsFunctionDefinition().Name, t.Content);
						})) as FunctionDefinition) == null)
						{
							RootMode = RootModes.ParameterList;
							Mc.CurrentFunction = new FunctionDefinition() { Location = SavedLocation };
							Mc.CurrentFunction.Name = t.Content;
							Mc.Params.Clear();
							//Don't forget to cleanup completed function definitions
						}
						else
							AddError("Duplicate function definition {1}, already defined here {0}"
								.Fmt(fd.Name, fd.Location));
					}
					else
						if (tk.IsSymbol("="))
						{
							Token t = LoadToken();
							String name = t.Content;
							DeclaredConstant exist;
							if ((exist = Mc.DeclConstants.LookUpConstant(name)) == null)
							{
								RootMode = RootModes.Constant;
								EsPush(new AssignmentStatement(BinaryOperationType.Assign)
								{
									Left = new VariableReference(name) { Location = SavedLocation },
									Location = SavedLocation
								});
								Sl.Clear();
								Cs = new StatementBlock();//Fake
							}
							else
								AddError("Constant \"{0}\" was already declared at {1}.".Fmt(t.Content, exist.Location), null, t.Location);
						}
						else
							AddErrorPre("Unexpected {0}, expecting function definition or constant declaration.");
			}

			void OnRootMode_Constant()
			{
				if (tk.IsSymbol("{") || tk.IsSymbol("}"))
					AddError("Unexpected block begin/end specifier in constant declaration.");
				else
				{
					OnExpression();
					if (!SlEmpty)
					{
						var ass = Sl[0].AsAssignment();
						//We use our nifty IE to resolve constants now! But...we have to treat RequireRuntime errors as
						//expression formations that aren't permitted in constant declarations.
						IE.Evaluate(ass.Right);
						switch (IE.Result.ResultType)
						{
							case InlineEvaluator.EvaluationResult.Success:
								var c = IE.Result.Result;
								var dc = new DeclaredConstant()
								{
									Name = ass.Left.AsVariableReference().Name,
									Value = c,
									ExpressiveValue = Options.Enable_StoreResolvedConstants ? c : ass.Right,
									Location = ass.Location,
								};
								if (c.IsConstant())
									dc.ResolvedType = c.AsConstant().ConstantType;
								dc.IsTruple = c.IsTruple();
								Mc.DeclConstants.Add(dc);
								break;
							case InlineEvaluator.EvaluationResult.DivisionByZero:
								AddError("Evaluation of declared constant resulted in division of zero.", null, IE.Result.ErrorExpr.Location);
								break;
							case InlineEvaluator.EvaluationResult.RequiresRuntimeEvaluation:
							case InlineEvaluator.EvaluationResult.InvalidOperation:
								if (IE.Result.InvalidMsg != null)
									AddError(IE.Result.InvalidMsg.ToString(), null, IE.Result.ErrorExpr.Location);
								else
									AddError("Could not evaluate declared constant, expression has invalid operations/operands.",
										null, IE.Result.ErrorExpr.Location);
								break;
							case InlineEvaluator.EvaluationResult.ConstantDoesNotExist:
								AddError("Unknown constant name/declaration in constant declaration \"{0}\"".Fmt(IE.Result.ErrorExpr.AsVariableReference().Name),
									null, IE.Result.ErrorExpr.Location);
								break;
						}
						/*Constant.ResolveResult rr = new Constant.ResolveResult();
						Expression c = Constant.ResolveConstant(ass.Right, Mc.Constants, ref rr);
						if (rr.ResultType == Constant.ResolveResult.ResultTypes.Success)
						{
							var dc = new DeclaredConstant()
							{
								Name = ass.Left.AsVariableReference().Name,
								Value = Options.Enable_StoreResolvedConstants ? c : ass.Right,
								Location = ass.Location,
							};
							if (c.IsConstant())
								dc.ResolvedType = c.AsConstant().ConstantType;
							dc.IsTruple = c.IsTruple();
							Mc.Constants.Add(dc);
						}
						else if (rr.ResultType == Constant.ResolveResult.ResultTypes.DivisionByZero)
							AddError("Declared constant expression divided by zero.");
						else if (rr.ResultType == Constant.ResolveResult.ResultTypes.MalformedNumber)
							AddError("Number \"{0}\" in declared constant is malformed.".Fmt(rr.LastConstant));
						else if (rr.ResultType == Constant.ResolveResult.ResultTypes.UnknownConstant)
							AddError("Unknown constant name/declaration in constant declaration \"{0}\"".Fmt(rr.LastConstant));
						else
							AddError("Could not resolve declared constant, expression has invalid operations/operands.");*/

						Sl.Clear();
						Cs = null;
						Ce = null;
						RootMode = RootModes.None;
					}
					else if (Ce != null && !EsEmpty && Ce.IsStatement() && !Ce.IsPrimitivable())
						AddErrorPre("Only primitives and grouping are allowed in constant declarations");
					else
						if (Es.Count > 1 && !EsPk.IsPrimitivable())
							AddErrorPre("Only primitives and grouping are allowed in constant declarations");
				}
			}

			void OnRootMode_ParameterList()
			{
				switch (tk.Type)
				{
					case TokenType.Identifier:
						if (Mc.IsDeclarative && StringMap.ParamModKeys.Contains(content))
						{
							var mod = StringMap.ParamModKeys.Get();
							if (Mc.CurrentParam != null)
								if (Mc.CurrentParam.Name == null)
									if (Mc.CurrentParam[mod])
										AddErrorPre("Duplicate parameter modifier {0}.");
									else
										if (mod != ParamModifiers.Minimum || Mc.CurrentFunction.MinimumArgs == null)
											if (mod != ParamModifiers.Minimum ||
												!Mc.CurrentParam[ParamModifiers.VariableArguments])
												Mc.CurrentParam[mod] = true;
											else
												AddError("Cannot use Minimum-modifier on a variable argument parameter.");
										else
											AddErrorPre("Minimum modifier already specified at {0}."
												.Fmt(Mc.Params.First(p => p[ParamModifiers.Minimum]).Location));
								else
									AddErrorPre("Parameter modifier {0} cannot go after parameter name.");
							else
							{
								Mc.CurrentParam = new Param()
								{
									Name = null,
									Location = CurLoc
								};
								Mc.CurrentParam[StringMap.ParamModKeys.Get()] = true;
							}
						}
						else if (Mc.CurrentParam == null)
							Mc.CurrentParam = new Param() { Location = CurLoc, Name = content };
						else if (Mc.CurrentParam.Name == null)
						{
							if (StringMap.ParamModKeys.Contains(content))
								AddWarning("Keyword {0} is reserved as a parameter modifier for global declarative modules. " +
									"Since current module is not a global declarative, it will be treated as a parameter name.");
							Mc.CurrentParam.Name = content;
						}
						else
							AddErrorPre("Unexpected indentifier {0}, expecting \",\" or \")\"");
						break;
					case TokenType.Symbol:
						if (content == ")" || content == ",")
						{
							if (Mc.CurrentParam != null)
								if (Mc.CurrentParam.Name != null)
									if (!Mc.CurrentFunction.HasVariableArguments)
									{
										Param dupe = Mc.Params.Find(p => StrComp.Equals(p.Name, Mc.CurrentParam.Name));
										if (dupe == null)
										{
											Mc.CurrentFunction.HasVariableArguments = Mc.CurrentParam[ParamModifiers.VariableArguments];
											if (Mc.CurrentParam[ParamModifiers.Minimum])
												if (Mc.CurrentFunction.MinimumArgs == null)
													Mc.CurrentFunction.MinimumArgs = Mc.Params.Count + 1;
												else
													throw new Exception("We're setting up minimum modifier AGAIN!" +
														" We should have caught this while building the parameter.");
											Mc.Params.Add(Mc.CurrentParam);
											Mc.CurrentParam = null;
										}
										else
											AddError("Duplicate parameter {0} at {1}".Fmt(Mc.CurrentParam.Name, dupe.Location));
									}
									else
										AddError("Unexpected parameter item \"{0}\"".Fmt(Mc.CurrentParam.Express(0)) +
											" Cannot have additional parameter items after variable argument parameter.");
								else
									AddErrorPre("Unexpected {0}. Unexpected end of parameter item. Expecting parameter name.");
							else if (content == ",")
							{
								AddErrorPre("Unexpected {0}. Missing parameter in parameter list of function \"{0}\"."
									.Fmt(Mc.CurrentFunction.Name));
								break;
							}

							if (content == ")")
							{//Immediately move into function level, the opening brackets indicate a statement collection anyways.
								Mc.CurrentFunction.Params = Mc.Params.ToArray();
								//We don't use a stack for deferring function creation because functions are never nested.
								Mc.Params.Clear();
								SwitchLevel(ParseLevel.Function);
								RootMode = RootModes.None;
								FunctionMode = FunctionModes.AfterSignature;
							}
						}
						else
							AddErrorPre("Unexpected {0}, expecting \",\" or \")\".");
						break;
					default:
						if (IsSignificant)
							AddErrorPre("Unexpected {0} in parameter list.");
						break;
				}
			}

			void OnLevel_Root()
			{
				RootModeMap[RootMode]();
			}

			void OnLevel_Function()
			{
				switch (FunctionMode)
				{
					case FunctionModes.AfterSignature:
						if (IsSignificant)
							if (tk.IsSymbol("{"))
								if (!Mc.HasDeclarations)
								{
									if (!Mc.HasDefinitions)
										Mc.HasDefinitions = true;
									Sl = new List<Statement>();
									Cs = new StatementBlock() { Location = CurLoc };
									FunctionMode = FunctionModes.InFunctionBlock;
								}
								else
									AddError("Cannot define functions in already declaration-only-module.");
							else if (tk.IsSymbol(";"))
								if (Mc.IsDeclarative)
									if (!Mc.HasDefinitions)
									{
										if (!Mc.HasDeclarations)
											Mc.HasDeclarations = true;
										Mc.CurrentFunction.RootStatement = null;
										Mc.Declarations.Add(Mc.CurrentFunction);
										Mc.CurrentFunction = null;
										SwitchLevel(ParseLevel.Root);
									}
									else
										AddError("Cannot declare functions in already definition-only-module.");
								else
									AddErrorPre("Cannot declare functions in a non-declarative module.");
							else
								AddErrorPre("Unexpected {0}, expecting \"}\" or \";\".");
						break;
					case FunctionModes.InFunctionBlock:
						OnStatement();
						break;
				}
			}

			public enum StatementModes
			{
				Single,
				LoopSet
			}

			Boolean CheckEs(Expression.ExpressionTypes type)
			{
				return !EsEmpty
					&& EsPk.GetExpressionType()
					== type;
			}

			Boolean CheckSs(Statement.StatementTypes type)
			{
				return !SsEmpty && SsPk.GetStatementType() == type;
			}

			Boolean CheckSsConditional(ConditionalTypes type)
			{
				return CheckSs(Statement.StatementTypes.Conditional) && SsPk.IsConditionalStatement(type);
			}

			Boolean CheckSsMultiConditional()
			{
				return !SsEmpty && SsPk.IsConditionalStatement(ConditionalTypes.MultiConditional);
			}

			Boolean CheckCs(Statement.StatementTypes type)
			{
				return Cs != null && Cs.GetStatementType() == type;
			}

			Boolean CheckCsConditional()
			{
				return CheckCs(Statement.StatementTypes.Conditional);
			}

			Boolean CheckCsConditional(ConditionalTypes type)
			{
				return CheckCs(Statement.StatementTypes.Conditional) && Cs.IsConditionalStatement(type);
			}

			Boolean CheckCsObjectLoop()
			{
				return CheckCsConditional(ConditionalTypes.ObjectLoop);
			}

			Boolean CheckEsAsReference(Reference.ReferenceTypes type)
			{
				return Mc.ExpressionStack.Count > 0
					&& Mc.ExpressionStack.Peek().GetExpressionType() == Expression.ExpressionTypes.Reference
					&& Mc.ExpressionStack.Peek().AsReference().GetReferenceType() == type;
			}

			Boolean CheckEsAsStatement(Statement.StatementTypes type)
			{
				return Mc.ExpressionStack.Count > 0
					&& Mc.ExpressionStack.Peek().GetExpressionType() == Expression.ExpressionTypes.Statement
					&& Mc.ExpressionStack.Peek().AsStatement().GetStatementType() == type;
			}

			Boolean CheckEsAsConditional(ConditionalTypes type)
			{
				return CheckEsAsStatement(Statement.StatementTypes.Conditional) &&
					Mc.ExpressionStack.Peek().AsConditionalStatement().GetConditionType() == type;
			}

			Boolean CheckEsAsCase()
			{
				return !EsEmpty && EsPk.IsCaseExpression();
			}

			/// <summary>
			/// The expression stack stacks up incomplete expressions, when an expression has multiple
			/// expression parts that need to be filled, they are pushed onto the stack so that when
			/// we can provide them, they can be popped out as a completed expression and assigned
			/// to Ce.
			/// </summary>
			Stack<Expression> Es { get { return Mc.ExpressionStack; } }
			/// <summary>
			/// Ce will alwayes hold the most current completed and full expression.
			/// It does NOT imply the whole statement, it simply exists so that
			/// further operations can be performed on it, like binary/unary
			/// operators etc. When Ce is null and Es is empty, this means
			/// there is no statement and expression, the beginning of a statement. When Es is
			/// not empty and Ce is null, it means we're waiting for an expression
			/// for the current unresolved expression. If Ce is not null,
			/// and Es is not empty, it means we're ready for more operations to
			/// be performed with Ce. Like unary operators, being the Self part
			/// of a call, part of a binary expression etc.
			/// </summary>
			Expression Ce
			{
				get { return Mc.CompleteExpression; }
				set { Mc.CompleteExpression = value; }
			}

			/// <summary>
			/// Whether the current conditional has finished its condition. i.e. closing parenthesis.
			/// </summary>
			Boolean IsCsUnfinishedCondition
			{
				get
				{
					return CheckCsConditional() && !Cs.AsConditionalStatement().FinishedConditional;
				}
			}

			Stack<WorkingStatement> Ss { get { return Mc.StatementStack; } }

			List<Statement> Sl { get { return Mc.StatementList; } set { Mc.StatementList = value; } }

			Statement Cs { get { return Mc.CurrentStatement; } set { Mc.CurrentStatement = value; } }

			Boolean EsEmpty
			{
				get
				{
					return Es.Count == 0;
				}
			}

			Boolean AddModifier(CallModifiers modifier)
			{
				if (!EsPk.IsInvocation())
					throw new Exception("Attempting to add call modifier to non-invocation.");
				CallModifiers cm = StringMap.CallModKeys.Get();
				if (cm == CallModifiers.Volatile && !Options.Enable_CallModifier_Volatile)
				{
					AddErrorPre("Cannot use call modifier {0}.");
					return false;
				}
				if (cm.IsModifierExclusive() && EsPk.AsInvocation().Modifiers.Count == 0)
					if (!cm.AvailableInParseMode(Options.ParsingMode))
						AddErrorPre("Call modifier {{0}} cannot be used in parsing mode \"{0}\".".Fmt(Options.ParsingMode.ToString()));
					else
					{
						EsPk.AsInvocation().Modifiers.Add(StringMap.CallModKeys.Get());
						return true;
					}
				else
					AddErrorPre("Call modifier {0} cannot be used with other modifiers.");

				return false;
			}

			/// <summary>
			/// This handles identifying custom unaries and the builtin
			/// unary "return".
			/// </summary>
			/// <param name="type"></param>
			void AddUnary(UnaryFunctionTypes type)
			{
				if (EsEmpty)
					if (Ce == null)
						if (type == UnaryFunctionTypes.Return)
							if (Mc.Scope != ScopeMode.Expression)
								Es.Push(new Return() { Location = CurLoc });
							else
								AddError("Expression scopes do not allow returns.");
						else
							Es.Push(new UnaryCall(type) { Location = CurLoc });
					else
						AddError("Unary calls are not allowed a self parameter.");
				else
					AddErrorPre("Unary call {0} must called as a single statement.");
			}

			void EsPush(Expression expression)
			{
				Es.Push(expression);
			}

			void SsPush(Statement newStatement)
			{
				Ss.Push(new WorkingStatement() { SavedStatement = Cs, SavedStatementList = Mc.StatementList });
				Sl = new List<Statement>();
				Cs = newStatement;
			}

			Text.DocLocation CurLoc
			{
				get
				{
					return tk.Location;
				}
			}

			Boolean NamelessFunction
			{
				get
				{
					return CheckEsAsStatement(Statement.StatementTypes.FunctionCall)
						&& !EsPk.AsFunctionCall().HasFunction();
				}
			}

			Boolean NamedFunction
			{
				get
				{
					return CheckEsAsStatement(Statement.StatementTypes.FunctionCall)
						&& EsPk.AsFunctionCall().HasFunction();
				}
			}

			Boolean StartedFunction
			{
				get
				{
					return NamelessFunction && EsPk.AsFunctionCall().StartedParameterList;
				}
			}

			Expression EsPk
			{
				get
				{
					return Es.Peek();
				}
			}

			Statement SsPk
			{
				get
				{
					return Ss.Peek().SavedStatement;
				}
			}

			List<Statement> SsPkSl
			{
				get
				{
					return Ss.Peek().SavedStatementList;
				}
			}

			Statement SsPkSlPk
			{
				get
				{
					return Ss.Peek().SavedStatementList.Last();
				}
			}

			Boolean SsPkSlEmpty
			{
				get
				{
					return !SsEmpty && SsPkSl.Count <= 0;
				}
			}

			Statement SlPk
			{
				get
				{
					return Sl.Last();
				}
			}

			Boolean SlEmpty
			{
				get
				{
					return Sl.Count <= 0;
				}
			}

			Boolean SsEmpty
			{
				get
				{
					return Ss.Count <= 0;
				}
			}

			String DebugStack
			{
				get
				{
					StringBuilder sb = new StringBuilder();
					foreach (var s in Es)
					{
						sb.Append("{0};".Fmt(s.Express(0)));
					}
					return sb.ToString();
				}
			}

			Boolean IsSignificant
			{
				get
				{
					return tk.Type != TokenType.WhiteSpace && tk.Type != TokenType.Comment; 
				}
			}

			Statement CeAsStatement
			{
				get
				{//Postfix operators fail because they are treated as statements.
					return Ce != null ? Ce.AsStatement() : null;
				}
			}

			/// <summary>
			/// Whether we're building the condition for a conditional statement, meaning we've already hit the
			/// opening parenthesis.
			/// </summary>
			Boolean HasStartedConditional
			{
				get
				{
					return Cs != null && Cs.IsConditionalStatement() && Cs.AsConditionalStatement().StartedCondition;
				}
			}

			Boolean DoesNearestFlowableStatementExist(Boolean skipMulti, ref int target)
			{
				if (Cs != null)
					if (Cs.IsConditionalStatement() &&
						Cs.AsConditionalStatement().GetConditionType().IsFlowable(skipMulti))
					{
						target = 0;
						return true;
					}
					else
					{
						int num = 1;
						foreach (var ws in Ss.ToArray())
							if (ws.SavedStatement.IsConditionalStatement() &&
								ws.SavedStatement.AsConditionalStatement().GetConditionType().IsFlowable(skipMulti))
								return true;
							else
								++num;
						return false;
					}
				else
					return false;
			}

			void PopSs()
			{
				var ws = Ss.Pop();
				Cs = ws.SavedStatement;
				Sl = ws.SavedStatementList;
			}

			FunctionInvocation OnInvocation(FunctionInvocation invocation)
			{
				if (invocation.IsSelfASelfCall())
					AddWarning("The self object of this invocation is another self object based invocation. Is this intended?");
				return invocation;
			}

			void OnEvaluateConstantContent(Constant c)
			{
				Constant.EvaluateContentWarnings warning;
				var result = c.EvaluateContent(Options, out warning);
				switch (result)
				{
					case Constant.EvaluateContentResult.HexNotAllowed:
						AddErrorPre("Invalid constant {0}, Hexadecimal constants are not allowed.");
						break;
					case Constant.EvaluateContentResult.InfinityNotAllowed:
						AddErrorPre("Infinity is now allowed.");
						break;
					case Constant.EvaluateContentResult.InvalidFloat:
						AddErrorPre("Invalid float {0}.");
						break;
					case Constant.EvaluateContentResult.InvalidInteger:
						AddErrorPre("Invalid integer {0}.");
						break;
				}
				switch (warning)
				{
					case Constant.EvaluateContentWarnings.IntegerExceedsMaximum:
						AddWarning("Integer exceeds maximum value.");
						break;
					case Constant.EvaluateContentWarnings.IntegerExceedsMinimum:
						AddWarning("Integer exceeds minimum value.");
						break;
				}
			}

			void OnDoubleBracket()
			{
				if (NamelessFunction)
				{
					EsPush(Es.Pop().AsFunctionCall().ConvertToPointerCall());
				}
				else
				{
					EsPush(OnInvocation(new PointerCall() { Self = Ce, Location = CurLoc }));
					Ce = null;
				}
			}

			void OnScopeQualifier()
			{
				if (!IsSignificant)
					return;
				if (Context[ContextVar.NeedsFinalReferenceName] == null)
				{
					if (tk.Type == TokenType.Identifier)
						Mc.StringList.Add(tk.Content);
					else
						if (!tk.IsSymbol("\\") && !tk.IsSymbol(":"))
							AddErrorPre("Unexpected {0}, expecting \"::\" or \"\\\\\" for scope qualifier.");
						else
							if (tk.IsSymbol(":") && GetConsecutiveSymbols(":") == "::")
							{
								/*peek = Reader.Read(true);
								if (tk.IsSymbol(":"))
								{*/
								Reader.DiscardLastConsec();
								Context[ContextVar.NeedsFinalReferenceName] = true;
								//}
							}
							else
								if (Reader.Prev == null || Reader.Prev.Type != TokenType.Identifier)
									AddError("Cannot have empty path in scope qualifier.");
				}
				else
					if (tk.Type == TokenType.Identifier)//Apparently we can whitespace here.
					//Add the final reference name
					{
						FunctionReference fref = new FunctionReference(Mc.StringList.Count > 0 ?
							ModuleReference.Create(Mc.StringList) : null)
							{
								Name = content,
								LocalExplicit = Mc.StringList.Count == 0 ? true : false,
								Location = SavedLocation//I think we come from mainly "::" and "/" on a varref
							};
						Mc.StringList.Clear();
						if (NamelessFunction)
							EsPk.AsFunctionCall().Function = fref; //Ce is probably already null. No need to do it here?
						else
							Ce = fref;
						Context[ContextVar.NeedsFinalReferenceName] = null;
						Context[ContextVar.ScopeQualifier] = null;
					}
					else
						AddErrorPre("Unexpected {0}, expecting function name for scope qualifier.");
			}

			/*
			 * We are indicative of parsing a case/default
			 * label by pushing onto Es a phony expression
			 * that simply describes that we're adding
			 * a case/default switch label.
			 * 
			 * On a case/default, we push the phoney
			 * expression. Then if it's a case,
			 * we set context var NeedCaseExpression,
			 * so we can skip the OnCase handler and
			 * set have OnExpression set Ce to a constant.
			 */
			void OnCase()
			{
				if (!IsSignificant)
					return;
				if (!tk.IsSymbol(":"))
					AddErrorPre("Unexpected {0}, expecting \":\" to end switch case/default.");
				else
				{
					Boolean isDefault = Es.Pop().AsCaseExpression().IsDefault;
					var mc = SsPk.AsMultiConditional();
					if (!isDefault)
					{
						Case entry;
						if (!mc.AddCase(Ce.AsConstant(), Sl.Count, out entry))
							AddError("Case already exists."); // TODO: Maybe more information.
						else
							if (!Options.Allow_GroupedDefault && entry.HasDefault)
								AddError("Case is not allowed to be grouped with default case.");
					}
					else
						if (!mc.HasDefault)
						{
							Case entry;
							mc.AddDefault(Sl.Count, out entry);
							if (!Options.Allow_GroupedDefault && entry.Values.Count > 1)
								AddError("Default case is not allowed to be grouped with other cases.");
						}
						else
							AddErrorPre("Switch body already has default case.");
					SsPk.AsMultiConditional().InsideCase = true;
					Ce = null;
				}
			}

			void OnObjectLoop()
			{
				if (!IsSignificant)
					return;
				if (Context[ContextVar.ObjectLoopHasIterator] == null)
					if (tk.Type == TokenType.Identifier)
					{
						Cs.AsConditionalObjectLoop().Iterator = new VariableReference(content) { Location = CurLoc };
						Context[ContextVar.ObjectLoopHasIterator] = true;
					}
					else
						AddErrorPre("Illegal {0}, expecting iteration identifier for object enumeration.");
				else
					if (Context[ContextVar.ObjectLoopNeedsCollection] == null)
						if (tk.IsIdentifier(StringMap.ObjectLoopSeparator))
						{
							Context[ContextVar.ObjectLoopHasIterator] = null;
							Context[ContextVar.ObjectLoopNeedsCollection] = true;
						}
						else
							if (tk.IsSymbol(","))
								if (Context[ContextVar.ObjectLoopHasIndexer] == null)
								{
									Context[ContextVar.ObjectLoopHasIndexer] = true;
									Cs.AsConditionalObjectLoop().Key = Cs.AsConditionalObjectLoop().Iterator;
									Cs.AsConditionalObjectLoop().Iterator = null;
									Context[ContextVar.ObjectLoopHasIterator] = null;
								}
								else
									AddErrorPre("Object loop already has the indexer {0}.".Fmt(Cs.AsConditionalObjectLoop().Key));
							else
								AddErrorPre("Illegal {{0}}, expecting \"{0}\" keyword.".Fmt(StringMap.ObjectLoopSeparator));
					else
						throw new Exception("We're suppose to pass expression parsing onto OnExpression, why are we still here?");
			}

			void OnDirectiveVariable()
			{
				Context[ContextVar.ReadyForDirectiveVar] = null;
				if (tk.Type == TokenType.Identifier)
					if (GetIdentifierType() == IdentifierType.None)
						if (Options.ParsingMode == ParseModes.Singleplayer)
							Ce = new Constant(Runtime.VarType.DirectiveVar, content) { Location = SavedLocation };
						else
							AddErrorPre("Directive variables are single player parsing mode only.");
					else
						AddErrorPre("{0} is reserved and cannot be used as an identifier for directive variables.");
				else
					AddErrorPre("Unexpected {0}, expecting directive variable.");
			}

			void OnStatement()
			{
				IdentifierType idenType = GetIdentifierType();
				if (Cs.IsConditionalStatement() && !Cs.IsConverseStatement() &&
					!Cs.AsConditionalStatement().StartedCondition)
				{
					if (!IsSignificant)
						return;

					if (!tk.IsSymbol("("))
						AddErrorPre("Unexpected {0}, expecting opening parenthesis.");
					else
						Cs.AsConditionalStatement().StartedCondition = true;
				}//All conditional handlers come after this block because this block handles the opening parenthesis.
				else if (Context[ContextVar.CaseNeedsExpression] != null && IsSignificant && !tk.Type.IsTokenConstant()
					&& !SsEmpty && SsPk.IsCaseExpression() && !EsPk.AsCaseExpression().IsDefault)
					AddErrorPre("Unexpected {0}, expecting constant for case expression.");
				else if (CheckCsObjectLoop() && Context[ContextVar.ObjectLoopNeedsCollection] == null && IsCsUnfinishedCondition)
					OnObjectLoop();
				else if (CheckEsAsCase() && Context[ContextVar.CaseNeedsExpression] == null)
					OnCase();
				else if (Context[ContextVar.ScopeQualifier] != null)
					OnScopeQualifier();
				else if (Context[ContextVar.ReadyForDirectiveVar] != null)
					OnDirectiveVariable();
				else if (!EsEmpty && EsPk.IsFunctionCall() && !EsPk.AsInvocation().StartedParameterList)
				{
					if (!IsSignificant)
						return;
					FunctionCall fc = EsPk.AsFunctionCall();
					if (tk.IsSymbol(":") && GetConsecutiveSymbols(":") == "::")
					{
						Reader.DiscardLastConsec();
						//If it has a function name to correct
						if (fc.HasFunction())
							//If it doesn't already have an external-scope-qualifier
							if (fc.Function.ScopeType == Reference.ScopeTypes.Local)
								//If it isn't explicitly local, meaing not like this: thread ::foobarfunc();
								if (!fc.Function.LocalExplicit)
								{
									Mc.StringList.Clear();
									Mc.StringList.Add(fc.Function.Name);
									fc.Function = null;
									Context[ContextVar.NeedsFinalReferenceName] = true;
									Context[ContextVar.ScopeQualifier] = true;
								}
								else
									AddError("Cannot local reference another local reference.");
							else
								AddError("Cannot add function reference to already fully-qualified scope specifier.");
						else
						{
							Context[ContextVar.NeedsFinalReferenceName] = true;
							Context[ContextVar.ScopeQualifier] = true;
						}
					}
					else if (tk.IsSymbol("\\"))//Make the onExpression procedure for symbol '\' trigger reinterpreting for varrefs.
						if (fc.HasFunction())
							if (fc.Function.ScopeType == Reference.ScopeTypes.Local && !fc.Function.LocalExplicit)
								if (!fc.Function.LocalExplicit)
								{
									Mc.StringList.Clear();
									Mc.StringList.Add(fc.Function.Name);
									SaveLocation(fc.Function.Location);
									fc.Function = null;
									Context[ContextVar.ScopeQualifier] = true;
								}
								else
									AddErrorPre("Unexpected {0}, function already has fully qualified name.");
							else
								AddErrorPre("Unexpected {0}, cannot add another function reference in already fully qualified function name.");
						else
							AddErrorPre("Unexpected {0}, full scope qualified must start with path.");

					else if (tk.IsSymbol("("))
						fc.StartedParameterList = true;

					else if (!fc.HasFunction() && tk.Type == TokenType.Identifier && idenType == IdentifierType.Modifier)
						AddModifier(StringMap.CallModKeys.Get());
					else if (!fc.HasFunction() && tk.IsSymbol("[") && GetConsecutiveSymbols("[") == "[[")
					{
						Reader.DiscardLastConsec();
						Es.Pop();
						EsPush(fc.ConvertToPointerCall());
					}
					else if (tk.Type == TokenType.Identifier)
						if (idenType == IdentifierType.None)
							if (!fc.HasFunction())
								fc.Function = new FunctionReference(null) { Name = content, Location = CurLoc };
							else
								AddErrorPre("Unexpected identifier {{0}}, expecting scope qualification or parameter list. Or unknown call modifier \"{0}\".".Fmt(fc.Function.Name));
						else
							AddErrorPre("{0} is reserved and cannot be used as part of a function call.");
					else
						AddErrorPre("Unexpected {0}, expecting parameter list or path/scope qualifier.");
				}
				else if (CheckEsAsStatement(Statement.StatementTypes.PointerCall) && EsPk.AsInvocation().HasFunction()
						&& !EsPk.AsInvocation().StartedParameterList && IsSignificant)
					if (tk.IsSymbol("("))
						EsPk.AsInvocation().StartedParameterList = true;
					else
						AddErrorPre("Unexpected {0}, expecting parameter list following function pointer invocation.");
				else if (Ce != null && Ce.IsFlowControl() && IsSignificant && !tk.IsSymbol(";"))
					AddError("Expecting \";\" after break/continue.");
				else
				{
					OnExpression();
					if (Context[ContextVar.CaseNeedsExpression] != null && Ce != null && Es.Count == 1
						&& EsPk.GetExpressionType() == Expression.ExpressionTypes.Case)
						Context[ContextVar.CaseNeedsExpression] = null;
				}
			}

			void OnExpression()
			{
				String multi = null;

				if (!EsEmpty && EsPk.IsAccessor())
					if (tk.Type != TokenType.Identifier)
						AddErrorPre("Unexpected {0}, expecting property name identifier for accessor.");
					else
					{
						EsPk.AsAccessor().PropertyName = content;
						Ce = Es.Pop();
					}
				else
					switch (tk.Type)
					{
						case TokenType.Comment:
							break;
						case TokenType.WhiteSpace:
							break;
						case TokenType.ResourceVar:
							if (Ce == null)
								Ce = new Constant(Runtime.VarType.ResourceVar, content) { Location = CurLoc };
							else
								AddErrorPre("Unexpected resource variable {0}.");
							break;
						case TokenType.MetaString:
							if (Ce == null)
								Ce = new Constant(Runtime.VarType.MetaString, content) { Location = CurLoc };
							else
								AddErrorPre("Unexpected meta string {0}.");
							break;

						case TokenType.Number:
							if (Ce == null)
							{
								Ce = new Constant(content.Contains(".") ? Runtime.VarType.Float
									: Runtime.VarType.Integer, content) { Location = CurLoc };
								OnEvaluateConstantContent(Ce.AsConstant());
							}
							else
								AddErrorPre("Unexpected number {0}.");
							break;

						case TokenType.String:
							if (Ce == null)
								Ce = new Constant(Runtime.VarType.String, content) { Location = CurLoc };
							else
								AddErrorPre("Unexpected string {0}.");
							break;

						case TokenType.Identifier:
							IdentifierType idenType = GetIdentifierType();

							if (CheckEsAsCase() && idenType != IdentifierType.NamedConstant)
								AddErrorPre("Unexpected variable {0}, identifier after case label must be a constant.");
							else
								switch (idenType)
								{
									case IdentifierType.Condition:
										OnConditionalKeyword();
										break;

									case IdentifierType.Modifier:
										if (NamelessFunction)
											throw new Exception("Middle-ground function handling should occur up there! Not here!");
										EsPush(OnInvocation(new FunctionCall(null) { Self = Ce, Location = CurLoc }));
										Ce = null;
										AddModifier(StringMap.CallModKeys.Get());
										break;

									case IdentifierType.NamedConstant:
										if (Ce == null)
										{
											var type = StringMap.NamedConstantKeys.Get();
											Ce = new Constant(type.GetNamedConstantType(), content) { Location = CurLoc };
											OnEvaluateConstantContent(Ce.AsConstant());
										}
										else
											AddErrorPre("Unexpected constant {0} after variable.");
										break;

									case IdentifierType.Unary:
										AddUnary(StringMap.UnaryFuncKeys.Get());
										break;

									case IdentifierType.FlowControl:
										int target = 0;
										FlowControlTypes lct = StringMap.LoopControlKeys.Get();
										if (Ce == null && EsEmpty)
											switch (lct)
											{
												case FlowControlTypes.Break:
													if (DoesNearestFlowableStatementExist(false, ref target))
													{
														if (SsPk.IsConditionalStatement(ConditionalTypes.MultiConditional))
															if (SsPk.AsMultiConditional().InsideCase)
																SsPk.AsMultiConditional().InsideCase = false;
															else
																AddWarning("Redundant break in switch statement.");
														Ce = new FlowControlStatement(lct, target);
													}
													else
														AddError("No flow controllable statements near by to break.");
													break;
												case FlowControlTypes.Continue:
													if (DoesNearestFlowableStatementExist(true, ref target))
														Ce = new FlowControlStatement(lct, target);
													else
														AddError("No flow controllable statements near by to continue.");
													break;
											}
										else
											AddErrorPre("{0} cannot be used here because it is a reserved keyword for flow control.");
										break;

									case IdentifierType.MultiCase:
										SwitchKeywords sk = StringMap.SwitchKeywordKeys.Get();
										WrapCompleteConditionals(true);//Case labels are not expressions we must deal with branch graph wrapping.
										if (CheckSsMultiConditional())
											if (Ce == null && EsEmpty)
											{
												EsPush(new CaseExpression(sk == SwitchKeywords.Default));
												if (sk == SwitchKeywords.Case)
													Context[ContextVar.CaseNeedsExpression] = true;
											}
											else
												AddErrorPre("{0} cannot be used here, it is a reserved case/default label for switch statement bodies.");
										else
											AddErrorPre("{0} can only be used inside a switch statement body.");
										break;

									case IdentifierType.None:
										if (Ce == null)
											Ce = new VariableReference(content) { Location = CurLoc };
										else if (Ce.IsInlineArray())
											AddError("Cannot invoke the function \"{0}\" on an inline array.".Fmt(content));
										else
										{
											EsPush(OnInvocation(new FunctionCall()
											{
												Function = new FunctionReference() { Name = content, Location = CurLoc },
												Self = Ce,
												Location = CurLoc
											}));
											Ce = null;
										}
										break;
								}
							break;
						case TokenType.Symbol:
							switch (content)
							{
								case ":":
									if (GetConsecutiveSymbols(":") == "::")
									{
										Reader.DiscardLastConsec();
										/*TODO[FIXED]: This should only be used for local functions. According to cod's gsc rules,
											* The scope specifier is always before the function name.
											* Fixed: Actually it's meants to reference a function, that's all. We just simulate
											* the context of actually already parsing a scope qualifier.
											*/
										if (Ce != null && Ce.IsVariableReference())
										{
											Mc.StringList.Add(Ce.AsVariableReference().Name);
											Ce = null;
										}
										Context[ContextVar.NeedsFinalReferenceName] = true;
										Context[ContextVar.ScopeQualifier] = true;
									}
									else
										AddErrorPre("{0} can only be used as a scope resolution qualifier");
									break;
								case "\\":
									if (Ce != null && Ce.IsVariableReference())
										if (Ce.AsVariableReference().ScopeType == Reference.ScopeTypes.Local)
										{
											SaveLocation(Ce.Location);
											Context[ContextVar.ScopeQualifier] = true;
											Mc.StringList.Clear();
											Mc.StringList.Add(Ce.AsVariableReference().Name);
											Ce = null;
										}
										else
											AddErrorPre("Unexpected {0}, scope qualifier already exists.");
									else
										AddErrorPre("Unexpected {0}, {0} is used only as a path divider in a scope qualifier.");
									break;
								case "(":
									if (Ce == null)
										EsPush(new Group() { Location = CurLoc });
									else
										if (Ce.IsVariableReference())
										{
											FunctionCall cs = new FunctionCall()
											{
												Function = new FunctionReference(null) 
												{
													Name = Ce.AsVariableReference().Name,
													Location = Ce.Location
												},
												StartedParameterList = true,
												Location = Ce.Location
											};
											Ce = null;
											EsPush(cs);
										}
										else if (Ce.IsReference(Reference.ReferenceTypes.Function)) //maps\blah::func(); ::a();
										{
											FunctionCall cs = new FunctionCall()
											{
												Function = Ce.AsFunctionReference(),
												StartedParameterList = true,
												Location = Ce.Location
											};
											EsPush(cs);
											Ce = null;
										}
										else
											AddErrorPre("Unexpected {0}.");
									break;
								case "#":
									if (Ce != null)
										AddErrorPre("Unexpected {0}");
									else
									{
										Context[ContextVar.ReadyForDirectiveVar] = true;
										SaveLocation();
									}
									break;
								case ",":
									if (Ce == null)
										if (EsEmpty)
											StopErrorStartStatement();
										else if (EsPk.IsInvocation())
											AddError("Empty parameter in function invocation.");
										else if (EsPk.IsTruple())
											AddError("Empty parameter in truple expression.");
										else if (EsPk.GetExpressionType() == Expression.ExpressionTypes.InlineArray)
											AddError("Empty array element.");
										else
											AddErrorPre("Unexpected {0}");
									else if (EsEmpty)
											AddErrorPre("Unexpected {0}");
										else if (IsEsPkSealableBinding(true))
											ImmediateResolve();
										else if (CheckEs(Expression.ExpressionTypes.Truple))
											if (EsPk.AsTruple().Items.Count >= Truple.MaxItems)
												AddError("Too many items in truple.");
											else
											{
												EsPk.AsTruple().Items.Add(Ce);
												Ce = null;
											}
										else if (EsPk.IsInvocation() && EsPk.AsInvocation().StartedParameterList) //BUG: IF we're building a ptr invoke and the ptr expression has a comma, no further
										//checking of the invocation state can cause this to WRONGFULLY PASS
										{
											EsPk.AsInvocation().Arguments.Add(Ce);
											Ce = null;
										}
										else if (CheckEs(Expression.ExpressionTypes.Group))
										{
											Group g = Es.Pop().AsGroup();
											Truple t = new Truple() { Location = g.Location };
											t.Items.Add(Ce);
											EsPush(t);
											Ce = null;
										}
										else if (CheckEs(Expression.ExpressionTypes.InlineArray))
										{
											EsPk.AsInlineArray().Items.Add(Ce);
											Ce = null;
										}
										else
											AddErrorPre("Unexpected {0}");
									break;

								case ")":
									if (!EsEmpty && IsEsPkSealableBinding(true))
										ImmediateResolve();
									else if (EsEmpty && Cs.IsConditionalStatement() && IsCsUnfinishedCondition)
										OnConditionalExpressionEnd();
									else if (Ce == null)//No ready expression
										if (EsEmpty)
											StopErrorStartStatement();
										//It's part of something, but has no complete expression
										else if (EsPk.IsGroup())//Part of a group, but no expression
											AddError("Cannot have an empty expression group.");
										else if (EsPk.IsInvocation())//A function call without parameters.
											if (EsPk.AsInvocation().StartedParameterList)
												if (EsPk.AsInvocation().Arguments.Count == 0)//Whether we really want to have no params.
													Ce = Es.Pop();
												else//We have no complete expression
													AddErrorPre("Unexpected {0}, expecting parameter or closing parenthesis.");
											else
												AddErrorPre("Unexpected {0}, expecting parameter list.");
										else
											AddErrorPre("Unexpected {0}, not inside a parameter-list/group.");
									else
										if (IsEsPkSealableBinding(true)) // Groupings can group assignments are expressions.
											ImmediateResolve();
										else if (EsPk.IsInvocation())
										{
											FunctionInvocation fi = Es.Pop().AsInvocation();
											fi.Arguments.Add(Ce);
											Ce = fi;
										}
										else if (EsPk.IsTruple())
											if (EsPk.AsTruple().Items.Count != Truple.MaxItems - 1)
												AddError("Truple does not have 3 items.", null, EsPk.Location);
											else
											{
												Truple t = Es.Pop().AsTruple();
												t.Items.Add(Ce);
												Ce = t;
											}
										else if (EsPk.IsGroup())
										{
											Group g = Es.Pop().AsGroup();
											g.Value = Ce;
											Ce = g;
										}
										else
											AddErrorPre("Unexpected {0}. No group/truple/parameter-list to close.");
																		
									break;

								// We cant use += beacuse the plus interferes with this case.
								case "-":
								case "+":
									Token _tk = tk;
									StringMap.UnaryOpKeys.Contains(content);
									UnaryOperationType utype = StringMap.UnaryOpKeys.Get();
									if (Ce == null)
										if (utype == UnaryOperationType.Positive &&
											!Options.Allow_PositiveUnaryPostfixOperator)
											AddError("Postive unary operator is not allowed.");
										else
											EsPush(new UnaryOperation(utype) { Location = _tk.Location });
									else
									{
										multi = GetConsecutiveSymbols("+=-", 2);//We interfere with the "?=" assignments, we can reconcile here.
										if (multi == "++" || multi == "--")
										{
											Reader.DiscardLastConsec();
											if (Ce.IsReference() && !Ce.IsReference(Reference.ReferenceTypes.Function))
											{
												StringMap.UnaryOpKeys.Contains(multi);
												utype = StringMap.UnaryOpKeys.Get();
												Ce = new PostFixStatement(utype) { Operand = Ce, Location = _tk.Location };
											}
											else
												AddErrorPre("Postfix {0} operator must be applied to variable-references/accessors/indexers");
										}
										else
											OnSymbolSet(multi);
									}
									break;

								case ".":
									if (Ce == null)
										AddErrorPre("Accessor operator needs left hand expression.");
									else if (Options.Enable_StrictAccessor && (!Ce.IsReference() || Ce.IsReference(Reference.ReferenceTypes.Function))
										&& !Ce.IsInvocation())
										AddError("Strict accessor must have an accessor/variable/indexer as the left expression.");
									else
									{
										Accessor accessor = Accessor.Create(Ce);
										EsPush(accessor);
										Ce = null;
									}
									break;
								case "{":
									OnStatementBlockBegin();
									break;
								case "}":
									OnStatementBlockEnd();
									break;
								case ";":
									if (EsEmpty)//We are ready to end statement.
									{
										if (!Options.Allow_EmptyStatements && Ce == null &&
											!IsCsUnfinishedCondition)
										{
											AddError("Empty statements are not allowed.");
											return;
										}
										else if (Mc.Scope != ScopeMode.Expression)
											OnStatementEnd();
										else
											AddErrorPre("Unexpected {0}, expression scope does not allow statements.");
									}
									else
										if (Ce == null && !EsPk.IsUnaryCall())
											AddError("Unexpected end of statement.");
										else
											ImmediateResolve();//Keep resolving
									break;
								case "[":
									_tk = tk;
									if (GetConsecutiveSymbols("[") == "[[")
									{
										Reader.DiscardLastConsec();
										OnDoubleBracket();
									}
									else
									{
										if (Ce == null)
											/*if (EsEmpty && Mc.Scope != ScopeMode.Expression)
												AddErrorPre("Unexpected {0}, cannot have an inline array as left value for assignment.");
											else*/
											{
												if (!EsEmpty && EsPk.IsInlineArray())
													AddWarning("Nesting inline arrays is discouraged, as it may cause conflicts with pointer invocations.");
												EsPush(new InlineArray() { Location = _tk.Location });
											}
										else
											if (Ce.GetExpressionType().IsIndexable() &&
												(!Ce.IsStatement() || Ce.AsStatement().GetStatementType().IsIndexable()) &&
												(!Ce.IsReference() || Ce.AsReference().GetReferenceType().IsIndexable()) &&
												(!Ce.IsConstant() || Ce.AsConstant().ConstantType.IsIndexable()))
											{
												EsPush(new Indexer() { Array = Ce, Location = _tk.Location });
												Ce = null;
											}
											else
												AddError("Can only index expressions that are strings/references/arrays.");
									}
									break;
								case "]":
									//TODO: Interesting issue, nested indexer conditions causes interference with
									//the ending function invoke symbols. I guess for now encourage the current
									//specification and dont use inline arrays.
									if (!EsEmpty)
										if (/*!EsPk.IsIndexer() && !EsPk.IsInlineArray()*/
											IsEsPkSealableBinding(true))
											if (Ce != null)
												ImmediateResolve();
											else
												AddErrorPre("Unexpected {0}.");
										else if (CheckEsAsReference(Reference.ReferenceTypes.Indexer))
											if (Ce.IsStatement(Statement.StatementTypes.Assignment) &&
												!Options.Allow_AssignmentsAsExpressions)
											{
												AddError("Cannot use assignments as expressions inside indexer.");
												break;
											}
											else
												if (Ce == null)
													AddError("Cannot have empty index expression.");
												else
												{
													EsPk.AsIndexer().Index = Ce;
													Ce = Es.Pop();
												}
										else if (CheckEs(Expression.ExpressionTypes.InlineArray))
											if (!Options.Allow_InlineArrays &&
												(EsPk.AsInlineArray().Items.Count > 0 || Ce != null))
												AddError("Inline arrays are not allowed.");
											else
											{
												if (Ce != null)
													EsPk.AsInlineArray().Items.Add(Ce);
												Ce = Es.Pop();
											}
										else if (GetConsecutiveSymbols("]", 2, true) == "]]") //Do this last to wrap up indexers or inline arrays
										{
											Reader.DiscardLastConsec();
											if (Ce != null)
												if (!EsEmpty)
													if (IsEsPkSealableBinding())
														ImmediateResolve();
													else
														if (CheckEsAsStatement(Statement.StatementTypes.PointerCall))
															if (!EsPk.AsPointerFunctionCall().StartedParameterList)
															{
																EsPk.AsPointerFunctionCall().FunctionPointer = Ce;
																Ce = null;
															}
															else
																AddError("Expecting parameter list for function pointer call.");
														else
															AddErrorPre("Unexpected {0}.");
												else
													AddErrorPre("Unexpected {0}, expecting pointer call expression first.");
											else
												AddError("Pointer call needs pointer expression.");
										}
										else
											AddErrorPre("Unexpected {0}, no inline array or indexer to close.");
									else
										StopErrorStartStatement();
									break;
								default:
									multi = GetConsecutiveSymbols(StringMap.ConsecOpSymbols);
									OnSymbolSet(multi);
									break;
							}
							break;
						case TokenType.EOF:
							if (Mc.Scope == ScopeMode.Expression)
								OnEndScopeExpression();
							else if (Mc.Scope == ScopeMode.Function)
								OnEndScopeFunction();
							else
								throw new Exception("We shouldn't handle the EOF in OnExpression when we're in module scope.");
							break;
						default:
							throw new Exception("Unknown token type: " + tk.Type.ToString());
					}
			}

			/// <summary>
			/// Checks EsPk as a binary operation and checks to see
			/// if it has lower precedence than the specified
			/// operation type.
			/// </summary>
			/// <param name="type">The type to compare to.</param>
			/// <returns>Whether EsPk binary operation type has a lower precedence value than <c>type</c>.</returns>
			Boolean IsHigherThanEsPkPrecedence(BinaryOperationType type)
			{
				return StringMap.OperationOrder[EsPk.AsBinaryOperation().OperationType] > StringMap.OperationOrder[type];
			}

			void OnSymbolSet(String multi)
			{
				Token _tk = tk;
				Boolean isAssign = false, isBinary = false;
				String test = null;
				int i = multi.Length;

				//Good solution to multi symbol ambiguity without resorting to extra tokenizer logic.
				while (i > 0)
				{
					test = multi.Substring(0, i);
					isAssign = StringMap.OpAssKeys.Contains(test);
					isBinary = StringMap.OpKeys.Contains(test);
					if (isAssign || isBinary)
						break;
					--i;
				}

				if (isBinary || isAssign)
				{
					//Be careful if we deferred, to ensure we arrive at the same place, don't fuck with the backing queues.
					if (!IsDeferred)
						Reader.DiscardBackingStore(test.Length - 1);
					BinaryOperationType opType = StringMap.OpKeys.Get();
					if (isAssign)
						if (Ce == null)
							if (EsEmpty)
								AddError("The \"{0}\" assignment needs a left hand expression.".Fmt(test));
							else
								AddError("Unexpected \"{0}\" assignment.".Fmt(test));
						else if (!Options.Allow_AssignmentsAsExpressions &&
							//Check whether we're becoming an expression by using !EsEmpty or if Ce is an assignment itself.
							(Ce.IsStatement(Statement.StatementTypes.Assignment) || !EsEmpty))
							AddError("Assignments as expressions are not allowed.");
						else if (Ce.IsReference(Reference.ReferenceTypes.Function))
							AddError("Cannot assign function reference.");
						/*a = a = f + 1; Since we're pushing an assignment, might as well resolve
																expressions that are assignments.*/
						/*&& !EsPk.IsStatement(Statement.StatementTypes.Assignment)*/
						else if (!EsEmpty && IsEsPkSealableBinding(false))
							ImmediateResolve();
						else
							if (Ce.IsReference() && !Ce.IsReference(Reference.ReferenceTypes.Function))
							{
								EsPush(new AssignmentStatement(StringMap.OpAssKeys.Get()) { Left = Ce, Location = _tk.Location });
								Ce = null;
							}
							else
								AddErrorPre("Invalid left-value expression for assignment.");
					else
						AddBinaryOperation(opType, _tk);
				}
				else//From here on out, we've dealt with the possibilites that include multi-symbol operators.
					if (StringMap.UnaryOpKeys.Contains(content))
						if (!EsEmpty && Ce != null && IsEsPkSealableBinding(false))
							ImmediateResolve();
						else
							if (Ce == null)
							{
								//Reader.DiscardLastConsec(); Only reading content, not multi
								UnaryOperationType uo = StringMap.UnaryOpKeys.Get();
								if (uo.IsPostfixUnary())
									throw new Exception("Unary operator +/- should be handled individually up there.");
								if (Ce != null)
									AddErrorPre("Unexpected {0}.");
								else
									EsPush(new UnaryOperation(uo) { Location = _tk.Location });
							}
							else
								AddErrorPre("Unexpected {0}.");
					else
						if (StringMap.OpKeys.Contains(content))
							AddBinaryOperation(StringMap.OpKeys.Get(), _tk);
						else
							AddErrorPre("Unknown operation {0}.");
			}

			void ImmediateResolve()
			{
				if (EsEmpty)
					throw new Exception("Attmpted to resolve when unresolved expression stack is empty");
				//This will fail miserably if there are any expressions that actually accept a null Ce.

				if (EsPk.IsUnaryOperation(UnaryOperationType.Negative) && (Ce.IsConstant(Runtime.VarType.Integer)
					|| Ce.IsConstant(Runtime.VarType.Float)))
				{
					Es.Pop();
					Ce.AsConstant().Value = "-" + Ce.AsConstant().Value;
				}
				else if (EsPk.IsUnaryCall()) //Instant unary call resolve, should be only one in Es.
				{
					if (Ce != null)
						EsPk.AsUnaryCall().Arg = Ce;
					Ce = Es.Pop();
					if (!EsEmpty)
						throw new Exception("We should have stop error'd at creating UnaryCall and checking EsEmpty!");
				}
				else
				if (Ce == null)
					throw new Exception("Ce null when performing immediate resolve");
				else
				{
					Expression.ExpressionTypes type = EsPk.GetExpressionType();
					if (EsPk.IsBinaryOperation() ||
						EsPk.IsUnaryOperation() ||
						EsPk.IsStatement(Statement.StatementTypes.Assignment))
					{
						EsPk.Add(Ce);
						if (!EsPk.IsComplete())
							throw new Exception("Could not resolve {0}.".Fmt(EsPk.GetExpressionType()));
						Ce = Es.Pop();
					}
					else
						if (type == Expression.ExpressionTypes.Group)
							AddErrorPre("Unexpected {0}, expecting closing parenthesis.");
						else
							if (EsPk.IsReference(Reference.ReferenceTypes.Indexer))
								AddErrorPre("Unexpected {0}, expecting \"]\" to complete indexer.");
							else
								if (type == Expression.ExpressionTypes.InlineArray)
									AddErrorPre("Unexpected {0}, expecting \"]\" to complete inline array.");
								else
									AddErrorPre("Unexpected {0}");
				}
				DeferToken();
			}

			Boolean StatementCheck()
			{
				if (!Options.Allow_ExpressionsAsStatements && Ce != null && !Ce.IsStatement())
				{
					AddError("Cannot use an expression as a statement.", null, Ce.Location);
					return false;
				}
				else if (EsEmpty && Ce != null && Mc.Scope == ScopeMode.Expression)
				{
					AddErrorPre("Unexpected {0}, expression scope does not allow statements.");
				}
				return true;
			}

			/*
			 * On ')' when Cs is a conditional 
			 */
			void OnConditionalExpressionEnd()
			{
				var CS = Cs.AsConditionalStatement();

				switch (CS.GetConditionType())
				{
					case ConditionalTypes.Converse:
						AddErrorPre("Illegal {0}, \"else\" statement does not have a conditional expression.");
						break;
					case ConditionalTypes.LoopEx:
						if (CS.AsConditionalLoopExStatement().Progress == ConditionalLoopExStatement.ConditionProgress.NeedRepeater)
						{
							CS.AsConditionalLoopExStatement().AddPart(Ce);
							CS.FinishedConditional = true;
						}
						else
							AddError("\"for\" statement has too little or too many statement/expressions in statement/expressions list.");
						break;
					case ConditionalTypes.ObjectLoop:
						if (Ce != null)
						{
							CS.AsConditionalObjectLoop().Collection = Ce;
							CS.FinishedConditional = true;
							Context[ContextVar.ObjectLoopNeedsCollection] = null;
							Context[ContextVar.ObjectLoopHasIndexer] = null;
						}
						else
							AddError("Foreach loop must have a collection to iterate.");
						break;
					default:
						if (Ce != null)
						{
							CS.SetCondition(Ce);
							CS.FinishedConditional = true;
						}
						else
							AddError("Conditional statement \"{0}\" needs a conditional expression.".Fmt(StringMap.CondKeys[CS.GetConditionType()]));
						break;
				}

				Ce = null;
			}

			/// <summary>
			/// If cs is a complete conditional. Meaning Cs has a primary
			/// statement and is a non-bicond, or if it is a bicond
			/// and has both a primary and converse.
			/// </summary>
			/// <returns></returns>
			Boolean IsCompleteConditional(Boolean halfBiConds = false)
			{
				if (!Cs.IsConditionalStatement())
					return false;
				if (Cs.IsConditionalStatement(ConditionalTypes.BiConditional))
					if (!halfBiConds)
						return Cs.AsBiConditionalStatement().HasConverseStatement;
					else
						return Cs.AsBiConditionalStatement().HasStatement;
				return Cs.AsConditionalStatement().HasStatement;
			}

			void OnConditionalKeyword()
			{
				if (Mc.Scope == ScopeMode.Expression)
				{
					AddErrorPre("Unexpected {0}, expression scope does not allow conditional loops/blocks.");
					return;
				}

				WrapCompleteConditionals(false);
				if (Ce == null && EsEmpty)
				{
					ConditionalTypes ct = StringMap.CondKeys.Get();
					switch (ct)
					{
						case ConditionalTypes.Converse:
							if (Cs.IsConditionalStatement(ConditionalTypes.BiConditional))
							{
								//We try the next approach, but we don't do anything???
								SsPush(Cs);
								Cs = new ConverseStatementPlaceHolder();
							}
							else
								AddError("No completed \"if\" statement before \"else\".");
							break;
						case ConditionalTypes.ObjectLoop:
							WrapCompleteConditionals(true);
							SsPush(Cs);
							Cs = new ConditionalObjectLoopStatement() { Location = CurLoc };
							Context[ContextVar.ObjectLoopNeedsCollection] = null;
							break;
						default:
							WrapCompleteConditionals(true);
							ConditionalStatement cs = Activator.CreateInstance(
								StringMap.CondTypeMap[StringMap.CondKeys.Get()]) as ConditionalStatement;
							if (ct == ConditionalTypes.MultiConditional)
								cs.AsMultiConditional().InsideCase = false;
							cs.Location = CurLoc;
							SsPush(Cs);
							Cs = cs;
							break;
					}

					Cs.Location = CurLoc;
				}
				else
					AddErrorPre("{0} is reserved as a conditional statement.");
			}
			/// <summary>
			/// Wraps up complete conditionals under more conditionals.
			/// </summary>
			void WrapCompleteConditionals(Boolean halfBiConds = false)
			{
				while (IsCompleteConditional(halfBiConds))
				{
					if (SsPk.IsConverseStatement())
					{
						var temp = Cs;
						PopSs();
						SsPk.AsBiConditionalStatement().SetConverseStatement(temp);
					}
					else if (SsPk.IsConditionalStatement())
						SsPk.AsConditionalStatement().SetStatement(Cs);
					else if (SsPk.IsStatementBlock())
							SsPkSl.Add(Cs);
					else
						throw new NotImplementedException();
					PopSs();	
				}
			}

			// TODO: Fails when we pass a Ce that's actually a statement block. [fixed]
			void OnStatementEnd()
			{
				if (!IsCsUnfinishedCondition)
					WrapCompleteConditionals(true);
				//If it's in a group, take it out. It's redundant and
				//what's important is what's inside, because group basically
				//does nothing but structure the DOM as it parses.
				if (Ce != null && Ce.GetExpressionType() == Expression.ExpressionTypes.Group)
				{
					Ce = Ce.AsGroup().Value;
					DeferToken();
					return;
				}
				//Dunno if it's helpful to assume that a single identifier
				//is a single unary call.
				else if (Ce != null && Ce.IsVariableReference()
					&& Ce.AsVariableReference().Scope == null)
					if (StringMap.UnaryFuncKeys.Contains(Ce.AsVariableReference().Name))
						Ce = new UnaryCall(StringMap.UnaryFuncKeys.Get()) { Arg = null, Location = Ce.Location };
					else
					{
						AddError("Unknown unary function {0}.".Fmt(Ce.AsVariableReference().Name));
						return;
					}
				/*
				 * Ending on ';' inside conditional
				 */
				if (HasStartedConditional)
				{
					var CS = Cs.AsConditionalStatement();
					//Adding an expression in the statement list for a forloop.
					if (!CS.FinishedConditional)
						if (CS.GetConditionType() == ConditionalTypes.LoopEx)
						{
							if (CS.AsConditionalLoopExStatement().Progress != ConditionalLoopExStatement.ConditionProgress.Done)
							{
								//Here, we will also accept null expressions for "for" statements.
								CS.AsConditionalLoopExStatement().AddPart(Ce);
								Ce = null;
							}
							else//Incase other non-normal conditional expressions come across one of these.
								AddError("\"for\" loop must have only 3 expressions/statements.");
						}
						else
							AddErrorPre("Unexpected {0}, expecting closing parenthesis.");
					else //A single non-blocked statement. Make sure it's not on a switch statement.
						AddCeToConditionalCs();
				}//Allow constant declarations so we don't have to use fake statementblocks.
				else
					AddCeToSl();
			}

			void AddCeToSl()
			{
				if (SsEmpty || !SsPk.IsConditionalStatement(ConditionalTypes.MultiConditional)
					|| SsPk.AsMultiConditional().InsideCase
					|| Ce.IsFlowControl(FlowControlTypes.Break))
				{
					if (StatementCheck())
					{
						if (Cs.IsConditionalStatement())
							throw new Exception("Handling single statement as rooted statement inside unfinished conditional.");
						Sl.Add(CeAsStatement);
						Ce = null;
					}
				}
				else
					AddError("Unexpected statement, statements needs to be under a case or default label.");
			}

			void OnStatementBlockBegin()
			{
				if (EsEmpty && Ce == null)
					if (!IsCsUnfinishedCondition)//Make sure we're not inside an unfinished conditional.
						if (Mc.Scope != ScopeMode.Expression)
						{
							WrapCompleteConditionals(true);
							SsPush(new StatementBlock() { Location = CurLoc });
						}
						else
							AddErrorPre("Unexpected {0}, expression scope does not allow statement blocks.");
					else
						AddErrorPre("Illegal {0}, cannot start statement block inside conditional expression.");
				else
					AddErrorPre("Unexpected {0}, Cannot start statement block inside expression.");
			}

			//TODO: Statement body binding to conditional statements. [fixed]
			void OnStatementBlockEnd()
			{
				WrapCompleteConditionals(true);//Clean up the inside statement list.

				if (Cs.IsStatementBlock())
					if (Ce == null && EsEmpty)
					{
						StatementBlock sb = Cs.AsStatementBlock();
						//Save our statements. Go into the current block.
						sb.Statements = Sl.ToStatementCollection();

						if (!SsEmpty && SsPk.IsConditionalStatement(ConditionalTypes.MultiConditional) &&
							sb.Statements.Length == 0 && !Options.Allow_EmptyMultiConditionals)
							AddError("Empty switch statements are not allowed.");

						if (Ss.Count == 0)
							switch (Mc.Scope)
							{
								case ScopeMode.Module: //Finalize function
									Mc.CurrentFunction.RootStatement = sb;
									Mc.Declarations.Add(Mc.CurrentFunction);
									Mc.CurrentFunction = null;
									Sl.Clear();
									SwitchLevel(ParseLevel.Root);
									break;
								case ScopeMode.Function:
								case ScopeMode.Expression:
									AddErrorPre("Unexpected {0}, not in module scope.");
									break;
							}
						else
						{
							PopSs();
							/*if (Cs.IsConditionalStatement(ConditionalTypes.MultiConditional))
								if (Cs.AsMultiConditional().InsideCase && Cs.AsMultiConditional().Cases.Last().IsDefault)
									AddError("Mulitconditional missing a break statement after default statement.");*/
							Ce = sb;
							if (Cs.IsStatementBlock())
								AddCeToSl();
							else //Could be anything else! I guess we're assuming conditional statements.
								AddCeToConditionalCs();
						}
					}
					else
						AddErrorPre("Incomplete statement/expression. Unexpected {0} inside statement/expression.");
				else
					AddErrorPre("Unexpected {0}, incomplete conditional.");
			}

			void AddCeToConditionalCs()
			{
				var CS = Cs.AsConditionalStatement();
				if (CS.GetConditionType() == ConditionalTypes.MultiConditional && (Ce == null
					|| !Ce.IsStatementBlock()))
					AddError("Switch statement must have a block-statement body.");
				if (Ce == null)
					if (Options.Allow_EmptyStatementsAfterConditionals)
						AddWarning("Empty statement after conditional. Is this intended?");
					else
					{
						AddError("Empty statement after conditional.");
						return;
					}

				if (CS.GetConditionType() != ConditionalTypes.Converse)
				{
					if (CS.HasStatement) //If it already possesses a statement, we're exiting out of half statements
						WrapCompleteConditionals(true);
					CS.SetStatement(CeAsStatement);
					Ce = null;
					WrapCompleteConditionals();
				}
				else if (SsPk.IsBiConditionalStatement())
					if (SsPk.AsBiConditionalStatement().HasStatement)
						if (!SsPk.AsBiConditionalStatement().HasConverseStatement)
						{
							SsPk.AsBiConditionalStatement().SetConverseStatement(CeAsStatement);
							PopSs();
							Ce = null;
							WrapCompleteConditionals();
						}
						else
							AddError("\"if\" statement already has an \"else\" body.");
					else
						AddError("\"if\" statement needs a statement/body first.");
				else
					AddError("No \"if\" statement prehand to \"else\".");
			}

			void AddBinaryOperation(BinaryOperationType btype, Token originToken)
			{
				if (Ce != null)
					if (!EsEmpty && IsEsPkSealableBinding(false) &&
						(!EsPk.IsBinaryOperation() || !IsHigherThanEsPkPrecedence(btype)))
						ImmediateResolve();
					else if (Ce.IsReference(Reference.ReferenceTypes.Function))
						AddError("Cannot use a function reference as an operand for a binary operator.");
					else
					{
						BinaryOperation bo = new BinaryOperation(Ce) { OperationType = btype, Location = originToken.Location };
						EsPush(bo);
						Ce = null;
					}
				else
					AddErrorPre("Binary operator {{0}} needs left hand expression.{0}".Fmt(
						btype == BinaryOperationType.Modulo ? " (If you meant to use a resource variable, enable singleplayer parsing.)" : ""));
			}

			/// <summary>
			/// Whether the expression is a binary operation, a unary operation,
			/// or if it's an assignment based on <paramref name="allowAssignments"/>.
			/// Determines whether we can use Ce to seal as of right now.
			/// </summary>
			/// <param name="allowAssignments"></param>
			/// <returns></returns>
			public Boolean IsEsPkSealableBinding(Boolean allowAssignments = true)
			{
				//Postfix operators are never pushed onto Es anyways.
				return EsPk.IsBinaryOperation() || EsPk.IsUnaryOperation() ||
					(allowAssignments && EsPk.IsStatement(Statement.StatementTypes.Assignment));
			}

			void DeferToken()
			{
				//Do not putstore, because we'll on putstore the later token that we don't want.
				Defer = true;
			}

			//We may need a sepearte DiscardConsec because we don't
			//want to discard symbols from a deferred read, otherwise
			//we'd be throwing away symbols for no good reason.
			//But the internal consec length resets so there's no problem.
			String GetConsecutiveSymbols(String symbols, int max = 6, Boolean forceIgnoreDefferd = false)
			{
				if (IsDeferred)
					if (!forceIgnoreDefferd)
						return Reader.ConsecString.ToString();
				return Reader.ReadConsecutiveSymbols(symbols, max);
			}

			void OnEndScopeFunction()
			{
				if (EsEmpty)
				{
					WrapCompleteConditionals(true);
					if (!SsEmpty)
						AddError("Expecting \"}\".");
					else
					{
						Cs.AsStatementBlock().Statements = Sl.ToStatementCollection();
						FinalStatements = Cs.AsStatementBlock();
						Sl.Clear();
						IsDone = true;
					}
				}
				else
					AddError("Unexpected end of statement.");
			}

			void OnEndScopeExpression()
			{
				if (EsEmpty)//We are ready to end statement.
				{
					FinalExpression = Ce;
					Sl.Clear();
					RootMode = RootModes.None;
					IsDone = true;
					//Don't switch to back to level root, fair chance someone might reuse the object and want to stay in the correct scope.
				}
				else
					if (Ce == null && !EsPk.IsUnaryCall()) //TODO: Make sure we don't allow returns for expression-scopes. [done]
						AddError("Unexpected end of statement.");
					else//TODO: Test waittillframeend, just incase. Ce should be not null anyways.
						ImmediateResolve();
			}
		}
	}
}
