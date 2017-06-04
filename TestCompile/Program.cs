using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading;
using GameScriptCompiler;
using GameScriptCompiler.CodeDOM;

namespace TestCompile
{
	class Program
	{
		public static Stack<Tuple<ConsoleColor, ConsoleColor>> ColorStack = new Stack<Tuple<ConsoleColor, ConsoleColor>>();
		public static Dictionary<ConCol, Tuple<ConsoleColor, ConsoleColor>> ColorMap = new Dictionary<ConCol,Tuple<ConsoleColor,ConsoleColor>>();

		static void PushCc(ConsoleColor foreground, ConsoleColor background)
		{
			ColorStack.Push(new Tuple<ConsoleColor, ConsoleColor>(Console.ForegroundColor, Console.BackgroundColor));
			Console.ForegroundColor = foreground;
			Console.BackgroundColor = background;
		}

		static void PopCc(Boolean writeLine = false)
		{
			var t = ColorStack.Pop();
			Console.ForegroundColor = t.Item1;
			Console.BackgroundColor = t.Item2;

			if (writeLine)
				Console.WriteLine();
		}

		static void ColorInit()
		{
			ColorMap.Add(ConCol.Error, new Tuple<ConsoleColor,ConsoleColor>(ConsoleColor.Red, ConsoleColor.DarkRed));
			ColorMap.Add(ConCol.Info, new Tuple<ConsoleColor,ConsoleColor>(ConsoleColor.Cyan, ConsoleColor.DarkCyan));
			ColorMap.Add(ConCol.Warning, new Tuple<ConsoleColor,ConsoleColor>(ConsoleColor.Yellow, ConsoleColor.DarkYellow));
			ColorMap.Add(ConCol.Success, new Tuple<ConsoleColor, ConsoleColor>(ConsoleColor.Green, ConsoleColor.DarkGreen));
		}

		static void PushAlert()
		{
			PushCc(ConsoleColor.Yellow, ConsoleColor.DarkYellow);
		}

		static void PushInfo()
		{
			PushCc(ConsoleColor.Cyan, ConsoleColor.DarkCyan);
		}

		static void PushInfo2()
		{
			PushCc(ConsoleColor.Green, ConsoleColor.DarkGreen);
		}

		static ConsoleKey CharQuestion(String question, params ConsoleKey[] chars)
		{
			ConsoleKey? choice = null;
			Boolean again = false;
			while (choice == null)
			{
				PushInfo();
				Console.Write("{0}{1}".Fmt(again ? "I SAID, " : "", question));
				PopCc(true);
				ConsoleKey read = Console.ReadKey(true).Key;
				foreach (var c in chars)
					if (c == read)
					{
						choice = c;
						break;
					}
				again = true;
			}
			return choice.Value;
		}

		public enum ConCol
		{
			Info,
			Warning,
			Error,
			Success
		}

		static void PushCc(ConCol col)
		{
			var c = ColorMap[col];
			PushCc(c.Item1, c.Item2);
		}

		static void WriteLine()
		{
			Console.WriteLine();
		}

		static void WriteLine(String str, params Object[] args)
		{
			Console.WriteLine(str, args);
		}

		static void WriteLine(ConCol col, String str, params Object[] args)
		{
			PushCc(col);
			Console.Write(str, args);
			PopCc();
			Console.WriteLine();
		}

		static void Write(String str, params Object[] args)
		{
			Console.Write(str, args);
		}

		static void Write(ConCol col, String str, params Object[] args)
		{
			PushCc(col);
			Console.Write(str, args);
			PopCc();
		}

		static void CheckOutputFolder()
		{
			if (!Directory.Exists(paramOutputFolder))
			{
				WriteLine(ConCol.Info, "Directory [{0}] does not exist. Creating it.".Fmt(paramOutputFolder));
				Directory.CreateDirectory(paramOutputFolder);
			}
		}

		static CodeDOMModule global = null;
		static List<FileInfo> pending = new List<FileInfo>();
		static String defaultSettings = "Settings.xml";

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		public static extern IntPtr GetConsoleWindow();

		[Args.ArgDesc(ArgName="settings file", Desc="Specify settings file to use. Default is \"Settings.xml\"")]
		public static String paramSettings = null;

		[Args.ArgDesc(ArgName="global script", Desc="Sets global declaration file.")]
		public static String paramSetGlobal = null;

		[Args.ArgDesc(ArgName="extension", Desc="Extension to look for when looking for script files. Default is \"gsc\".")]
		public static String paramSetScriptExt = "gsc";

