using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using GameScriptCompiler.Tokenization;
using GameScriptCompiler.CodeDOM;

namespace GameScriptCompiler
{
	public class SyntaxBuilder : IDisposable
	{
		public class Message
		{
			public String Msg = null;
			public Text.DocLocation? Location = null;
			public static Message Create(String msg, Text.DocLocation? location = null)
			{
				var m = new Message();
				m.Msg = msg;
				m.Location = location;
				return m;
			}
			public override string ToString()
			{
				return Msg;
			}
		}
		String FileName;
		ScopeMode Scope;
		CompilerFrontEnd Cfe;
		StreamReader Fs = null;
		Dictionary<Stages, Tuple<Func<Boolean>, Func<float>>> Steps;
		public Tokenizer Tok { get; private set; }
		public CodeDomParser Syn { get; private set; }
		public List<Token> Tokens = null;

		/// <summary>
		/// Only use this method to retreive the final DOM if your
		/// target scope is <c>ScopeMode.Module</c>,
		/// which is the most common scope.
		/// </summary>
		/// <param name="name">Name to give DOM graph.</param>
		/// <returns></returns>
		public CodeDOMModule GetFinalDOM(String name)
		{
			return Syn == null ? null : Syn.CreateFinalDOM(name);
		}

		/// <summary>
		/// Retrives the <c>Expressable</c> object type from
		/// final objects that are a result of a successfull
		/// compile. Useful for converting the result to
		/// a string.
		/// </summary>
		/// <returns></returns>
		public Expressable GetExpressable()
		{
			if (Syn == null)
				return null;
			switch (Scope)
			{
				case ScopeMode.Module:
					return Syn.CreateFinalDOM("No Name");
				case ScopeMode.Expression:
					var e = Syn.CreateFinalExpression() as Expressable;
					return e ?? new NullExpression();
				case ScopeMode.Function:
					return Syn.CreateFinalFunction();
				default:
					return null;
			}
		}

		public Boolean Success { get; private set;}

		public List<Message> Warns = new List<Message>(), Errors = new List<Message>();

		public IEnumerable<Message> GetAllMessages()
		{
			return Warns.Concat(Errors);
		}

		public Stages Stage { get; private set; }

		public enum Stages
		{
			Init,
			Tokening,
			Syntaxing
		}

		public Boolean OscarKilo
		{
			get
			{
				return Warns.Count == 0 && Errors.Count == 0;
			}
		}

		public float Progress
		{
			get
			{
				return GetProgress();
			}
		}

		private float GetProgress()
		{
			return Steps[Stage].Item2();
		}

		private void Init(CompilerFrontEnd cfe, ScopeMode scope)
		{
			Stage = Stages.Init;
			Cfe = cfe;
			Scope = scope;
			Steps = new Dictionary<Stages, Tuple<Func<Boolean>, Func<float>>>();
			Steps.Add(Stages.Init, new Tuple<Func<bool>,Func<float>>(OnInit, InitProg));
			Steps.Add(Stages.Tokening, new Tuple<Func<bool>,Func<float>>(OnTokenStep, TokenProg));
			Steps.Add(Stages.Syntaxing, new Tuple<Func<bool>,Func<float>>(OnSyntaxStep, SyntaxProg));
			Fs = null;
		}

		public SyntaxBuilder(CompilerFrontEnd cfe, String path, ScopeMode scope = ScopeMode.Module)
		{
			Init(cfe, scope);
			FileName = path;
		}

		public SyntaxBuilder(CompilerFrontEnd cfe, StreamReader stream, ScopeMode scope = ScopeMode.Module)
		{
			Init(cfe, scope);
			Fs = stream;
		}

		public Boolean Step()
		{
			return Steps[Stage].Item1();
		}

		private float InitProg()
		{
			return 0;
		}

		private float TokenProg()
		{
			return Tok.Progress;
		}

		private float SyntaxProg()
		{
			return Syn.Progress;
		}

