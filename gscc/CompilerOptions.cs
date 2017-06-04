using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Xml;
using System.ComponentModel;
using GameScriptCompiler.CodeDOM;

namespace GameScriptCompiler
{
	[AttributeUsage(AttributeTargets.Field)]
	public class CompilerOptionAttribute : Attribute
	{
		public String Fullname;
		public String Description;
		public CompilerOptionAttribute(String fullname, String description)
		{
			this.Fullname = fullname;
			this.Description = description;
		}
	}

	internal class FieldToPropertyOptionDescriptor : PropertyDescriptor
	{
		Option TargetOption;
		public FieldToPropertyOptionDescriptor(Option targetOption, String name, Attribute[] attributes)
			: base(name, attributes)
		{
			this.TargetOption = targetOption;
		}

		public override bool CanResetValue(object component)
		{
			return false;
		}

		public override Type ComponentType
		{
			get { return typeof(CompilerOptions); }
		}

		public override object GetValue(object component)
		{
			return TargetOption.Field.GetValue(component);
		}

		public override bool IsReadOnly
		{
			get { return false; }
		}

		public override Type PropertyType
		{
			get { return TargetOption.ValueType; }
		}

		public override void ResetValue(object component)
		{
			throw new NotImplementedException();
		}

		public override void SetValue(object component, object value)
		{
			TargetOption.Field.SetValue(component, value);
		}

		public override bool ShouldSerializeValue(object component)
		{
			return true;
		}
	}

	public class Option
	{
		public String Fullname;
		public String Description;
		public FieldInfo Field;
		public OptionTypes OptionType;
		public Type ValueType;

		public Object GetValue(CompilerOptions option)
		{
				if (OptionType == OptionTypes.Enum)
					return Field.GetValue(option).ToString();
				return Field.GetValue(option);
		}

		public Boolean SetValue(String value, CompilerOptions option)
		{
			Object targetValue;
			switch (OptionType)
			{
				case OptionTypes.Enum:
					if (Enum.GetNames(Field.FieldType).FirstOrDefault(s => s == value) == null)
						return false;
					targetValue = Enum.Parse(Field.FieldType, value, true);
					break;
				case OptionTypes.Int:
					{
						int r;
						if (!int.TryParse(value, out r))
							return false;
						targetValue = r;
					}
					break;
				case OptionTypes.String:
					targetValue = value;
					break;
				case OptionTypes.Boolean:
					Boolean b;
					if (!Boolean.TryParse(value, out b))
						return false;
					targetValue = b;
					break;
				default:
					throw new NotImplementedException();
			}
			Field.SetValue(option, targetValue);
			return true;
		}

		public enum OptionTypes
		{
			Boolean,
			Enum,
			String,
			Int
		}
	}

	public class BrowsableCompilerOptionsConverter : TypeConverter
	{
		static PropertyDescriptorCollection PDLs = null;
		public BrowsableCompilerOptionsConverter()
		{
			if (PDLs != null)
				return;
			List<PropertyDescriptor> list = new List<PropertyDescriptor>();
			foreach (var kvp in CompilerOptions.GetOptions())
			{
				List<Attribute> attrs = new List<Attribute>();
				foreach (Attribute attr in kvp.Value.Field.GetCustomAttributes(typeof(Attribute), true))
				{
					if (attr.GetType() == typeof(CompilerOptionAttribute))
					{
						var cod = attr as CompilerOptionAttribute;
						attrs.Add(new DescriptionAttribute(cod.Description));
						attrs.Add(new DisplayNameAttribute(cod.Fullname));
					}
					else
						attrs.Add(attr);
				}
				list.Add(new FieldToPropertyOptionDescriptor(kvp.Value, kvp.Key, attrs.ToArray()));
			}
			PDLs = new PropertyDescriptorCollection(list.ToArray());
		}

		public override bool GetPropertiesSupported(ITypeDescriptorContext context)
		{
			//return base.GetPropertiesSupported(context);
			return context.Instance != null;
		}

		public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
		{
			return PDLs;
		}
	}
	
	[TypeConverter(typeof(BrowsableCompilerOptionsConverter))]
	public class CompilerOptions
	{
		public static String rootName = "gscc_settings";
		private static Dictionary<Type, Option.OptionTypes> TypeMap = null;