		[Args.ArgDesc(ArgName="path", Desc="Path for base scripts for context anaylzation. This will use the imports "+
			"in the script code to determine what files to compile with.")]
		public static String paramBasePath = null;

		[Args.ArgDesc(ArgName = "path", Desc = "Path for main working scripts that modify the base scripts.")]
		public static String paramMainPath = null;

		[Args.ArgDesc(Desc = "Compiles generated copy against original for compiler validity. " +
			"Note that specifying EmitPacked will also crosscheck with the packed output.")]
		public static Boolean paramCrossCheck = false;

		[Args.ArgDesc(ArgName = "filename", Desc = "Outputs a generated copy of the input file into the output directory. If using "+
			"/CrossCheck and /CopyName is not specified, the orignal name of the current source will be used.")]
		public static String paramCopyName = null;

		[Args.ArgDesc(ArgName = "folder", Desc = "Name of folder to place generated and/or packed copies.")]
		public static String paramOutputFolder = "output";

		[Args.ArgDesc(Desc="Whether to emit a packed copy of the current source. Filename will be CopyName with \".packed\" in between extension "+
			"and filename.")]
		public static Boolean paramEmitPacked = false;

		[Args.ArgDesc(ArgName = "number", Desc="Skips processing of n files.")]
		public static String paramSkipUntil = null;
		[Args.ArgDesc(ArgName = "logpath", Desc = "Output validation log into file and do not output into console.")]
		public static String paramValidationLog = null;

		[Args.ArgDesc(ArgName = "stubpath", Desc = "Emits stubs of unknown functions (not references) after validation. Note that no other errors but " +
			"unknown functions calls are allowed after a validation. If any other error appears, no stub will be emitted.")]
		public static String paramStubOut = null;

		[Args.ArgDesc(ArgName = "file to run", Desc = "Executes a shell command upon the first error.")]
		public static String paramErrorExecute = null;

		[Args.ArgDesc(ArgName = "parameters", Desc = "Parameters to use when using /ErrorExecute. Params Placeholders: \r\n{0} Full script path of first error\r\n{1} Line of error,\r\n{2} Column of error")]
		public static String paramExecuteParams = null;

		[Args.ArgDesc(Desc="Packs the logical error and warning messages into objects into a compressed file in /ValidationLog instead of a text log")]
		public static  Boolean paramPackLog = false;

		[Args.ArgDesc(Desc = "Cleared output folder")]
		public static Boolean paramClearOutput = false;

		public static String fileMask
		{
			get
			{
				return "*.{0}".Fmt(paramSetScriptExt);
			}
		}

		static void ExecuteOnError(String fullpath, GameScriptCompiler.Text.DocLocation location)
		{
			if (paramErrorExecute == null)
				return;
			try
			{
				ProcessStartInfo psi = new ProcessStartInfo(paramErrorExecute,
					paramExecuteParams != null ? paramExecuteParams.Fmt("\"{0}\"".Fmt(fullpath), location.Line, location.Column) : "");
				psi.UseShellExecute = true;
				Process.Start(psi);
			}
			catch (Exception e)
			{
				WriteLine(ConCol.Error, "Unable to execute [{0}]: {1}", fullpath, e.Message);
			}
		}

		static int Main(string[] args)
		{
			int r = _Main(args);
			if (Debugger.IsAttached)
				Console.ReadKey(true);
			return r;
		}