		private Boolean OnInit()
		{
			if (Fs == null)
			try
			{
				Fs = new StreamReader(FileName);
			}
			catch (Exception e)
			{
				Errors.Add(Message.Create("Failed to open \"{0}\" ({1})".Fmt(FileName, e.Message)));
				return false;
			}
			Tokens = new List<Token>();
			Stage = Stages.Tokening;
			Tok = new Tokenizer(Fs, Cfe.Options);
			return true;
		}

		private Boolean OnTokenStep()
		{
			if (!Tok.Step())
			{
				Fs.Close();
				if (Tok.Error)
				{
					Errors.Add(Message.Create(Tok.ErrorMessage, Tok.ErrorLocation));
					return false;
				}
				Stage = Stages.Syntaxing;
				Syn = new CodeDomParser(Tokens.ToArray(), Cfe.Options, Scope);
				return true;
			}
			else if (Tok.Result.Type == FeedResultType.NewToken)
				Tokens.Add(Tok.Result.Token);
			return true;
		}

		private Boolean OnSyntaxStep()
		{
			if (Syn.Step())
				return true;

			foreach (var m in Syn.Errors)
			{
				Errors.Add(Message.Create("{0} {1}".Fmt(m.Message, m.Location), m.Location));
			}
			foreach (var m in Syn.Warnings)
				Warns.Add(Message.Create("{0} {1}".Fmt(m.Message, m.Location), m.Location));

			if (!Syn.IsFatalError)
				Success = true;
			return false;
		}

		public void SavePacked(StreamWriter sw)
		{
			if (Stage != Stages.Syntaxing)
				throw new Exception("Attempted to save packed before requires stage.");
			Packer.PackToMinimumWhitespace(Tokens.ToArray(), sw);
		}

		public void Dispose()
		{
			if (Fs != null)
				Fs.Dispose();
		}
	}

	/// <summary>
	/// A front end for semantical symbol resolving, add entire
	/// directories of scripts to perform semantical validation
	/// of all symbols. You may also supply a separate global
	/// script file for symbols that aren't meant to be
	/// defined.
	/// </summary>
	public class ValidationBuilder
	{
		DirectoryInfo Base, Working;
		//TODO: Do some base path existence checking
		//DictoryInfo BasePaths;
		public ContextAnalyzation.ContextAnalyzer Ca;
		String ScriptExt;
		IEnumerator<FileInfo> Current;

		Dictionary<ContextAnalyzation.ContextAnalyzer.ErrorType, int> ErrorStats
			= new Dictionary<ContextAnalyzation.ContextAnalyzer.ErrorType, int>();
		Dictionary<ContextAnalyzation.ContextAnalyzer.WarnType, int> WarnStats
			= new Dictionary<ContextAnalyzation.ContextAnalyzer.WarnType, int>();

		public class ModuleInfo
		{
			public String Name, NameExtless;
			public String[] Paths;
			public FileInfo Info;
		}

		public String BuildMessageStats()
		{
			ErrorStats.Clear();
			WarnStats.Clear();
			foreach (var m in Ca.Errors)
				if (ErrorStats.ContainsKey(m.ErrorType))
					ErrorStats[m.ErrorType]++;
				else
					ErrorStats[m.ErrorType] = 1;
			foreach (var m in Ca.Warnings)
				if (WarnStats.ContainsKey(m.WarnType))
					WarnStats[m.WarnType]++;
				else
					WarnStats[m.WarnType] = 1;
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("Errors:");
			foreach (var kvp in ErrorStats)
				sb.AppendLine("{0}: {1}".Fmt(kvp.Key, kvp.Value));
			sb.AppendLine();
			sb.AppendLine("Warnings:");
			foreach (var kvp in WarnStats)
				sb.AppendLine("{0}: {1}".Fmt(kvp.Key, kvp.Value));
			sb.AppendLine();
			return sb.ToString();
		}

