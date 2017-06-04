using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace GameScriptCompiler
{
	namespace CodeDOM
	{
		class SearchMap<T>
		{
			Dictionary<T, String> Map;
			T LastFind = default(T);
			IEqualityComparer<String> Comparer;
			public SearchMap(bool forceCaseSensitive = false)
			{
				Map = new Dictionary<T,string>();
				Comparer = forceCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
			}
			public String this[T key]
			{
				get
				{
					return Map[key];
				}
				set
				{
					Map[key] = value;
				}
			}

			/// <summary>
			/// Checks to see if the string is contained in the collection.
			/// </summary>
			/// <param name="str">String to find</param>
			/// <param name="Store">Whether to store the result.</param>
			/// <returns></returns>
			public bool Contains(String str, bool Store = true)
			{
				return Map.Any(kvp =>
					{
						if (Comparer.Equals(str, kvp.Value))
						{
							if (Store)
								LastFind = kvp.Key;
							return true;
						}
						return false;
					});
			}
			public T Get()
			{
				return LastFind;
			}

			public T Get(String str)
			{
				if (this.Contains(str, true))
					return LastFind;
				return default(T);
			}
		}


		static class StringMap
		{

			public static SearchMap<ConditionalTypes> CondKeys;
			public static SearchMap<DirectiveType> DirKeys;
			public static SearchMap<BinaryOperationType> OpKeys;
			public static SearchMap<BinaryOperationType> OpAssKeys;
			public static SearchMap<CallModifiers> CallModKeys;
			public static SearchMap<NamedConstantTypes> NamedConstantKeys;
			public static SearchMap<FlowControlTypes> LoopControlKeys;
			public static SearchMap<SwitchKeywords> SwitchKeywordKeys;
			public static SearchMap<UnaryOperationType> UnaryOpKeys;
			public static SearchMap<UnaryFunctionTypes> UnaryFuncKeys;
			public static SearchMap<InternalDirectives> InternalDirs;
			public static SearchMap<ParamModifiers> ParamModKeys;
			public static Dictionary<UnaryFunctionTypes, UnaryFunctionDef> UnaryFuncs;
			public static String ConsecOpSymbols;
			public static readonly String ObjectLoopSeparator = "in", HexSpecifier = "0x";
			public static Dictionary<ConditionalTypes, Type> CondTypeMap;
			public static Dictionary<Char, Char> CharToEscape, EscapeToChar;
			public static Dictionary<BinaryOperationType, int> OperationOrder;
			public static Dictionary<InlineEvaluator.InvalidErrorType, String> InvalidOpMsgs;

			private static void AddUnaryFunc(UnaryFunctionTypes type, Boolean hasArg)
			{
				UnaryFuncs.Add(type, new UnaryFunctionDef() { Keyword = UnaryFuncKeys[type], HasArg = hasArg, UnaryType = type });
			}

			/*public static void LoadAdditionalMapData(XmlDocument doc)
			{
				Init();
				XmlElement root = doc.SelectSingleNode("/mapdata") as XmlElement;
				if (root == null)
					throw new Exception("No root node \"mapdata\"");
				foreach (var kvp in Extensibles)
				{
					XmlElement ext = doc.SelectSingleNode("./{0}".Fmt(kvp.Key)) as XmlElement;
					if (ext == null)
						continue;
				}
			}*/

			public static IEqualityComparer<String> GetComparer(Boolean caseSen)
			{
				return caseSen ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
			}

			public static void Init()
			{
				GSIL.OpcodeGen.InitAliases();

				CharToEscape = new Dictionary<char, char>();
				CharToEscape['\r'] = 'r';
				CharToEscape['\n'] = 'n';
				CharToEscape['\a'] = 'a';
				CharToEscape['\f'] = 'f';
				CharToEscape['\t'] = 't';
				CharToEscape['\v'] = 'v';
				CharToEscape['\\'] = '\\';
				CharToEscape['"'] = '"';
				EscapeToChar = new Dictionary<char, char>();
				foreach (var kvp in CharToEscape)
					EscapeToChar[kvp.Value] = kvp.Key;

				CondKeys = new SearchMap<ConditionalTypes>(true);
				CondKeys[ConditionalTypes.BiConditional] = "if";
				CondKeys[ConditionalTypes.Loop] = "while";
				CondKeys[ConditionalTypes.LoopEx] = "for";
				CondKeys[ConditionalTypes.ObjectLoop] = "foreach";
				CondKeys[ConditionalTypes.Converse] = "else";
				//CondKeys[ConditionalTypes.ConverseConditional] = "elseif";
				CondKeys[ConditionalTypes.MultiConditional] = "switch";

				CondTypeMap = new Dictionary<ConditionalTypes, Type>();
				CondTypeMap[ConditionalTypes.BiConditional] = typeof(BiConditionalStatement);
				CondTypeMap[ConditionalTypes.Converse] = typeof(ConverseStatementPlaceHolder);
				CondTypeMap[ConditionalTypes.Loop] = typeof(ConditionalLoopStatement);
				CondTypeMap[ConditionalTypes.LoopEx] = typeof(ConditionalLoopExStatement);
				CondTypeMap[ConditionalTypes.MultiConditional] = typeof(MultiConditionalStatement);
				CondTypeMap[ConditionalTypes.ObjectLoop] = typeof(ConditionalObjectLoopStatement);

				DirKeys = new SearchMap<DirectiveType>(true);
				DirKeys[DirectiveType.Include] = "include";

				//TODO: Add the rest of the assignment operators. The set does not maintain a congruency to assume
				//that an equal sign at the end means an assignment, i.e. "<= and >=".
				OpKeys = new SearchMap<BinaryOperationType>();
				OpKeys[BinaryOperationType.Addition] = "+";
				OpKeys[BinaryOperationType.BitAND] = "&";
				OpKeys[BinaryOperationType.BitOR] = "|";
				OpKeys[BinaryOperationType.BitShiftLeft] = "<<";
				OpKeys[BinaryOperationType.BitShiftRight] = ">>";
				OpKeys[BinaryOperationType.BitXOR] = "^";
				OpKeys[BinaryOperationType.BoolAnd] = "&&";
				OpKeys[BinaryOperationType.BoolEquals] = "==";
				OpKeys[BinaryOperationType.BoolNotEquals] = "!=";
				OpKeys[BinaryOperationType.BoolGreater] = ">";
				OpKeys[BinaryOperationType.BoolGreaterEquals] = ">=";
				OpKeys[BinaryOperationType.BoolLess] = "<";
				OpKeys[BinaryOperationType.BoolLessEquals] = "<=";
				OpKeys[BinaryOperationType.BoolNotEquals] = "!=";
				OpKeys[BinaryOperationType.BoolOr] = "||";
				OpKeys[BinaryOperationType.Division] = "/";
				OpKeys[BinaryOperationType.Modulo] = "%";
				OpKeys[BinaryOperationType.Multiplication] = "*";
				OpKeys[BinaryOperationType.Subtraction] = "-";
				OpKeys[BinaryOperationType.Assign] = "=";
				/*
			Addition, //a + b
			Subtraction,//a - b
			Division,//a / b
			Multiplication,//a * b
			BitAND,//a & b
			BitOR,//a | b
			BitXOR,//a ^ b
			Modulo,//a % b
			BitShiftRight, // a >> b
			BitShiftLeft, // a << b
				 */

				OpAssKeys = new SearchMap<BinaryOperationType>();
				OpAssKeys[BinaryOperationType.Assign] = "=";
				OpAssKeys[BinaryOperationType.Addition] = "+=";
				OpAssKeys[BinaryOperationType.Subtraction] = "-=";
				OpAssKeys[BinaryOperationType.Division] = "/=";
				OpAssKeys[BinaryOperationType.Multiplication] = "*=";
				OpAssKeys[BinaryOperationType.BitAND] = "&=";
				OpAssKeys[BinaryOperationType.BitOR] = "|=";
				OpAssKeys[BinaryOperationType.BitXOR] = "^=";
				OpAssKeys[BinaryOperationType.Modulo] = "%=";
				OpAssKeys[BinaryOperationType.BitShiftRight] = ">>=";
				OpAssKeys[BinaryOperationType.BitShiftLeft] = "<<=";


				StringBuilder sb = new StringBuilder("!");
				foreach (BinaryOperationType type in Enum.GetValues(typeof(BinaryOperationType)))
				{
					String s = OpKeys[type];
					if (s.Length == 1)
						sb.Append(s);
				}
				ConsecOpSymbols = sb.ToString();

				UnaryFuncKeys = new SearchMap<UnaryFunctionTypes>(true);
				UnaryFuncKeys[UnaryFunctionTypes.Return] = "return";
				UnaryFuncKeys[UnaryFunctionTypes.Wait] = "wait";
				UnaryFuncKeys[UnaryFunctionTypes.WaitTillFrameEnd] = "waittillframeend";

				UnaryFuncs = new Dictionary<UnaryFunctionTypes, UnaryFunctionDef>();
				AddUnaryFunc(UnaryFunctionTypes.Wait, true);
				AddUnaryFunc(UnaryFunctionTypes.WaitTillFrameEnd, false);
				AddUnaryFunc(UnaryFunctionTypes.Return, true);

				CallModKeys = new SearchMap<CallModifiers>(true);
				//Don't forget to update Extensions.AvailableInParseMode amd Extensions.IsModifierExclusive
				CallModKeys[CallModifiers.Thread] = "thread";
				CallModKeys[CallModifiers.Volatile] = "volatile";
				CallModKeys[CallModifiers.ChildThread] = "childthread";
				CallModKeys[CallModifiers.Call] = "call";

				NamedConstantKeys = new SearchMap<NamedConstantTypes>(true);
				NamedConstantKeys[NamedConstantTypes.False] = "false";
				NamedConstantKeys[NamedConstantTypes.Infinity] = "infinity";
				NamedConstantKeys[NamedConstantTypes.True] = "true";
				NamedConstantKeys[NamedConstantTypes.Undefined] = "undefined";

				LoopControlKeys = new SearchMap<FlowControlTypes>(true);
				LoopControlKeys[FlowControlTypes.Break] = "break";
				LoopControlKeys[FlowControlTypes.Continue] = "continue";

				SwitchKeywordKeys = new SearchMap<SwitchKeywords>(true);
				SwitchKeywordKeys[SwitchKeywords.Case] = "case";
				SwitchKeywordKeys[SwitchKeywords.Default] = "default";

				UnaryOpKeys = new SearchMap<UnaryOperationType>(true);
				UnaryOpKeys[UnaryOperationType.Negative] = "-";
				UnaryOpKeys[UnaryOperationType.Not] = "!";
				UnaryOpKeys[UnaryOperationType.Positive] = "+";
				UnaryOpKeys[UnaryOperationType.Invert] = "~";
				UnaryOpKeys[UnaryOperationType.PostDecrement] = "--";
				UnaryOpKeys[UnaryOperationType.PostIncrement] = "++";

				InternalDirs = new SearchMap<InternalDirectives>(true);
				InternalDirs[InternalDirectives.DeclarativeGlobal] = "set_global_declarative";
				InternalDirs[InternalDirectives.AddGlobalVariable] = "add_global";

				ParamModKeys = new SearchMap<ParamModifiers>(true);
				ParamModKeys[ParamModifiers.Reference] = "__ref";
				ParamModKeys[ParamModifiers.VariableArguments] = "__varg";
				ParamModKeys[ParamModifiers.Minimum] = "__min";

				OperationOrder = new Dictionary<BinaryOperationType, int>();
				OperationOrder[BinaryOperationType.Addition] = 1;
				OperationOrder[BinaryOperationType.Assign] = 10;
				OperationOrder[BinaryOperationType.BitAND] = 5;
				OperationOrder[BinaryOperationType.BitOR] = 7;
				OperationOrder[BinaryOperationType.BitShiftLeft] = 2;
				OperationOrder[BinaryOperationType.BitShiftRight] = 2;
				OperationOrder[BinaryOperationType.BitXOR] = 6;
				OperationOrder[BinaryOperationType.BoolAnd] = 8;
				OperationOrder[BinaryOperationType.BoolEquals] = 4;
				OperationOrder[BinaryOperationType.BoolGreater] = 3;
				OperationOrder[BinaryOperationType.BoolGreaterEquals] = 3;
				OperationOrder[BinaryOperationType.BoolLess] = 3;
				OperationOrder[BinaryOperationType.BoolLessEquals] = 3;
				OperationOrder[BinaryOperationType.BoolNotEquals] = 4;
				OperationOrder[BinaryOperationType.BoolOr] = 9;
				OperationOrder[BinaryOperationType.Division] = 0;
				OperationOrder[BinaryOperationType.Modulo] = 0;
				OperationOrder[BinaryOperationType.Multiplication] = 0;
				OperationOrder[BinaryOperationType.Subtraction] = 1;

				InvalidOpMsgs = new Dictionary<InlineEvaluator.InvalidErrorType, string>();
				InvalidOpMsgs[InlineEvaluator.InvalidErrorType.IncompatibleTypesForBinaryOperation] =
					"Types \"{0}\" and \"{1}\" have no compatible binary operator for \"{2}\".";
				InvalidOpMsgs[InlineEvaluator.InvalidErrorType.IncompatibleTypeForUnaryOperation] =
					"Type \"{0}\" has no compatible unary operator for \"{1}\".";
			}
		}
	}
}