		static int _Main(string[] args)
		{
			ColorInit();

			var fields = Args.GetFieldsFromPrefix<Program>("param");
			List<String> targets = new List<string>();
			var r = Args.SetArgs(null, args, ref targets, fields);
			if (args.Length == 0 || r == Args.ArgResult.IncompleteOrInvalid)
			{
				Console.WriteLine("Need arguments or invalid arguments.");
				Console.WriteLine();
				Console.WriteLine(Args.GetUsage(fields, null));
				return 0;
			}

			if (paramBasePath != null && targets.Count > 0)
			{
				WriteLine(ConCol.Warning, "Cannot build syntax targets and perform validation together. Either provide just syntax targets "
					+ "or provide a base path for validation with /BasePath.");
				return 1;
			}

			CompilerFrontEnd cfe = new CompilerFrontEnd();

			//Get the settings file from the current module directory, not working directory. Or else D&D would use incorrect paths.
			if (paramSettings == null)
				paramSettings = Path.Combine((new FileInfo(Assembly.GetExecutingAssembly().Location)).DirectoryName, defaultSettings);
			

			if (!File.Exists(paramSettings))
			{
				using (FileStream fs = new FileStream(paramSettings, FileMode.Create))
					cfe.Options.SaveOptions(fs);
				WriteLine(ConCol.Info, "Writing settings to [{0}]".Fmt(paramSettings));
			}
			else
			{
				using (FileStream fs = new FileStream(paramSettings, FileMode.Open))
					cfe.Options.LoadOptions(fs);
				WriteLine(ConCol.Info, "Loaded settings from [{0}]".Fmt(paramSettings));
				using (FileStream fs = new FileStream(paramSettings, FileMode.Create))
					cfe.Options.SaveOptions(fs);
			}

			if (paramClearOutput)
			{
				WriteLine(ConCol.Info, "Clearing output folder");
				var di = new DirectoryInfo(paramOutputFolder);
				if (di.Exists)
					di.Delete(true);
				di.Create();
			}
			
			if (paramSetGlobal != null)
			{
				try
				{
					if (!ProcessFile(cfe, new FileInfo(paramSetGlobal), 1, 0))
						return 1;
				}
				catch (Exception e)
				{
					WriteLine(ConCol.Error, "Error reading global file [{0}]: {1}", paramSetGlobal, e.Message);
					e.BreakException();
				}
				global = cfe.UserDefinedDOM;
				if (!global.IsDeclarative)
				{
					WriteLine(ConCol.Error, "Global module must be set declarative with \"#set_global_declarative\".");
					Console.ReadKey(true);
					return 1;
				}
			}

			foreach (var file in targets)
			{
				try
				{
					if (File.Exists(file))
						pending.Add(new FileInfo(file));
					else
						if (Directory.Exists(file))
						{
							foreach (var _file in (new DirectoryInfo(file)).EnumerateFiles(fileMask, SearchOption.AllDirectories))
								pending.Add(_file);
						}
						else
							WriteLine(ConCol.Warning, "Could not find file/folder [{0}]".Fmt(file));
				}
				catch (Exception e)
				{
					WriteLine(ConCol.Error, "Could not add file [{0}] to pending: {1}".Fmt(file, e.Message));
					e.BreakException();
				}
			}

			if (paramBasePath == null)
				BuildSyntax(cfe);
			else
				Validate(cfe);

			return 0;
		}