		public CompilerOptions()
		{
			if (TypeMap != null)
				return;
			TypeMap = new Dictionary<Type, Option.OptionTypes>();
			TypeMap[typeof(Boolean)] = Option.OptionTypes.Boolean;
			TypeMap[typeof(String)] = Option.OptionTypes.String;
			TypeMap[typeof(Enum)] = Option.OptionTypes.Enum;
			TypeMap[typeof(int)] = Option.OptionTypes.Int;
		}

		public static Dictionary<String, Option> GetOptions()
		{
			Dictionary<String, Option> options = new Dictionary<string, Option>();
			Attribute[] attrs;
			CompilerOptionAttribute co;
			foreach (var f in typeof(CompilerOptions).GetFields())
			{
				attrs = f.GetCustomAttributes(typeof(CompilerOptionAttribute), false) as Attribute[];
				if (attrs.Length <= 0)
					continue;
				co = attrs[0] as CompilerOptionAttribute;
				var o = new Option()
				{
					Description = co.Description,
					Fullname = co.Fullname,
					Field = f
				};
				o.ValueType = f.FieldType;
				if (TypeMap.ContainsKey(f.FieldType))
					o.OptionType = TypeMap[f.FieldType];
				else
					if (TypeMap.ContainsKey(f.FieldType.BaseType))
						o.OptionType = TypeMap[f.FieldType.BaseType];
					else
						throw new Exception("Unknown option type: {0}".Fmt(f.FieldType));					
				options.Add(f.Name, o);
			}
			return options;
		}

		public void SaveOptions(Stream target)
		{
			XmlWriter xw = XmlTextWriter.Create(target, new XmlWriterSettings()
			{
				Encoding = new UTF8Encoding(false, false),
				Indent = true
			});
			XmlDocument xd = new XmlDocument();
			XmlElement root = xd.CreateElement(rootName);
			foreach (var kvp in GetOptions())
			{
				var o = kvp.Value;
				var v = xd.CreateElement(o.Field.Name);
				root.AppendChild(xd.CreateComment(o.Fullname));
				root.AppendChild(xd.CreateComment(o.Description));
				v.AppendChild(xd.CreateTextNode(o.GetValue(this).ToString()));
				root.AppendChild(v);
			}
			xd.AppendChild(root);
			xd.Save(xw);
		}

		public void LoadOptions(Stream source)
		{
			XmlReader xr = new XmlTextReader(source);
			XmlDocument xd = new XmlDocument();
			var d = GetOptions();
			try
			{
				xd.Load(xr);
			}
			catch (Exception e)
			{
				Console.WriteLine("Unable to open options: {0}".Fmt(e));
				return;
			}

			XmlElement root = xd.SelectSingleNode(rootName) as XmlElement;
			if (root == null)
				return;
			foreach (XmlNode e in root.ChildNodes)
			{
				if (e.NodeType != XmlNodeType.Element
					|| !d.ContainsKey(e.Name))
					continue;
				d[e.Name].SetValue(e.InnerText, this);
			}
		}

		public IEqualityComparer<String> GetComparer()
		{
			return StringMap.GetComparer(!this.Allow_CaseInsensitiveReferences);
		}