		public ValidationBuilder(CompilerOptions options, CodeDOMModule global,
			String scriptExt,
			String basePath, String workingBasePath = null)
		{
			Ca = new ContextAnalyzation.ContextAnalyzer(options, global);
			if (workingBasePath == null)
				workingBasePath = basePath;
			try
			{
				Base = new DirectoryInfo(basePath);
				if (!Base.Exists)
					throw new FileNotFoundException("Base path directory does not exist: {0}".Fmt(basePath));
			}
			catch (Exception e)
			{
				throw new Exception("Cannot get directory information on \"{0}\"".Fmt(Base), e);
			}
			try
			{
				Working = new DirectoryInfo(workingBasePath);
				if (!Working.Exists)
					throw new FileNotFoundException("Working base path directory does not exist: {0}".Fmt(workingBasePath));
			}
			catch (Exception e)
			{
				throw new Exception("Cannot get directory information on \"{0}\"".Fmt(workingBasePath), e);
			}
			ScriptExt = scriptExt;
			Current = Working.EnumerateFiles("*.{0}".Fmt(ScriptExt), SearchOption.AllDirectories).GetEnumerator();
		}

		public ContextAnalyzation.MessageObject[] GetMessageObjects()
		{
			List<ContextAnalyzation.MessageObject> msgs = new List<ContextAnalyzation.MessageObject>();
			foreach (var m in Ca.Warnings)
				msgs.Add(m.CreateObject());
			foreach (var m in Ca.Errors)
				msgs.Add(m.CreateObject());
			return msgs.ToArray();
		}

		//TODO: Leading slashes produce empty strings when split, and these are not handled correctly.
		public static String[] GetPathDifference(String basePath, String filePath)
		{
			List<String> paths = new List<string>();
			IEnumerator<String> iBase = basePath.Split('/', '\\').AsEnumerable().GetEnumerator(),
				iPath = filePath.Split('/', '\\').AsEnumerable().GetEnumerator();
			while (iPath.MoveNext())
			{
				if (!iBase.MoveNext() || iBase.Current == "")
				{
					paths.Add(iPath.Current);
					break;
				}

				if (!iBase.Current.Equals(iPath.Current, StringComparison.InvariantCultureIgnoreCase))
					return null;
			}
			while (iPath.MoveNext())
				paths.Add(iPath.Current);
			paths.RemoveAt(paths.Count - 1);
			return paths.ToArray();
		}

		public static String GetExtlessName(String name)
		{
			if (!name.Contains('.'))
				return name;
			return name.Substring(0, name.LastIndexOf('.'));
		}

		public Boolean NextFile(ref ModuleInfo moduleInfo)
		{
			if (!Current.MoveNext())
				return false;
			moduleInfo = new ModuleInfo()
			{
				Info = Current.Current,
				Name = Current.Current.Name,
				NameExtless = GetExtlessName(Current.Current.Name),
				Paths = GetPathDifference(Working.FullName, Current.Current.FullName)
			};
			return true;
		}


		public Boolean Exists(ModuleReference @ref, String @base)
		{
			return File.Exists(Path.Combine(@base, Path.Combine(@ref.Paths), "{0}.{1}".Fmt(@ref.ModuleName, ScriptExt)));
		}
	}

	/// <summary>
	/// This is the front end object used with other front ends
	/// like the ValidationBuilder and SyntaxBuilder. You can
	/// of course use just the front end.
	/// </summary>
	public class CompilerFrontEnd
	{
		public CompilerOptions Options;
		public CodeDOMModule UserDefinedDOM = null;

		public CompilerFrontEnd()
		{
			Options = new CompilerOptions();
			CodeDOM.StringMap.Init();
		}

		public static List<Token> CreateEmptyTokenList()
		{
			return new List<Token>();
		}

		public static CompilerOptions CreateOptions(ParseModes mode = ParseModes.Singleplayer)
		{
			return new CompilerOptions() { ParsingMode = mode };
		}

		public Tokenizer CreateTokenizer(StreamReader inputStream)
		{
			return new Tokenizer(inputStream, Options);
		}

		public CodeDomParser CreateParser(Token[] tokens)
		{
			return new CodeDomParser(tokens, Options);
		}
	}
}