		static void Validate(CompilerFrontEnd cfe)
		{
			ValidationBuilder v = new ValidationBuilder(cfe.Options, global, paramSetScriptExt, paramBasePath, paramMainPath);
			List<ValidationBuilder.ModuleInfo> files = new List<ValidationBuilder.ModuleInfo>();
			ValidationBuilder.ModuleInfo moduleInfo = new ValidationBuilder.ModuleInfo();

			while (v.NextFile(ref moduleInfo))
			{
				if (moduleInfo.Paths == null)
				{
					WriteLine(ConCol.Error, "Path error for {0}, path difference bug!", moduleInfo.Name);
					continue;
				}
				files.Add(moduleInfo);
			}
			int Successes = 0;
			foreach (var mi in files.AsIndexable())
			{
				if (ProcessFile(cfe, mi.Value.Info, mi.Total, mi.Index))
				{
					++Successes;
					cfe.UserDefinedDOM.FullPath = mi.Value.Info.FullName;
					v.Ca.AddNewModule(cfe.UserDefinedDOM, mi.Value.Paths);
				}
			}
			if (v.Ca.FinalAnalyze()) //TODO: Does root CM even become resolved?
				WriteLine(ConCol.Success, "All symbols are resolved.");
			if (paramValidationLog == null)
			{
				WriteLine(ConCol.Warning, v.BuildMessageStats());
				WriteLine(ConCol.Warning, "Warnings ({0} Total):".Fmt(v.Ca.Warnings.Count));
				foreach (var m in v.Ca.Warnings.AsIndexable())
					if (m.Index < 100)
						WriteLine(ConCol.Warning, "{0}) {1}", m.Index + 1, m.Value.ToString());
					else
					{
						WriteLine(ConCol.Info, "Number of warnings exceed 100. To view all of them, compile with a validation log.");
						break;
					}
				WriteLine(ConCol.Error, "Errors ({0} Total):".Fmt(v.Ca.Errors.Count));
				foreach (var m in v.Ca.Errors.AsIndexable())
					if (m.Index < 100)
						WriteLine(ConCol.Error, "{0}) {1}", m.Index + 1, m.Value.ToString());
					else
					{
						WriteLine(ConCol.Info, "Number of errors exceed 100. To view all of them, compile with a validation log.");
						break;
					}
				if (v.Ca.Errors.Count > 0)
					ExecuteOnError(v.Ca.Errors[0].Module.DOM.FullPath, v.Ca.Errors[0].Target.Location);
			}
			else
				if (!paramPackLog)
				{
					WriteLine(ConCol.Info, "No Warning/Error Messages will be shown here, all messages will be written to {0}", paramValidationLog);
					using (var sw = new StreamWriter(paramValidationLog))
					{
						sw.WriteLine(v.BuildMessageStats());
						sw.WriteLine("Warnings ({0}):".Fmt(v.Ca.Warnings.Count));
						sw.WriteLine();
						sw.WriteLine();
						foreach (var m in v.Ca.Warnings)
							sw.WriteLine("{0:D3}) {1}", m.MsgType, m.ToString());
						sw.WriteLine("Errors ({0}):".Fmt(v.Ca.Errors.Count));
						sw.WriteLine();
						sw.WriteLine();
						foreach (var m in v.Ca.Errors)
							sw.WriteLine("{0:D3}) {1}", m.MsgType, m.ToString());
					}
				}
				else
					using (var sw = new StreamWriter(paramValidationLog))
					{
						XmlSerializer xs = new XmlSerializer(typeof(GameScriptCompiler.ContextAnalyzation.MessageObject[]));
						xs.Serialize(sw, v.GetMessageObjects());
					}

			if (paramStubOut != null)
				if (StubGen.CheckUnSpecifics(v.Ca.Errors))
				{
					using (var sw = new StreamWriter(paramStubOut))
					{
						StubGen.StubOut(sw, v.Ca.Errors, cfe.Options);
						WriteLine(ConCol.Info, "Wrote function stubs [{0}]", paramStubOut);
					}
				}
				else
					WriteLine(ConCol.Warning, "Cannot write stubs with these particular errors.");
			if (Successes == files.Count)
				WriteLine(ConCol.Success, "Succussfully syntaxed all files!");
			else
				WriteLine(ConCol.Warning, "Completed with warnings/errrors.");

		}

		static void BuildSyntax(CompilerFrontEnd cfe)
		{
			int Successes = 0, skip = -1;
			Boolean DoSkip = false;
			if (paramSkipUntil != null && int.TryParse(paramSkipUntil, out skip))
				DoSkip = true;

			foreach (var cur in pending.AsIndexable())
			{
				if (DoSkip && cur.Index < skip)
					continue;
				try
				{
					if (ProcessFile(cfe, cur.Value, cur.Total, cur.Index, false))
					{
						++Successes;
					}
				}
				catch (Exception e)
				{
					WriteLine(ConCol.Error, "Exception processing file [{0}]: {1}", cur.Value.Name, e.Message);
					e.BreakException();
				}
			}

			WriteLine(Successes == pending.Count ? ConCol.Success : ConCol.Warning,
				"Compiled {0}/{1}", Successes, pending.Count);
		}