		[Category("Common")]
		[CompilerOption("Assignments as Expressions",
@"Allows assignments to be expressions. Enable statements such as these:
<code>a = b = 0; g = g+=c;</code>")]
		public Boolean Allow_AssignmentsAsExpressions = false;

		[CompilerOption("Strict Accessors",
@"This will enforce the accessor operator to only work
only on variables, accessors, indexes and function invocations.")]
		public Boolean Enable_StrictAccessor = false;

		[CompilerOption("Expressions as Statements",
@"This will allow expressions to be statements. Your code shouldn't contains any expression
statements, but this will allow them.")]
		public Boolean Allow_ExpressionsAsStatements = false;

		[Category("Common")]
		[CompilerOption("Positive unary operator",
@"Enables the positive unary prefix operator +. Or the opposite
of the negative operator.
<code>while(+foo)</code>")]
		public Boolean Allow_PositiveUnaryPostfixOperator = false;

		[Category("Common")]
		[CompilerOption("Inline arrays",
@"Allows inline array initialization. As opposed to the empty array specifier used
to assigned empty arrays to variables. NOTE that inline arrays are discouraged because
of conflicting pointer-function-call syntax and the double square bracket specifier.
<code>a = [1,2,3];</code>")]
		public Boolean Allow_InlineArrays = false;

		[CompilerOption("Single Quote Strings",
@"Allows single quote strings. This is non-formative.
<code>a = 'foobar';</code>")]
		public Boolean Allow_SingleQuoteStrings = false;

		[CompilerOption("Treat unknown chars as letters",
@"Treats unknown characters beyond normal alphabetic range as letters.")]
		public Boolean Enable_TreatUnknownCharsAsLetters = false;

		[CompilerOption("Debug Code",
@"Emits code in enclosed in /# #/")]
		public Boolean Enable_DebugCode = false;

		[CompilerOption("Empty Statements.",
@"Allows single semi-colons to act as empty statements.
<code>for(;;);</code>")]
		public Boolean Allow_EmptyStatements = true;

		[CompilerOption("Empty statements after conditionals",
@"In prominent C-like languages, statements like this
are allowed <code>if(blah());</code> but are discouraged. Enabling
this will enable that but will output warnings.
You generally want this off for clean code.")]
		public Boolean Allow_EmptyStatementsAfterConditionals = false;

		[CompilerOption("Volatile Call Modifier",
@"Enables the call modifier <code>volatile</code>. Volatile ensures a thread
will not end while it is calling a function with the volatile-modifier.
This is non-formative and is only available to the CFGSCC.")]
		public Boolean Enable_CallModifier_Volatile = false;


		[CompilerOption("Store resolved constants",
@"Whether to keep the expression structure if a declared constant has one.
If not, the structure will still be evaluated but will not store
the result for regurgatated auto-regenerated code. 
<code>FOOBAR = 6; BOOBAZ = FOOBAR * 4 - 3;</code>")]
		public Boolean Enable_StoreResolvedConstants = false;

		[Category("Common")]
		[CompilerOption("Break on Error (DEBUG)",
@"This will break to a breakpoint on the first error if debugging
is enabled with supplied source files for the compiler.")]
		public Boolean Enable_BreakOnError = true;

		[Category("Common")]
		[CompilerOption("Break on Warn (DEBUG)",
@"This will break to a breakpoint on the first warning if debugging
is enabled with supplied source files for the compiler.")]
		public Boolean Enable_BreakOnWarn = false;

		[CompilerOption("Case Insensitive References",
@"By specification, this should be always on.")]
		public Boolean Allow_CaseInsensitiveReferences = true;

		[CompilerOption("Allow Default Case To Be Grouped",
@"Whether the default case can be grouped with other case expressions.")]
		public Boolean Allow_GroupedDefault = true;

		[CompilerOption("Allow Empty Switch Statements",
@"Allows switch statements to have no statements and cases.")]
		public Boolean Allow_EmptyMultiConditionals = false;

		[CompilerOption("Allow Global Function Override",
@"Allows globally defined functions to override locally defined versions. So non-scope
specific calls will always invoke the global version.")]
		public Boolean Allow_GlobalFunctionOverride = true;

		[CompilerOption("Search for constants in Includes",
@"Will search includes when referencing constants. This is non-formative.")]
		public Boolean Enable_ConstantDeepSearch = false;

		[CompilerOption("Parsing mode",
@"Whether to enable/disable certain code elements. Singleplayer scripts
inherit all elements from multiplayer scripts and have various
animation constructs such as the #animtree variable and other
various directives. Note that directives must be known and defined
to the compiler in order to check for directive semantic errors.
<code>FOOBAR = 6; BOOBAZ = FOOBAR * 4 - 3;</code>")]
		public ParseModes ParsingMode = ParseModes.Singleplayer;

		[CompilerOption("Support Hex Specifier",
@"Whether to allow hexadecimal numbers.")]
		public Boolean Allow_HexSpecifier = false;

		[CompilerOption("Script-include recursion threshold",
@"The maximum depth of script includes.")]
		public int MaximumScriptDepth = 50;

		[CompilerOption("Support infinity",
@"Allows the infinity constant for floatings types.")]
		public Boolean Allow_Infinity = false;

		[CompilerOption("Warn pre-declared iterators",
@"Warns if an interator or key references an existing variable.")]
		public Boolean Warn_PredeclaredIterators = true;
	}
}