		static Boolean ProcessFile(CompilerFrontEnd cfe, FileInfo file, int total, int current, Boolean basic = true)
		{
			SyntaxBuilder sb = null;
			Boolean OscarKilo = false;
			ConsoleKey ck;

			while (!OscarKilo)
			{
				sb = new SyntaxBuilder(cfe, file.FullName);
				WriteLine(file.Name);
				SyntaxBuilder.Stages last = (SyntaxBuilder.Stages)(-1);
				while (sb.Step())
				{
					if (last != sb.Stage)
						Write(ConCol.Info, "\r({0}/{1}): {2}", current + 1, total, (last = sb.Stage).ToString());
				}
				WriteLine();


				if (!sb.OscarKilo)
				{
					if (!sb.Success)
						WriteLine(ConCol.Error, "Unable to process [{0}]:", file.Name);
					if (sb.Warns.Count > 0)
					{
						WriteLine(ConCol.Warning, "Warnings:");
						foreach (var m in sb.Warns.AsIndexable())
						{
							WriteLine(ConCol.Warning, "{0}) {1}", m.Index + 1, m.Value);
						}
						WriteLine();
					}
					if (sb.Errors.Count > 0)
					{
						WriteLine(ConCol.Error, "Errors:");
						foreach (var m in sb.Errors.AsIndexable())
							WriteLine(ConCol.Error, "{0}) {1}", m.Index + 1, m.Value);
						if (sb.Errors.Count > 0)
							ExecuteOnError(file.FullName, sb.Errors[0].Location.Value);
					}
					WriteLine("Press anything...");
					Console.ReadKey(true);

					ck = CharQuestion("Do you want to (c)ontinue or (t)ry again?", ConsoleKey.C, ConsoleKey.T, ConsoleKey.F);
					if (ck == ConsoleKey.C)
						break;
				}
				else
					OscarKilo = true;
			}

			if (sb.Success)
				cfe.UserDefinedDOM = sb.GetFinalDOM(ValidationBuilder.GetExtlessName(file.Name));

			if (sb.Success && !basic)
			{
				String targetName = paramCopyName == null ? file.Name : paramCopyName;
				String finalTarget = Path.Combine(paramOutputFolder, targetName);
				String targetPacked = "{0}.packed{1}".Fmt(ValidationBuilder.GetExtlessName(targetName),
						(new FileInfo(targetName)).Extension), finalTargetPacked = null;


				if (paramCopyName != null || paramCrossCheck)
				{
					CheckOutputFolder();
					WriteLine(ConCol.Info, "Outputing generated source.");
					using (var sw = new StreamWriter(finalTarget))
						sw.Write(cfe.UserDefinedDOM.Express(0));
					WriteLine(ConCol.Info, "Emitted generated file [{0}].".Fmt(targetName));
				}

				if (paramEmitPacked)
				{

					finalTargetPacked = Path.Combine(paramOutputFolder, targetPacked);
					try
					{
						using (StreamWriter sw = new StreamWriter(finalTargetPacked))
							sb.SavePacked(sw);
						WriteLine(ConCol.Success, "Emitted packed source [{0}].", finalTargetPacked);
					}
					catch (Exception e)
					{
						WriteLine(ConCol.Error, "Unable to emit packed source of [{0}]: {1}", targetPacked, e.Message);
						e.BreakException();
						return false;
					}
				}

				if (paramCrossCheck)
					try
					{
						CodeDOMModule A, B;
						CheckOutputFolder();
						A = cfe.UserDefinedDOM;
						
						if (!ProcessFile(cfe, new FileInfo(finalTarget), total, current))
						{
							WriteLine(ConCol.Error, "Cross check failed! Please contact Chaz pronto!");
							throw new Exception("Cross check failed. Compiler isn't up to standard.");
						}

						B = cfe.UserDefinedDOM;

						var d = CodeDOMComparer.CompareDOM(A, B);

						if (d.Count == 0)
							WriteLine(ConCol.Success, "Crosscheck Original To Copy Success!");
						else
						{
							WriteLine(ConCol.Warning, "Crosscheck returned differences:");
							foreach (var item in d.AsIndexable())
								WriteLine(ConCol.Warning, "{0}> {1}", item.Index+1, item.Value.ToString());
							throw new Exception("Cross check failed. Compiler isn't up to standard.");
						}

						if (paramEmitPacked)
						{
							WriteLine(ConCol.Info, "Now cross checking with packed [{0}].", finalTargetPacked);
							if (!ProcessFile(cfe, new FileInfo(finalTargetPacked), total, current))
							{
								WriteLine(ConCol.Error, "Packed cross check failed! Please contact Chaz pronto!");
								throw new Exception("Packed cross check failed. Compiler isn't up to standard.");
							}

							B = cfe.UserDefinedDOM;

							d = CodeDOMComparer.CompareDOM(A, B);

							if (d.Count == 0)
								WriteLine(ConCol.Success, "Crosscheck Original To Packed Success!");
							else
							{
								WriteLine(ConCol.Warning, "Packed crosscheck returned differences:");
								foreach (var item in d.AsIndexable())
								WriteLine(ConCol.Warning, "{0}> {1}", item.Index+1, item.Value.ToString());
								throw new Exception("Packed Cross check failed. Compiler isn't up to standard.");
							}
						}

						cfe.UserDefinedDOM = A;
					}
					catch (Exception e)
					{
						WriteLine(ConCol.Error, "Crosscheck of file [{0}] failed: {1}".Fmt(targetName, e.Message));
						e.BreakException();
						return false;
					}
			}
			return sb.Success;
		}
	}

	

	static class Extensions
	{
		public static void BreakException(this Exception e)
		{
			if (Debugger.IsAttached)
				throw e;
			Console.WriteLine("Press anything...");
			Console.ReadKey(true);
		}
	}
}
