using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameScriptCompiler.CodeDOM;
using GameScriptCompiler.Text;

namespace GameScriptCompiler
{
	namespace ContextAnalyzation
	{
		#region Context Nodes
		public abstract class ContextNode
		{
			public Boolean IsResolved = false;
			public String Name;
			public NodeTypes NodeType;
			public enum NodeTypes
			{
				Dir,
				Module
			}
			public ContextNode(String name, NodeTypes type)
			{
				NodeType = type;
				Name = name;
			}
			public ContextDir AsDir() { return this as ContextDir; }
			public ContextModule AsModule() { return this as ContextModule; }
		}

		//Represents a local variable name. Is usually stored in a frame.
		public class LocalEntry
		{
			/// <summary>
			/// Name of local variable
			/// </summary>
			public String Name;
			/// <summary>
			/// List of references of this variable
			/// </summary>
			public List<VariableReference> References;
			public LocalEntry(VariableReference vr)
			{
				Name = vr.Name;
				References = new List<VariableReference>();
				References.Add(vr);
			}
		}

		public class LocalFrameEnumerator : IEnumerator<LocalEntry>
		{
			IEnumerator<KeyValuePair<String, LocalEntry>> _CurIt;

			public LocalFrameEnumerator(IEnumerable<KeyValuePair<String, LocalEntry>> enmrbl)
			{
				_CurIt = enmrbl.AsEnumerable().GetEnumerator();
			}

			public LocalEntry Current
			{
				get { return _CurIt.Current.Value; ; }
			}

			public void Dispose()
			{
			}

			object System.Collections.IEnumerator.Current
			{
				get { return _CurIt.Current.Value; }
			}

			public bool MoveNext()
			{
				return _CurIt.MoveNext();
			}

			public void Reset()
			{
				_CurIt.Reset();
			}
		}

		public class LocalFrame : IEnumerable<LocalEntry>
		{
			private class LocalEqualityComparer : IEqualityComparer<LocalEntry>
			{
				IEqualityComparer<String> StrComp;
				public LocalEqualityComparer(IEqualityComparer<String> @internal)
				{
					StrComp = @internal;
				}
				public bool Equals(LocalEntry x, LocalEntry y)
				{
					return StrComp.Equals(x.Name, y.Name);
				}

				public int GetHashCode(LocalEntry obj)
				{
					return StrComp.GetHashCode(obj.Name);
				}
			}

			Dictionary<String, LocalEntry> _LocalVars;
			Dictionary<String, VariableReference> _Unresolveds;
			public VariableReference LastUnresolved = null;
			public LocalEntry LastDuplicate = null;
			IEqualityComparer<String> StrComp;
			LocalEqualityComparer KeyComp;

			public LocalEntry this[String name]
			{
				get
				{
					return _LocalVars[name];
				}
			}

			public LocalFrame FrameIntercept(LocalFrame second)
			{
				return new LocalFrame(this.Intersect(second, KeyComp), StrComp);
			}

			public IEnumerable<LocalEntry> FrameExcept(LocalFrame second)
			{
				return this.Except(second, KeyComp);
			}

			public int Count
			{
				get
				{
					return _LocalVars.Count;
				}
			}

			public LocalFrame(IEqualityComparer<String> comp)
			{
				StrComp = comp;
				KeyComp = new LocalEqualityComparer(StrComp);
				_LocalVars = new Dictionary<string, LocalEntry>(comp);
				_Unresolveds = new Dictionary<string, VariableReference>(comp);
			}

			private LocalFrame(IEnumerable<LocalEntry> entries, IEqualityComparer<String> comp)
			{
				StrComp = comp;
				KeyComp = new LocalEqualityComparer(StrComp);
				_LocalVars = new Dictionary<string, LocalEntry>(StrComp);
				foreach (var e in entries)
					_LocalVars.Add(e.Name, e);
				_Unresolveds = new Dictionary<string, VariableReference>(comp);
			}

			/// <summary>
			/// You should have already done some work to make sure
			/// they're aren't any duplicates.
			/// </summary>
			/// <param name="frame"></param>
			public void Combine(IEnumerable<LocalEntry> frame)
			{
				foreach (var entry in frame)
					_LocalVars.Add(entry.Name, entry);
			}

			public Boolean Exists(String name)
			{
				if (_LocalVars.ContainsKey(name))
				{
					LastDuplicate = _LocalVars[name];
					return true;
				}
				return false;
			}
			public void NewVar(VariableReference vr)
			{
				if (_LocalVars.ContainsKey(vr.Name))
					throw new Exception("Attempting to create new var on local frame that already exists.");
				else
					_LocalVars.Add(vr.Name, new LocalEntry(vr));
			}
			public void AddEntry(LocalEntry entry)
			{
				_LocalVars.Add(entry.Name, entry);
			}
			public void Remove(String name)
			{
				_LocalVars.Remove(name);
			}

			public Boolean ExistsUn(VariableReference vr)
			{
				if (_Unresolveds.ContainsKey(vr.Name))
				{
					LastUnresolved = _Unresolveds[vr.Name];
					return true;
				}
				return false;
			}

			public void AddUn(VariableReference vr)
			{
				_Unresolveds.Add(vr.Name, vr);
			}

			public LocalFrame CreateCopy()
			{
				var cp = new LocalFrame(StrComp);
				foreach (var kvp in this)
					cp._LocalVars.Add(kvp.Name, kvp);
				return cp;
			}

			public IEnumerator<LocalEntry> GetEnumerator()
			{
				return new LocalFrameEnumerator(this._LocalVars);
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return new LocalFrameEnumerator(this._LocalVars);
			}
		}

		public class ContextModule : ContextNode
		{
			public Dictionary<String, Param> Arguments;
			public Stack<LocalFrame> Locals; //Stack because we can enumerate latest to earliest
			public CodeDOMModule DOM;
			public Dictionary<String, FunctionDefinition> FuncMap;
			public Dictionary<String, DeclaredConstant> ConstMap;
			public LocalFrame LastLookedUpLocal = null;
			public Param LastLookedUpParam = null;
			public ModuleReference PathReference = null;
			private IEqualityComparer<String> StrComp = null;
			private LocalFrame FirstFrame = null;


			public ContextModule(CodeDOMModule module, String[] paths, IEqualityComparer<String> comp)
				: base(module.Name, NodeTypes.Module)
			{
				StrComp = comp;
				this.Arguments = null;
				this.Locals = new Stack<LocalFrame>();
				this.DOM = module;

				FuncMap = new Dictionary<string, FunctionDefinition>(StrComp);
				ConstMap = new Dictionary<string, DeclaredConstant>(StrComp);

				foreach (var f in DOM.GetDefinitions())
					FuncMap.Add(f.Name, f);
				foreach (var c in DOM.Constants)
					ConstMap.Add(c.Name, c);
				this.IsResolved = this.DOM.IsDeclarative;

				List<String> ls = paths.ToList();
				ls.Add(DOM.Name);
				PathReference = ModuleReference.Create(ls);
			}

			public void SetArgument(IEnumerable<Param> args)
			{
				Arguments = args != null ? args.ToDictionary(p => p.Name, StrComp) : null;
			}

			public Boolean VarRefExistsAsParam(VariableReference vr)
			{
				if (Arguments.ContainsKey(vr.Name))
				{
					LastLookedUpParam = Arguments[vr.Name];
					return true;
				}
				return false;
			}

			public Boolean VarRefExists(VariableReference vr)
			{
				LastLookedUpLocal = null;
				foreach (var l in Locals)
				{
					if (l.Exists(vr.Name))
					{
						LastLookedUpLocal = l;
						return true;
					}
				}
				return false;
			}

			public Boolean VarRefExistsUn(VariableReference vr)
			{
				LastLookedUpLocal = null;
				foreach (var l in Locals)
				{
					if (l.ExistsUn(vr))
					{
						LastLookedUpLocal = l;
						return true;
					}
				}
				return false;
			}

			public void NewVar(VariableReference vr, Boolean bottomFrame)
			{
				if (!bottomFrame)
					Locals.Peek().NewVar(vr);
				else
					FirstFrame.NewVar(vr);
			}

			public void AddVarRefUn(VariableReference vr)
			{
				Locals.Peek().AddUn(vr);
			}

			public LocalFrame CreateScratchLocal()
			{
				return new LocalFrame(StrComp);
			}

			public void EnterScope(Statement statement)
			{
				Locals.Push(new LocalFrame(StrComp));
				statement.SourceFrame = Locals.Peek();
			}

			public void ExitScope()
			{
				if (Locals.Count == 1)
					if (Locals.Peek() != FirstFrame)
						throw new Exception("Attempted to exit last scope which has not been captured.");
					else
						FirstFrame = null;
				else
					if (Locals.Count > 1 && Locals.Peek() == FirstFrame)
						throw new Exception("First frame is not first frame at all!");
				Locals.Pop();
			}

			public void SetFirstFrame()
			{
				if (FirstFrame != null)
					throw new Exception("Attempted to set multiple first frames.");
				FirstFrame = Locals.Peek();
			}

			public LocalFrame Current
			{
				get
				{
					return Locals.Peek();
				}
			}

			public override string ToString()
			{
				return PathReference.Express(0);
			}
		}

		public class ContextDir : ContextNode
		{
			public Dictionary<String, ContextNode> Nodes;

			public ContextDir(String name, IEqualityComparer<String> comp)
				: base(name, NodeTypes.Dir)
			{
				Nodes = new Dictionary<string, ContextNode>(comp);
			}

			public void AddNode(ContextNode node)
			{
				if (Nodes.ContainsKey(node.Name))
					throw new Exception("Duplicate node!");
				Nodes.Add(node.Name, node);
			}

			public Boolean Exists(String name)
			{
				return Nodes.ContainsKey(name);
			}

			public Boolean DirExists(String name)
			{
				return Exists(name) && Nodes[name].NodeType == NodeTypes.Dir;
			}

			public Boolean ModuleExists(String name)
			{
				return Exists(name) && Nodes[name].NodeType == NodeTypes.Module;
			}

			public ContextNode this[String nodeName]
			{
				get
				{
					return Nodes.ContainsKey(nodeName) ? Nodes[nodeName] : null;
				}
			}


			/// <summary>
			/// Acccess a node from a this context dir using a module reference. The reference
			/// is relative, so performing this access from the base context dir would make
			/// all module references absolute.
			/// </summary>
			/// <param name="ref"></param>
			/// <returns></returns>
			public ContextNode this[ModuleReference @ref]
			{
				get
				{
					ContextDir curdir = this;
					foreach (String cur in @ref.Paths)
					{
						curdir = curdir[cur] as ContextDir;
						if (curdir == null)
							return null;
					}
					if (!curdir.ModuleExists(@ref.ModuleName))
						return null;
					return curdir[@ref.ModuleName];
				}
			}

			public ContextModuleEnumerator GetModuleEnumerator()
			{
				return new ContextModuleEnumerator(this);
			}

			public ContextDirEnumerator GetDirEnumerator()
			{
				return new ContextDirEnumerator(this);
			}

			public ContextShallowNodeEnumerator GetShallowNodeEnumerator()
			{
				return new ContextShallowNodeEnumerator(this);
			}
		}

		public class ContextShallowNodeEnumerator : IEnumerator<ContextNode>, IEnumerable<ContextNode>
		{
			IEnumerator<KeyValuePair<String, ContextNode>> _Internal;

			public ContextShallowNodeEnumerator(ContextDir dir)
			{
				_Internal = dir.Nodes.AsEnumerable().GetEnumerator();
			}

			public IEnumerator<ContextNode> GetEnumerator()
			{
				return this;
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return this;
			}

			public ContextNode Current
			{
				get { return _Internal.Current.Value; }
			}

			public void Dispose()
			{
			}

			object System.Collections.IEnumerator.Current
			{
				get { return _Internal.Current.Value; }
			}

			public bool MoveNext()
			{
				return _Internal.MoveNext();
			}

			public void Reset()
			{
				_Internal.Reset();
			}
		}

		public sealed class ContextModuleEnumerator : IEnumerator<ContextModule>, IEnumerable<ContextModule>
		{
			Stack<ContextDir> _Stack;
			IEnumerator<KeyValuePair<String, ContextNode>> _Internal;
			ContextDir _Root;

			public ContextModuleEnumerator(ContextDir dir)
			{
				this._Root = dir;
				_Internal = _Root.Nodes.AsEnumerable().GetEnumerator();
				_Stack = new Stack<ContextDir>();
			}

			public ContextModule Current
			{
				get { return _Internal.Current.Value.AsModule(); }
			}

			void IDisposable.Dispose()
			{
			}

			object System.Collections.IEnumerator.Current
			{
				get { return _Internal.Current.Value.AsModule(); }
			}

			bool System.Collections.IEnumerator.MoveNext()
			{
				while (true)
				{
					while (_Internal.MoveNext())
					{
						if (_Internal.Current.Value.NodeType == ContextNode.NodeTypes.Dir)
						{
							_Stack.Push(_Internal.Current.Value.AsDir());
							continue;
						}
						return true;
					}
					if (_Stack.Count == 0)
						break;
					_Internal = _Stack.Pop().Nodes.AsEnumerable().GetEnumerator();
				}
				return false;
			}

			void System.Collections.IEnumerator.Reset()
			{
				_Stack.Clear();
				_Internal = _Root.Nodes.AsEnumerable().GetEnumerator();
			}

			public IEnumerator<ContextModule> GetEnumerator()
			{
				return this;
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return this;
			}
		}

		public class ContextDirEnumerator : IEnumerator<ContextDir>, IEnumerable<ContextDir>
		{
			Stack<ContextDir> _Stack;
			IEnumerator<KeyValuePair<String, ContextNode>> _Internal;
			ContextDir _Root;

			public ContextDirEnumerator(ContextDir dir)
			{
				this._Root = dir;
				_Internal = _Root.Nodes.AsEnumerable().GetEnumerator();
				_Stack = new Stack<ContextDir>();
			}

			public ContextDir Current
			{
				get { return _Internal.Current.Value.AsDir(); }
			}

			void IDisposable.Dispose()
			{
			}

			object System.Collections.IEnumerator.Current
			{
				get { return _Internal.Current.Value.AsModule(); }
			}

			bool System.Collections.IEnumerator.MoveNext()
			{
				while (true)
				{
					while (_Internal.MoveNext())
					{
						if (_Internal.Current.Value.NodeType != ContextNode.NodeTypes.Dir)
							continue;
						_Stack.Push(_Internal.Current.Value.AsDir());
						return true;
					}
					if (_Stack.Count == 0)
						break;
					_Internal = _Stack.Pop().Nodes.AsEnumerable().GetEnumerator();
				}
				return false;
			}

			void System.Collections.IEnumerator.Reset()
			{
				_Stack.Clear();
				_Internal = _Root.Nodes.AsEnumerable().GetEnumerator();
			}

			public IEnumerator<ContextDir> GetEnumerator()
			{
				return this;
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return this;
			}
		}

		#endregion
		public class MessageObject
		{
			public enum MessageType { Warning, Error }
			public MessageType MsgType;
			public String Msg;
			public String Target, Target2;
			public String Module, Module2;
			public Text.DocLocation? Location, Location2, TargetLocation, TargetLocation2;

			public Text.DocLocation? GetFirstLocation()
			{
				if (Location != null)
					return Location;
				else if (TargetLocation != null)
					return TargetLocation;
				else if (Location2 != null)
					return Location2;
				else if (TargetLocation2 != null)
					return TargetLocation2;
				else
					return null;
			}
		}
		public abstract class Message
		{
			public int MsgType;
			public Expression Target, Target2;
			public ContextModule Module, Module2;
			public Text.DocLocation? Location, Location2;
			protected abstract String GetMsgTypeString();
			public MessageObject CreateObject()
			{
				var mo = new MessageObject();
				mo.Target = Target != null ? Target.ToString() : null;
				mo.Target2 = Target2 != null ? Target2.ToString() : null;
				mo.Module = Module != null ? Module.DOM.FullPath : null;
				mo.Module2 = Module2 != null ? Module2.DOM.FullPath : null;
				mo.Msg = GetMsgTypeString();
				mo.TargetLocation = Target != null ? (DocLocation?)Target.Location : null;
				mo.TargetLocation2 = Target2 != null ? (DocLocation?)Target2.Location : null;
				mo.MsgType = GetMsgType();
				return mo;
			}

			public abstract MessageObject.MessageType GetMsgType();

			public override string ToString()
			{
				StringBuilder sb = new StringBuilder();
				sb.Append("{0} ".Fmt(GetMsgTypeString()));
				if (Target != null)
					sb.Append("Target: \"{0}\"{1}".Fmt(Target.Express(0), Target.Location));
				if (Target2 != null)
					sb.Append("Target2: \"{0}\"{1}".Fmt(Target2.Express(0), Target2.Location));
				if (Module != null)
					sb.Append("Module: {0} ".Fmt(Module.PathReference.Express(0)));
				if (Module2 != null)
					sb.Append("Module2: {0} ".Fmt(Module2.PathReference.Express(0)));
				if (Location != null)
					sb.Append("Location: {0} ".Fmt(Location));
				if (Location2 != null)
					sb.Append("Location2: {0} ".Fmt(Location2));
				return sb.ToString();
			}
		}	
		public class WarningMessage : Message
		{
			public ContextAnalyzer.WarnType WarnType
			{
				get { return (ContextAnalyzer.WarnType)this.MsgType; }
			}
			protected override string GetMsgTypeString()
			{
				return ((ContextAnalyzer.WarnType)this.MsgType).ToString();
			}
			public override MessageObject.MessageType GetMsgType()
			{
				return MessageObject.MessageType.Warning;
			}
		}
		public class ErrorMessage : Message
		{
			public ContextAnalyzer.ErrorType ErrorType
			{
				get { return (ContextAnalyzer.ErrorType)this.MsgType; }
			}
			protected override string GetMsgTypeString()
			{
				return ((ContextAnalyzer.ErrorType)this.MsgType).ToString();
			}
			public override MessageObject.MessageType GetMsgType()
			{
				return MessageObject.MessageType.Error;
			}
		}

		public class CompleteFrames
		{
			public LocalFrame[][] Frames;
			int Total;
			public int Count { get { return this.Total; } }
			public CompleteFrames(LocalFrame[][] completeframes)
			{
				this.Frames = completeframes;
				int total = 0;
				foreach (var frames in this.Frames)
					foreach (var frame in frames)
						total += frame.Count;
				this.Total = total;
			}
		}

		/// <summary>
		/// CA works by analyzing every add.
		/// Then when all the desired modules are added,
		/// A final analyze is done to resolve the rest.
		/// 
		/// Note that this method will not be able
		/// to recursively analyze modules, which is
		/// the exact antithesis of this algorithm.
		/// </summary>
		public class ContextAnalyzer
		{
			Boolean IsOnFinal = false;
			Boolean OV_Lock = false;
			ContextDir ContextRoot;
			Stack<ContextModule> ContextStack;
			public Dictionary<String, VariableReference> GlobalVars = null;
			public List<WarningMessage> Warnings;
			public List<ErrorMessage> Errors;
			public List<List<LocalFrame>> PerFrames;
			public static int MaxLocalVars = 65535;
			public static int MaxArguments = 255;
			ContextModule GlobalContext;
			CompilerOptions Options;
			InlineEvaluator IE;


			public enum WarnType
			{
				WhyAccessingConstant,
				IncludingSameModule,
				RecursiveInclude,
				MultiConditionalDualInitalizedVariableNeedsBreakInCase,
				IteratorOrKeyReferencesPredeclaredVariable
			}

			public enum ErrorType
			{
				FunctionAlreadyExists,
				UnknownFunctionInvocation,
				UnknownFunctionReferenceLocal,
				UnknownFunctionReferenceExternal,
				UnknownFunctionReferenceExternal_UnknownScope,
				UnknownFunctionReferenceExternal_NotModule,
				UnknownReferenceVariable,
				IndexingNonIndexibleConstant,
				VariableReferenceNeedsToBeInitialized,
				UsingUninitializedVariable,
				CannotAssignLeftValue,
				CannotAssignSelf,
				CannotAssignGlobal,
				InvalidPostfixOperand,
				TooManyArguments,
				TooFewThanMinimumArguments,
				UnaryCallNeedsArgument,
				UnaryCallHasNoArguments,
				ArgumentIsNotReference,
				ArgumentIsNotMutableReference,
				InvalidFunctionPointerCallArgument,
				TrupleItemMustBeFloatOrInteger,
				InvalidUnaryOperation,
				InvalidBinaryOperation,
				DivisionByZero,
				ExceedMaxLocalVars,
				ExceedMaxArguments,
				ExceedMaxParameters
			}

			public ContextAnalyzer(CompilerOptions options, CodeDOMModule globalModule = null)
			{
				Options = options;
				GlobalContext = globalModule != null ? new ContextModule(globalModule, new String[] { }, options.GetComparer()) : null;
				Warnings = new List<WarningMessage>();
				Errors = new List<ErrorMessage>();
				ContextStack = new Stack<ContextModule>();
				ContextRoot = new ContextDir(null, options.GetComparer());
				PerFrames = new List<List<LocalFrame>>();
				IE = new InlineEvaluator(options, new InlineEvaluator.EvaluationOptions
				{
					Allow_InlineArrays = true,
					Enable_AutoConvertQuotientToInteger = false,
					Enable_Logical = true,
					Allow_OperationTypeRuntimeRequires = true
				});

				if (GlobalContext != null)
				{
					//Global vars are also case sensitive.
					GlobalVars = new Dictionary<string, VariableReference>();
					foreach (var var in GlobalContext.DOM.GlobalVariables)
						GlobalVars.Add(var.Name, var);
				}
			}

			private void ResetScanContext()
			{
				OV_Lock = false;
			}

			private ContextModule CurrentModule
			{
				get
				{
					return ContextStack.Count > 0 ? ContextStack.Peek() : null;
				}
			}

			private void Warn(WarnType type, Expression target, Expression target2 = null,
				ContextModule module2 = null, DocLocation? location = null, DocLocation? location2 = null)
			{
				if (!IsOnFinal)
					return;
				Warnings.Add(new WarningMessage()
				{
					MsgType = (int)type,
					Target = target,
					Target2 = target2,
					Module = CurrentModule,
					Module2 = module2,
					Location = location,
					Location2 = location2
				});
			}

			private void Error(ErrorType type, Expression target, Expression target2 = null,
				ContextModule module2 = null, DocLocation? location = null, DocLocation? location2 = null)
			{
				if (!IsOnFinal)
					return;
				Errors.Add(new ErrorMessage()
				{
					MsgType = (int)type,
					Target = target,
					Target2 = target2,
					Module = CurrentModule,
					Module2 = module2,
					Location = location,
					Location2 = location2
				});
			}

			private class RecursionResult
			{
				public ContextModule Module,//Module where the offending import was declared
					Recursed;//The module that will repeat itself
				public ImportDirective Import;//Import where the recursion occurs
			}

			//TODO: Integrate this somehow where we normally check imports for consts and funcs.
			//maybe mark them or something.
			private RecursionResult CheckRecursion(ContextModule module)
			{
				return __CheckRecursion(module);
			}


			private RecursionResult __CheckRecursion(ContextModule module, Stack<ContextModule> trace = null)
			{
				if (trace == null)
					trace = new Stack<ContextModule>();
				foreach (var import in module.DOM.Imports)
				{
					var c = ContextRoot[import.Reference];
					if (c == null || c.NodeType != ContextNode.NodeTypes.Module)
						continue;
					if (c == module)
						return new RecursionResult() { Import = import, Module = module, Recursed = module };
					foreach (var cm in trace)
						if (c == cm)
							return new RecursionResult() { Import = import, Module = module, Recursed = cm };
					trace.Push(module);
					var rr = __CheckRecursion(c.AsModule(), trace);
					trace.Pop();
					if (rr != null)
						return rr;
				}
				return null;
			}

			/// <summary>
			/// Adds a new module into the ContextNode hierarchy based on the module's
			/// path.
			/// </summary>
			/// <param name="module">The DOM to add</param>
			/// <param name="paths">A string split of the directory of the module. i.e. /folder1/folder2 would be String[] paths = {"folder1", "folder2"}</param>
			/// <returns>The module created and placed in the hierarchy for the DOM</returns>
			private ContextModule AddModule(CodeDOMModule module, String[] paths)
			{
				ContextDir target = ContextRoot;
				foreach (String p in paths)
				{
					if (!target.DirExists(p))
						target.AddNode(target = new ContextDir(p, Options.GetComparer()));
					else
						target = target[p].AsDir();
				}
				ContextModule m;
				target.AddNode(m = new ContextModule(module, paths, Options.GetComparer()));
				return m;
			}

			public void AddNewModule(CodeDOMModule module, String[] paths)
			{
				AddModule(module, paths);
			}

			public void AnalyzeModule(ContextModule module)
			{
				if (module.IsResolved)
					return;
				ContextStack.Push(module);
				Boolean hasUnresolves = false;

				//Check all imports and use every import's module reference to check for self-references
				foreach (var import in module.DOM.Imports) //TODO: If I ever do more import iterations, please check for same module.
					if (ContextRoot[import.Reference] == CurrentModule)
						Warn(WarnType.IncludingSameModule, null, null, null, import.Location);

				foreach (var kvp in module.FuncMap)
				{
					var fd = kvp.Value;
					foreach (var id in CurrentModule.DOM.Imports)
					{
						var node = ContextRoot[id.Reference];
						if (node == null || node.NodeType != ContextNode.NodeTypes.Module)
							continue;
						var searchmodule = node.AsModule();
						var fl = FunctionExists(searchmodule, kvp.Value.Name, false);
						if (fl.Result != LookUpResult.DoesNotExist &&
							(fl.Result == LookUpResult.Exists || ((fl.Module != GlobalContext) || !Options.Allow_GlobalFunctionOverride)))
							{
								Error(ErrorType.FunctionAlreadyExists, fd.CreateFunctionCall(),
									fl.Function.CreateFunctionCall(), fl.Module, fd.Location,
									fl.Function.Location);
								continue;
							}
					}
					if (fd.RootStatement == null)
						continue;
					if (fd.Params.Length > MaxArguments)
					{
						Error(ErrorType.ExceedMaxParameters, null, null, null, fd.Location);
						continue;
					}
					CurrentModule.SetArgument(fd.Params);
					ResetScanContext();
					EnterCurrentModuleScopeFrame(fd.RootStatement);
					CurrentModule.SetFirstFrame();
					Scan(fd.RootStatement);
					fd.AllFrames = this.GetAllFrames();
					ExitCurrentModuleScopeFrame();
					fd.MaxExplicitLocals = fd.AllFrames.Count;
					CurrentModule.SetArgument(null); //Maybe catch any null refs
					if (!fd.RootStatement.IsResolved)
						hasUnresolves = true;
				}
				module.IsResolved = !hasUnresolves;
				ContextStack.Pop();
			}

			private delegate void OnModuleDelegate(ContextModule module);

			private void AnalyzeContextDir(ContextDir dir, OnModuleDelegate onModule)
			{
				Boolean HasUnresolves = false;
				foreach (var node in dir.GetShallowNodeEnumerator())
				{
					if (node.NodeType == ContextNode.NodeTypes.Dir)
						AnalyzeContextDir(node.AsDir(), onModule);
					else
					{
						onModule(node.AsModule());
						AnalyzeModule(node.AsModule());
					}
					if (!node.IsResolved)
						HasUnresolves = true;
				}
				dir.IsResolved = !HasUnresolves;
			}

			public Boolean FinalAnalyze()
			{
				Errors.Clear();
				Warnings.Clear();
				IsOnFinal = true;
				AnalyzeContextDir(ContextRoot, cm => { });
				if (GlobalContext != null)
					AnalyzeModule(GlobalContext);
				IsOnFinal = false;
				return ContextRoot.IsResolved;
			}

			/// <summary>
			/// Describes a look-up operation's results.
			/// </summary>
			private enum LookUpResult
			{
				/// <summary>
				/// Constant/Function is not found in the module or anywhere else.
				/// </summary>
				DoesNotExist,
				/// <summary>
				/// The Constant/Function exists within the module.
				/// </summary>
				Exists,
				/// <summary>
				/// The Constant/Function exists within another module.
				/// </summary>
				ExistsInOtherModule
			}

			/// <summary>
			/// Data structure that describes the lookup operation for a function.
			/// </summary>
			private struct FunctionLookup
			{
				/// <summary>
				/// The module the function belongs in.
				/// </summary>
				public ContextModule Module;
				/// <summary>
				/// The actual function definition for the function.
				/// </summary>
				public FunctionDefinition Function;
				/// <summary>
				/// The look-up result.
				/// </summary>
				public LookUpResult Result;
				/// <summary>
				/// The last function lookup result. I did this so I could directly get the result
				/// of a function-lookup call without declaring a temporary local variable to store it
				/// and use it later.
				/// </summary>
				public static FunctionLookup LastLookUp;

				public static FunctionLookup CreateNonExist()
				{
					FunctionLookup fl = new FunctionLookup();
					fl.Module = null;
					fl.Function = null;
					fl.Result = LookUpResult.DoesNotExist;
					return LastLookUp = fl;
				}

				public static FunctionLookup CreateExist(FunctionDefinition target, ContextModule currentModule)
				{
					FunctionLookup fl = new FunctionLookup();
					fl.Module = currentModule;
					fl.Function = target;
					fl.Result = LookUpResult.Exists;
					return LastLookUp = fl;
				}

				public static FunctionLookup CreateExistExternal(FunctionDefinition target, ContextModule cm)
				{
					FunctionLookup fl = new FunctionLookup();
					fl.Module = cm;
					fl.Function = target;
					fl.Result = LookUpResult.ExistsInOtherModule;
					return LastLookUp = fl;
				}
			}

			/// <summary>
			/// Data structure that describes the location of a constant from a constant-lookup function.
			/// </summary>
			private struct ConstantLookup
			{
				/// <summary>
				/// The module the constant is declared in. Null if not found.
				/// </summary>
				public ContextModule Module;
				/// <summary>
				/// The constant object that contains the constant's expression graph. Null if not found.
				/// </summary>
				public DeclaredConstant Constant;
				/// <summary>
				/// Enum that describes the result of the lookup.
				/// </summary>
				public LookUpResult Result;

				public static ConstantLookup CreateNonExist()
				{
					ConstantLookup cl = new ConstantLookup();
					cl.Module = null;
					cl.Constant = null;
					cl.Result = LookUpResult.DoesNotExist;
					return cl;
				}

				public static ConstantLookup CreateExist(DeclaredConstant constant, ContextModule currentModule)
				{
					if (constant == null)
						throw new Exception("Attempted to create constant-lookup with null args.");
					ConstantLookup cl = new ConstantLookup();
					cl.Module = currentModule;
					cl.Constant = constant;
					cl.Result = LookUpResult.Exists;
					return cl;
				}

				public static ConstantLookup CreateExistExternal(DeclaredConstant constant, ContextModule cm)
				{
					if (constant == null || cm == null)
						throw new Exception("Attempted to create constant-lookup with null args.");
					ConstantLookup cl = new ConstantLookup();
					cl.Module = cm;
					cl.Constant = constant;
					cl.Result = LookUpResult.ExistsInOtherModule;
					return cl;
				}
			}

			//TODO: Find a way to avoid recusion loops [fixed]
			/// <summary>
			/// Finds a function in the current module or in its includes.
			/// </summary>
			/// <param name="module">Module to look in</param>
			/// <param name="name">Name of function</param>
			/// <param name="explicit">Whether to only look in the current module</param>
			/// <returns>A lookup data structure that describes the location of the function.</returns>
			private FunctionLookup FunctionExists(ContextModule module, String name, Boolean @explicit)
			{
				return __FunctionExists(module, name, @explicit);
			}


			private FunctionLookup __FunctionExists(ContextModule module, String name, Boolean @explicit,
				Stack<ContextModule> Recursion = null)
			{
				if (Recursion != null && Recursion.Count > Options.MaximumScriptDepth)
					throw new Exception("Recursion has reached threshold.");
				//Prevents recursions
				if (Recursion != null && Recursion.FirstOrDefault(cm => cm == module) != null)
					return FunctionLookup.CreateNonExist();

				if (!@explicit && GlobalContext != null && GlobalContext.FuncMap.ContainsKey(name))
					return FunctionLookup.CreateExistExternal(GlobalContext.FuncMap[name], GlobalContext);

				if (module.FuncMap.ContainsKey(name))
					return FunctionLookup.CreateExist(module.FuncMap[name], module);

				if (@explicit)
					return FunctionLookup.CreateNonExist();
				//Check imports, this will work on final pass.
				//TODO: What I'm not really sure about is whether
				//we check functions directly in their imports
				//as they are defined in the module itself
				//of subcursively inside it's own imports.
				//For now we sub-curse.
				ContextNode node;
				FunctionLookup fl;
				foreach (var mr in module.DOM.Imports)
				{
					node = ContextRoot[mr.Reference];
					if (node == null || node.NodeType != ContextNode.NodeTypes.Module ||
						node == CurrentModule)
						continue;
					if (Recursion == null)
						Recursion = new Stack<ContextModule>();
					Recursion.Push(module);
					fl = __FunctionExists(node.AsModule(), name, false, Recursion);
					Recursion.Pop();
					if (fl.Result == LookUpResult.Exists)
						return FunctionLookup.CreateExistExternal(fl.Function, node.AsModule());
					else
						if (fl.Result == LookUpResult.ExistsInOtherModule)
							return FunctionLookup.CreateExistExternal(fl.Function, fl.Module);
				}
				return FunctionLookup.CreateNonExist();
			}

			private ConstantLookup ConstantExists(ContextModule module, String name, Boolean @explicit)
			{
				return __ConstantExists(module, name, @explicit);
			}

			private ConstantLookup __ConstantExists(ContextModule module, String name, Boolean @explicit,
				Stack<ContextModule> Recursion = null)
			{
				if (Recursion != null && Recursion.Count > Options.MaximumScriptDepth)
					throw new Exception("Recursion has reached threshold.");
				if (Recursion != null && Recursion.FirstOrDefault(cm => cm == module) != null)
					return ConstantLookup.CreateNonExist();
				if (!@explicit && GlobalContext != null && GlobalContext.ConstMap.ContainsKey(name))
					return ConstantLookup.CreateExistExternal(GlobalContext.ConstMap[name], GlobalContext);
				if (module.ConstMap.ContainsKey(name))
					return ConstantLookup.CreateExist(module.ConstMap[name], module);
				if (@explicit)
					return ConstantLookup.CreateNonExist();
				ContextNode node;
				ConstantLookup cl;
				foreach (var mr in module.DOM.Imports)
				{
					node = ContextRoot[mr.Reference];
					if (node == null || node.NodeType != ContextNode.NodeTypes.Module ||
						node == CurrentModule)
						continue;
					if (Recursion == null)
						Recursion = new Stack<ContextModule>();
					Recursion.Push(module);
					cl = __ConstantExists(node.AsModule(), name, !Options.Enable_ConstantDeepSearch, Recursion);
					Recursion.Pop();
					if (cl.Result == LookUpResult.Exists)
						return ConstantLookup.CreateExistExternal(cl.Constant, node.AsModule());
					else
						if (cl.Result == LookUpResult.ExistsInOtherModule)
							return ConstantLookup.CreateExistExternal(cl.Constant, cl.Module);
				}
				return ConstantLookup.CreateNonExist();
			}

			private enum ReferenceResultType
			{
				Local,
				Argument,
				DeclaredConstant,
				Global,
				Self,
				Unknown
			}

			private class ReferenceLookupResult
			{
				public ReferenceResultType ResultType;
				public ConstantLookup LastConstantLookUp;
				public LocalFrame LastLookupLocalFrame;
				public Param LastArg;

				public static ReferenceLookupResult CreateUnknown()
				{
					var r = new ReferenceLookupResult();
					r.LastLookupLocalFrame = null;
					r.LastConstantLookUp = default(ConstantLookup);
					r.LastArg = null;
					r.ResultType = ReferenceResultType.Unknown;
					return r;
				}

				public static ReferenceLookupResult CreateArgument(Param param)
				{
					var r = new ReferenceLookupResult();
					r.LastLookupLocalFrame = null;
					r.LastConstantLookUp = default(ConstantLookup);
					r.LastArg = param;
					r.ResultType = ReferenceResultType.Argument;
					return r;
				}

				public static ReferenceLookupResult CreateConstant(ConstantLookup cl)
				{
					var r = new ReferenceLookupResult();
					r.LastLookupLocalFrame = null;
					r.LastConstantLookUp = cl;
					r.LastArg = null;
					r.ResultType = ReferenceResultType.DeclaredConstant;
					return r;
				}

				public static ReferenceLookupResult CreateLocal(String name, LocalFrame target)
				{
					var r = new ReferenceLookupResult();
					r.LastLookupLocalFrame = target;
					r.LastConstantLookUp = default(ConstantLookup);
					r.LastArg = null;
					r.ResultType = ReferenceResultType.Local;
					return r;
				}

				public static ReferenceLookupResult CreateGlobal(String name)
				{
					return new ReferenceLookupResult()
					{
						LastLookupLocalFrame = null,
						LastArg = null,
						LastConstantLookUp = default(ConstantLookup),
						ResultType = ReferenceResultType.Global
					};
				}

				public static ReferenceLookupResult CreateSelf()
				{
					return new ReferenceLookupResult()
					{
						LastLookupLocalFrame = null,
						LastArg = null,
						LastConstantLookUp = default(ConstantLookup),
						ResultType = ReferenceResultType.Self
					};
				}
			}

			private ReferenceLookupResult IdentifiyVariableReference(VariableReference vr)
			{
				var cl = ConstantExists(CurrentModule, vr.Name, !Options.Enable_ConstantDeepSearch);
				if (cl.Result != LookUpResult.DoesNotExist)
					return ReferenceLookupResult.CreateConstant(cl);
				if (CurrentModule.VarRefExistsAsParam(vr))
					return ReferenceLookupResult.CreateArgument(CurrentModule.LastLookedUpParam);
				if (CurrentModule.VarRefExists(vr))
					return ReferenceLookupResult.CreateLocal(vr.Name,
						CurrentModule.LastLookedUpLocal);
				if (IsGlobalVar(vr))
					return ReferenceLookupResult.CreateGlobal(vr.Name);
				if (IsSelf(vr))
					return ReferenceLookupResult.CreateSelf();
				return ReferenceLookupResult.CreateUnknown();
			}

			public String CurrentLocalVarsString
			{
				get
				{
					List<String> levels = new List<string>();
					foreach (List<LocalFrame> frames in this.PerFrames)
					{
						var l1 = new List<String>();
						foreach (var frame in frames)
							l1.Add("[{0}]".Fmt(frame.JoinExt(", ", le => le.Name)));
						levels.Add("{{{0}}}".Fmt(l1.JoinExt(", ", s => s)));
					}
					return levels.JoinExt(", ", s => s);
				}
			}

			public CompleteFrames GetAllFrames()
			{
				var tmplist = new List<LocalFrame[]>();
				tmplist.AddRange(this.PerFrames.Select(f => f.ToArray()));
				this.PerFrames.Clear();
				return new CompleteFrames(tmplist.ToArray());
			}

			//TODO: Redo these to acknowledge empty returns for valueless functions.
			private enum ReturnRating
			{
				Never,
				Always,
				Undetermined
			}

			//private struct ReturnRatingFlags
			//{
			//    ReturnRating Rating;
			//}

			//TODO: Add a parameter for expression type checking. For example, a truple cannot have a
			//return statement or a statement block. Read journal for more info.
			public Boolean ScanMulti(IEnumerable<Expression> exprs, Boolean isStatementSequence = true)
			{
				Boolean hasUnresolves = false;
				if (!isStatementSequence)
					foreach (var e in exprs)
					{
						if (e == null || e.IsResolved)
							continue;
						Scan(e);
						if (!e.IsResolved)
							hasUnresolves = true;
					}
				else
				{
					foreach (var e in exprs)
					{
						if (e == null || e.IsResolved)
							continue;
						Scan(e);
						if (!e.IsResolved)
							hasUnresolves = true;
					}
				}
				return !hasUnresolves;
			}

			private Boolean IsSelf(VariableReference vr)
			{
				return vr.Name == "self" && vr.ScopeType == Reference.ScopeTypes.Local;
			}

			private Boolean IsGlobalVar(VariableReference vr)
			{
				if (GlobalContext == null)
					return false;
				return GlobalVars.ContainsKey(vr.Name);
			}

			private void OnFunctionReference(FunctionReference f, FunctionCall call = null)
			{
				if (f.ScopeType == Reference.ScopeTypes.External)
				{
					ContextNode cn = ContextRoot[f.Scope];
					if (cn == null)
						Error(ErrorType.UnknownFunctionReferenceExternal_UnknownScope, f);
					else if (cn.NodeType != ContextNode.NodeTypes.Module)
						Error(ErrorType.UnknownFunctionReferenceExternal_NotModule, f);
					else if (FunctionExists(cn.AsModule(), f.Name, true).Result != LookUpResult.DoesNotExist)// Wouldn't make sense if externals were recursive.
					{
						f.IsResolved = true;
						f.Source = new ReferenceSourceFunction(cn.AsModule(), f.Name, f, call);
					}
					else
						Error(ErrorType.UnknownFunctionReferenceExternal, f);
				}
				else
					if (f.ScopeType == Reference.ScopeTypes.Local)
						if (FunctionExists(CurrentModule, f.Name, f.LocalExplicit).Result != LookUpResult.DoesNotExist)
						{
							var fl = FunctionLookup.LastLookUp;
							f.IsResolved = true;
							if (fl.Result == LookUpResult.Exists)
								f.Source = new ReferenceSourceFunction(CurrentModule,
									fl.Function.Name, f, call);
							else if (fl.Result == LookUpResult.ExistsInOtherModule)
								f.Source = new ReferenceSourceFunction(fl.Module,
									fl.Function.Name, f, call);
							else
								throw new NotImplementedException();
						}
						else
							Error(ErrorType.UnknownFunctionReferenceLocal, f);
					else
						throw new NotImplementedException();
			}

			/// <summary>
			/// Call this on normal VRs if you need other treatment of VRs besides scanning them. This
			/// also does unresolveds checking.
			/// </summary>
			/// <param name="vr"></param>
			/// <param name="isRef"></param>
			void OnVariableReference(VariableReference vr, bool isRef)
			{
				ReferenceLookupResult lr = IdentifiyVariableReference(vr);
				if (IsUnRe(vr))
					Error(ErrorType.UsingUninitializedVariable, vr, CurrentModule.LastLookedUpLocal.LastUnresolved);
				else
					switch (lr.ResultType)
					{
						case ReferenceResultType.Local:
							OnLocalVarRef(vr, lr.LastLookupLocalFrame, isRef);
							break;
						case ReferenceResultType.DeclaredConstant:
							vr.IsResolved = true;
							vr.Source = new ReferenceSourceConstant(lr.LastConstantLookUp.Constant.Name,
							lr.LastConstantLookUp.Result == LookUpResult.ExistsInOtherModule ?
							lr.LastConstantLookUp.Module : CurrentModule);
							break;
						case ReferenceResultType.Argument:
							vr.Source = new ReferenceSourceArgument(lr.LastArg, false);
							vr.IsResolved = true;
							break;
						case ReferenceResultType.Unknown:
							Error(ErrorType.UnknownReferenceVariable, vr);
							CurrentModule.AddVarRefUn(vr);
							break;
						case ReferenceResultType.Global:
							vr.IsResolved = true;
							vr.Source = new ReferenceSourceGlobal(vr.Name);
							break;
						case ReferenceResultType.Self:
							vr.IsResolved = true;
							vr.Source = new ReferenceSourceSelf();
							break;
						default:
							throw new NotImplementedException();
					}
			}

			void OnLocalVarRef(VariableReference vr, LocalFrame target, Boolean isRef)
			{
				vr.IsResolved = true;
				vr.Source = new ReferenceSourceLocal(vr.Name, false, target, isRef);
				target[vr.Name].References.Add(vr);
			}

			void CreateNewLocal(VariableReference vr, Boolean isRef, Boolean bottomFrame = false)
			{
				vr.IsResolved = true;
				vr.Source = new ReferenceSourceLocal(vr.Name, true, CurrentModule.Current, isRef);
				CurrentModule.NewVar(vr, bottomFrame);
			}

			void OnAssignment(AssignmentStatement a)
			{
				if (a.Left.IsAssignable())
					if (a.Left.IsVariableReference())
					{
						var vr = a.Left.AsVariableReference();
						ReferenceLookupResult lr = IdentifiyVariableReference(vr);

						if (!IsNotImmutable(vr))
							return;
						else if (a.OperationType == BinaryOperationType.Assign
								&& lr.ResultType == ReferenceResultType.Unknown && !IsUnRe(vr))
								CreateNewLocal(vr, false);
							/*else
								Error(ErrorType.VariableReferenceNeedsToBeInitialized, vr);*/
						else
							Scan(a.Left, false, ExpressionState.IsLValueOfAssignment);
					}
					else
						Scan(a.Left, false, ExpressionState.IsLValueOfAssignment);
				else
					Error(ErrorType.CannotAssignLeftValue, a.Left);

				Scan(a.Right);
				a.IsResolved = a.Left.IsResolved && a.Right.IsResolved;
			}

			/// <summary>
			/// Does a search for constants/globals/selfs to see
			/// if identifier is eligble to be a new local variable based
			/// on it being used as the l-value in an assignment. An existing
			/// local may also return true from this function.
			/// </summary>
			/// <param name="vr"></param>
			/// <returns></returns>
			bool IsNotImmutable(VariableReference vr)
			{
				ReferenceLookupResult lr = IdentifiyVariableReference(vr);
				if (lr.ResultType == ReferenceResultType.Self)
					Error(ErrorType.CannotAssignSelf, vr);
				else if (lr.ResultType == ReferenceResultType.Global)
					Error(ErrorType.CannotAssignGlobal, vr);
				else if (lr.ResultType == ReferenceResultType.DeclaredConstant)
					Error(ErrorType.IndexingNonIndexibleConstant, vr, null, lr.LastConstantLookUp.Module,
						null, lr.LastConstantLookUp.Constant.Location);
				else
					return true;
				return false;
			}

			/// <summary>
			/// Checks if binary or unary operation is a valid operation on constant types.
			/// </summary>
			/// <param name="expr"></param>
			/// <returns></returns>
			public Boolean CheckOperation(Expression expr)
			{
				IE.SetConstDeclLookup(CurrentModule.DOM.Constants);
				IE.Evaluate(expr);

				switch (IE.Result.ResultType)
				{
					case InlineEvaluator.EvaluationResult.DivisionByZero:
						Error(ErrorType.DivisionByZero, IE.Result.ErrorExpr);
						break;
					case InlineEvaluator.EvaluationResult.InvalidOperation:
						var exprs = IE.Result.InvalidMsg.Exprs;
						if (IE.Result.InvalidMsg.MsgType == InlineEvaluator.InvalidErrorType.IncompatibleTypeForUnaryOperation)
							Error(ErrorType.InvalidUnaryOperation, exprs[0] as Expression);
						else
							Error(ErrorType.InvalidBinaryOperation, exprs[0] as Expression, exprs[1] as Expression);
						break;
					default:
						return true;
				}
				expr.IsResolved = false;
				return false;
			}

			private void EnterCurrentModuleScopeFrame(Statement statement)
			{
				CurrentModule.EnterScope(statement);
				if (CurrentModule.Locals.Count - PerFrames.Count > 1)
					throw new Exception("A scope was entered more than once without a PerFrame capture");
				else if (CurrentModule.Locals.Count - PerFrames.Count == 1)
					PerFrames.Add(new List<LocalFrame>());
				PerFrames[CurrentModule.Locals.Count - 1].Add(CurrentModule.Locals.Peek());
			}

			private void ExitCurrentModuleScopeFrame()
			{
				CurrentModule.ExitScope();
			}

			private bool IsUnRe(VariableReference vr)
			{
				return CurrentModule.VarRefExistsUn(vr);
			}

			enum ExpressionState
			{
				None = 0,
				IsLValueOfAssignment = 1
			}

			private void Scan(Expression expr, Boolean newLocalScope = false, ExpressionState state = ExpressionState.None)
			{
				if (expr == null)
					throw new Exception("Whoopsies, CA scanned null expr");
				if (expr.IsResolved)
					return;
				if (newLocalScope)
				{
					if (!expr.IsStatement())
						throw new Exception("Attempted to create new local scope on non-statement.");
					EnterCurrentModuleScopeFrame(expr.AsStatement());
				}
				if (!OV_Lock && expr.IsOperation())
					OV_Lock = true;

				switch (expr.GetExpressionType())
				{
					#region Scan Reference
					case Expression.ExpressionTypes.Reference:
						{
							var r = expr.AsReference();
							switch (r.GetReferenceType())
							{
								case Reference.ReferenceTypes.Accessor:
									{
										var a = r.AsAccessor();
										if (a.TargetObject.IsConstant())
											Warn(WarnType.WhyAccessingConstant, a);
										Scan(a.TargetObject);
										if (a.TargetObject.IsResolved)
											a.IsResolved = true;
									}
									break;
								case Reference.ReferenceTypes.Function:
									{
										var f = r.AsFunctionReference();
										//TODO: Maybe we can add relative paths later, for now the spec is absolute...damn.
										OnFunctionReference(f);
									}
									break;
								case Reference.ReferenceTypes.Indexer:
									{
										var i = r.AsIndexer();
										//TODO: If we did use IE to evaluate constants, use this evaluation to determine whether the declared constant
										//is indexable, more specifically a string.

										//Apparently setting a new unknown variable is also assigning it, it's just an array instead of
										//of a direct object of the assignee
										if (i.Array.IsVariableReference())
										{
											var vr = i.Array.AsVariableReference();
											if (IsUnRe(vr))
												break;
											var lr = IdentifiyVariableReference(vr);
											if (lr.ResultType == ReferenceResultType.DeclaredConstant &&
												!lr.LastConstantLookUp.Constant.Value.IsIndexable())
												Error(ErrorType.IndexingNonIndexibleConstant, r, null, lr.LastConstantLookUp.Module);
											else if (lr.ResultType == ReferenceResultType.Unknown
												&& (state | ExpressionState.IsLValueOfAssignment) != ExpressionState.None) //Detect "setting elements" on unknowns.
												CreateNewLocal(vr, false);
											else
												Scan(vr);
										}
										else //TODO: Detect foreach'ing things like constants, exception being strings
											//If we don't do this, we can always rely on the runtime.
											Scan(i.Array);
										Scan(i.Index);
										i.IsResolved = i.Array.IsResolved && i.Index.IsResolved;
									}
									break;
								case Reference.ReferenceTypes.Variable:// We do new declares in the assignments, not here.
									{
										//TODO: For all resolved references we need to implement a reference locator.
										//[done] Added SourceReference descriptor class.
										var vr = r.AsVariableReference();
										OnVariableReference(vr, false);
									}
									break;
							}
						}
						break;
					#endregion
					case Expression.ExpressionTypes.BinaryOperator:
						{
							//TODO: Do extra checking here, for now it's simple
							//[delegated] to CheckOperations
							var bo = expr.AsBinaryOperation();
							Scan(bo.Left);
							Scan(bo.Right);
							bo.IsResolved = bo.Left.IsResolved && bo.Right.IsResolved;
						}
						break;
					case Expression.ExpressionTypes.Case:
						throw new NotSupportedException("Case isn't even suppose to exist. What's going on?");
					case Expression.ExpressionTypes.Constant:
						if (expr.AsConstant().ConstantType == Runtime.VarType.Object)
							throw new NotSupportedException("We never guess non-immutable types, What's going on?");
						expr.IsResolved = true;
						break;
					case Expression.ExpressionTypes.Group:
						{
							var g = expr.AsGroup();
							Scan(g.Value);
							g.IsResolved = g.Value.IsResolved;
						}
						break;
					case Expression.ExpressionTypes.InlineArray:
						{
							var ia = expr.AsInlineArray();
							expr.IsResolved = ScanMulti(ia.Items);
						}
						break;
					#region Scan Statements
					case Expression.ExpressionTypes.Statement:
						{
							var s = expr.AsStatement();
							switch (s.GetStatementType())
							{
								case Statement.StatementTypes.Assignment:
									{
										var a = s.AsAssignment();
										OnAssignment(a);
									}
									break;
								case Statement.StatementTypes.Block:
									{
										var sb = expr.AsStatementBlock();
										
										if (ScanMulti(sb.Statements))
											expr.IsResolved = true;
									}
									break;
								case Statement.StatementTypes.Conditional:
									{
										var conditional = expr.AsConditionalStatement();
										switch (conditional.GetConditionType())
										{
											case ConditionalTypes.BiConditional:
												{
													var bc = conditional.AsBiConditionalStatement();
													Boolean canMergeScopes = bc.ConverseStatement != null;
													Scan(bc.Condition);

													if (bc.ConverseStatement == null)
														Scan(bc.PrimaryStatement, true);
													else
													{
														LocalFrame frame_a, frame_b;
														Scan(bc.PrimaryStatement, true);
														frame_a = bc.PrimaryStatement.SourceFrame;
														Scan(bc.ConverseStatement, true);
														frame_b = bc.ConverseStatement.SourceFrame;


														//TODO: Since both branches initialize the similar locals
														//we need to make one reference instead of initalize.

														//Continue frame merging despite scan failures
														if (frame_a != null && frame_b != null)
														{
															LocalFrame current = CurrentModule.Current;
															List<LocalEntry> shared = new List<LocalEntry>(frame_a.FrameIntercept(frame_b));
															//Merge with current
															CurrentModule.Current.Combine(shared);
															foreach (LocalEntry entry in shared)
															{
																//Actually let them be VISes. They're dual initialized.
																//Update all references
																foreach (var r in frame_a[entry.Name].References)
																	r.Source.AsLocal().TargetFrame = CurrentModule.Current;
																foreach (var r in frame_b[entry.Name].References)
																	r.Source.AsLocal().TargetFrame = CurrentModule.Current;
																//Remove the local entry since these migrated locals
																//dont belong to these branches anymore.
																//We can actually keep them, but there would
																//be unused stack space.
																frame_a.Remove(entry.Name);
																frame_b.Remove(entry.Name);
															}
														}
													}

													bc.IsResolved = (bc.Condition.IsResolved == bc.PrimaryStatement.IsResolved == true)
														&& (bc.ConverseStatement == null || bc.ConverseStatement.IsResolved);
												}
												break;
											case ConditionalTypes.Converse:
												{
													throw new NotImplementedException("Parser shouldn't even have put a converse here.");
												}
											case ConditionalTypes.Loop:
												{
													var l = conditional.AsConditionalLoopStatement();
													Scan(l.Condition);
													Scan(l.PrimaryStatement, true);
													l.IsResolved = l.Condition.IsResolved == l.PrimaryStatement.IsResolved == true;
												}
												break;
											case ConditionalTypes.LoopEx:
												{
													var le = conditional.AsConditionalLoopExStatement();
													/*Any VISes will be into the current.*/
													//CurrentModule.EnterScope();
													if (le.Init != null) Scan(le.Init); //May treat differently if we added strong types.
													if (le.Condition != null) Scan(le.Condition);
													if (le.Repeater != null) Scan(le.Repeater);
													if (le.PrimaryStatement != null) Scan(le.PrimaryStatement, true);
													//CurrentModule.ExitScope();
													le.IsResolved = (le.Init == null || le.Init.IsResolved)
														&& (le.Condition == null || le.Condition.IsResolved)
														&& (le.Repeater == null || le.Repeater.IsResolved)
														&& (le.PrimaryStatement == null || le.PrimaryStatement.IsResolved);
												}
												break;
											case ConditionalTypes.MultiConditional:
												{
													var mc = conditional.AsMultiConditional();
													MultiConditionalStatement.Segment[] segments = mc.GetSegments();
													Boolean hasUnresolves = false;
													LocalFrame globalFrame = null;

													Scan(mc.CaseExpression);
													//TODO: Remove the true branch and try to fuse it with the converse
													//to releive as one. Also try to only emit no-default errors when
													//there is a non-empty shared-common local frame.

													/*if (!mc.HasDefault || !hasBreakOnDefault)
													{
														foreach (var seg in segments)
														{
															CurrentModule.EnterScope(seg.Statements);
															Scan(seg.Statements, true);
															CurrentModule.ExitScope();
															if (!seg.Statements.IsResolved)
																hasUnresolves = true;
														}
														mc.PrimaryStatement.IsResolved = !hasUnresolves;
														globalFrame = CurrentModule.CreateScratchLocal();
														foreach (var seg in segments)
															foreach (var kvp in seg.Statements.SourceFrame)
															{
																if (!globalFrame.Exists(kvp.Name))
																	globalFrame.AddEntry(kvp);
																//Re-direct frames again
																foreach (var r in kvp.References)
																	r.Source.AsLocal().TargetFrame = globalFrame;
															}
														mc.PrimaryStatement.SourceFrame = globalFrame;
													}
													else*/
													//If it has a default case, it's elligible for same-scope variable instantiations.
													LocalFrame sharedframe = null;
													var shallows = new List<KeyValuePair<string, VariableReference>>();
													/*
														* We need to grab the common vars. To do that we
														* individually scan each segment, establishing
														* a common frame created from the first segment.
														* 
														* Remove the non-intersecting names
														*/

													foreach (var seg in segments)
													{
														EnterCurrentModuleScopeFrame(seg.Statements);
														Scan(seg.Statements, true);
														ExitCurrentModuleScopeFrame();
														if (!seg.Statements.IsResolved)
															hasUnresolves = true;
														if (sharedframe != null)
															sharedframe = sharedframe.FrameIntercept(seg.Statements.SourceFrame);
														else
															sharedframe = seg.Statements.SourceFrame.CreateCopy();
													}

													if (mc.HasDefault)
													{
														//Check if each one is in a segment with at least one break.
														foreach (LocalEntry le in sharedframe.CreateCopy())
														{
															Boolean hasBreak = false;
															foreach (var seg in segments)
																if (seg.Statements.Count > 0
																	&& seg.Statements.Last().IsFlowControl(FlowControlTypes.Break))
																{
																	hasBreak = true;
																	break;
																}
															if (!hasBreak)
															{
																sharedframe.Remove(le.Name);
																Warn(WarnType.MultiConditionalDualInitalizedVariableNeedsBreakInCase, le.References[0]);
															}
														}
														//Combine with with current
														CurrentModule.Current.Combine(sharedframe);
														//Redirect all local frame pointers to higher current one.
														foreach (var seg in segments)
															foreach (var entry in sharedframe)
																foreach (var varref in entry.References)
																	varref.Source.AsLocal().TargetFrame = CurrentModule.Current;
													}
													//We've merged all the commons, now coalesce each case's local frame and merge them
													//into a single local frame for the PrimaryStatement's LocalFrame.
													mc.PrimaryStatement.IsResolved = !hasUnresolves;
													globalFrame = CurrentModule.CreateScratchLocal();
													foreach (var seg in segments)
														foreach (var kvp in seg.Statements.SourceFrame)
														{
															if (!globalFrame.Exists(kvp.Name))
																globalFrame.AddEntry(kvp);
															//Re-direct frames again
															foreach (var r in kvp.References)
																r.Source.AsLocal().TargetFrame = globalFrame;
														}
													mc.PrimaryStatement.SourceFrame = globalFrame;
													
													mc.IsResolved = hasUnresolves && mc.CaseExpression.IsResolved && mc.PrimaryStatement.IsResolved;
												}
												break;
											case ConditionalTypes.ObjectLoop:
												{
													//TODO: More checking here to. Detect shit like "foreach(i in 3)"
													var ol = conditional.AsConditionalObjectLoop();
													EnterCurrentModuleScopeFrame(ol.PrimaryStatement);
													//TODO: Add warnings to duplicate variables with different meanings. [fixed]
													foreach (var vr in new [] { ol.Key, ol.Iterator })
														if (vr != null)
														{
															if (IsUnRe(vr))
																continue;
															ReferenceLookupResult lr = IdentifiyVariableReference(vr);
															if (!IsNotImmutable(vr))
																continue;
															if (lr.ResultType == ReferenceResultType.Unknown)
																CreateNewLocal(vr, false);
															else
															{
																//If it's not an unknown but is a local or argument, continue as is.
																Scan(vr);
																if (vr.IsResolved)
																	Warn(WarnType.IteratorOrKeyReferencesPredeclaredVariable, vr);
															}
														}
													Scan(ol.Collection);
													Scan(ol.PrimaryStatement, true);
													ExitCurrentModuleScopeFrame();
													ol.IsResolved = ol.PrimaryStatement.IsResolved && ol.Collection.IsResolved;
												}
												break;
										}
									}
									break;
								case Statement.StatementTypes.FixOperator:
									{
										var fo = expr.AsPostfixStatement();
										if (fo.Operand.IsAssignable())
										{
											Scan(fo.Operand);
											fo.IsResolved = fo.Operand.IsResolved == true;
										}
										else
											Error(ErrorType.InvalidPostfixOperand, fo.Operand);
									}
									break;
								case Statement.StatementTypes.FlowControl:
									expr.IsResolved = true;
									break;
								case Statement.StatementTypes.FunctionCall:
									{
										/*TODO: Make sure refs are given variables, make them fresh if new.
										 *Account for variable arguments.
										 *Only non-function references can be in the place of a reference parameter.
										 *Variable references allowed are locals, and parameters.
										 *If a variable reference is not resolved and is passed as a reference
										 *parameter, it is a new local.
										 */
										var fc = expr.AsFunctionCall();
										Boolean CallsOkay = false;
										//First check the function reference itself.
										if (fc.Self != null)
											Scan(fc.Self);
										OnFunctionReference(fc.Function, fc);
										if (fc.Function.IsResolved)
										{
											var def = fc.Function.Source.Definition;
											if (fc.Arguments.Count > MaxArguments)
												Error(ErrorType.ExceedMaxArguments, fc);
											else if (def.MinimumArgs.HasValue && fc.Arguments.Count < def.MinimumArgs.Value)
												Error(ErrorType.TooFewThanMinimumArguments, fc);
											else if (!def.HasVariableArguments && fc.Arguments.Count > def.Params.Length)
												Error(ErrorType.TooManyArguments, fc, null, fc.Function.Source.Module, null, fc.Function.Source.Definition.Location);
											else
											{
												Boolean hasUnresolves = false;
												IEnumerator<Param> it = def.Params.AsEnumerable().GetEnumerator();
												Param param = null;
												foreach (var arg in fc.Arguments)
												{
													if (it.MoveNext())
														param = it.Current;

													if (!param[ParamModifiers.Reference])
													{//Handle normally
														Scan(arg);
														if (!arg.IsResolved && !hasUnresolves)
															hasUnresolves = true;
													}
													else//Make sure it's a new variable, existing variable, indexer or accessor.
														if (arg.IsReference() && !arg.IsReference(Reference.ReferenceTypes.Function))
														{
															var @ref = arg.AsReference();
															if (@ref.IsVariableReference())
															{
																VariableReference vr = @ref.AsVariableReference();
																ReferenceLookupResult lr = IdentifiyVariableReference(vr);
																if (!IsUnRe(vr))
																	switch (lr.ResultType)
																	{
																		case ReferenceResultType.Argument:
																			vr.IsResolved = true;
																			vr.Source = new ReferenceSourceArgument(lr.LastArg, true);
																			break;
																		case ReferenceResultType.Local:
																			OnLocalVarRef(vr, lr.LastLookupLocalFrame, true);
																			break;
																		case ReferenceResultType.Unknown:
																			//TODO: Make this function-global not current scope [fixed]
																			CreateNewLocal(vr, true, true);
																			break;
																		default:
																			Error(ErrorType.ArgumentIsNotMutableReference, vr);
																			break;
																	}
															}
															else
																Scan(@ref);
															if (!@ref.IsResolved && !hasUnresolves)
																hasUnresolves = true;
														}
														else
															Error(ErrorType.ArgumentIsNotReference, arg);
												}
												CallsOkay = !hasUnresolves;
											}
										}
										else
											Error(ErrorType.UnknownFunctionInvocation, fc);

										fc.IsResolved = CallsOkay && (fc.Self == null || fc.Self.IsResolved)
											&& fc.Function.IsResolved;
									}
									break;
								case Statement.StatementTypes.PointerCall:
									{
										var pc = expr.AsPointerFunctionCall();
										if (pc.FunctionPointer.IsReference() ||
											pc.FunctionPointer.IsInvocation())
											Scan(pc.FunctionPointer);
											/*Can only be accessor, variable reference, indexer, function reference (dunno why we allow it),
											  or function invocation */
										else
											Error(ErrorType.InvalidFunctionPointerCallArgument, pc);
										if (pc.Self != null)
											Scan(pc.Self);
										pc.IsResolved = ScanMulti(pc.Arguments) && (pc.Self != null && pc.Self.IsResolved)
											&& pc.FunctionPointer.IsResolved;
									}
									break;
								case Statement.StatementTypes.UnaryCall:
									{
										var uc = expr.AsUnaryCall();
										if (uc.UnaryType == UnaryCall.UnaryTypes.Other)
											if (StringMap.UnaryFuncs[uc.UnaryFunctionType].HasArg)
												if (uc.Arg != null)
													Scan(uc.Arg);
												else
													Error(ErrorType.UnaryCallHasNoArguments, uc);
											else
												if (uc.Arg == null)
													uc.IsResolved = true;
												else
													Error(ErrorType.UnaryCallNeedsArgument, uc);
										else
											//TODO: Emit a warning when a function mixes empty returns with parametered returns.
											if (uc.Arg == null)
												uc.IsResolved = true;
											else
											{
												Scan(uc.Arg);
												uc.IsResolved = uc.Arg.IsResolved;
											}
									}
									break;
							}
						}
						break;
					#endregion
					case Expression.ExpressionTypes.Truple:
						{
							var t = expr.AsTruple();
							//t.IsResolved = ScanMulti(t.Items);
							Boolean hasUnre = false;
							foreach (Expression item in t.Items)
							{
								Scan(item);
								if (!item.IsResolved)
									hasUnre = true;
								//TODO: When the IE is finished we can use that to determine whether
								//it is a constant instead of checking for its expression type.
								if (item.IsConstant())
								{
									var c = item.AsConstant();
									if (c.ConstantType != Runtime.VarType.Integer
										&& c.ConstantType != Runtime.VarType.Float)
									{
										Error(ErrorType.TrupleItemMustBeFloatOrInteger,
											item);
										hasUnre = true;
									}
								}
							}
							t.IsResolved = !hasUnre;
						}
						break;
					case Expression.ExpressionTypes.UnaryOperator:
						{
							var uo = expr.AsUnaryOperation();
							Scan(uo.Operand);
							uo.IsResolved = uo.Operand.IsResolved;
						}
						break;
				}

				if (OV_Lock && expr.IsResolved && expr.IsOperation())
				{
					expr.IsResolved = CheckOperation(expr);
					OV_Lock = false;
				}

				if (newLocalScope)
					CurrentModule.ExitScope();
			}

		}

		#region ReferenceSource

		//We omit function references because function references by themselves
		//contain enough information to deduce the module location.
		public enum ReferenceSourceType
		{
			Self,
			Argument,
			Local,
			Constant,
			Global,
			Function,
		}

		public abstract class ReferenceSource
		{
			public ReferenceSourceType SourceType { get; private set; }
			protected ReferenceSource(ReferenceSourceType type)
			{
				SourceType = type;
			}
			public ReferenceSourceArgument AsArgument() { return this as ReferenceSourceArgument; }
			public ReferenceSourceLocal AsLocal() { return this as ReferenceSourceLocal; }
			public ReferenceSourceConstant AsConstant() { return this as ReferenceSourceConstant; }
			public ReferenceSourceGlobal AsGlobal() { return this as ReferenceSourceGlobal; }
			public ReferenceSourceFunction AsFunction() { return this as ReferenceSourceFunction; }
		}

		public class ReferenceSourceSelf : ReferenceSource
		{
			public ReferenceSourceSelf() : base(ReferenceSourceType.Self) { }
		}

		public class ReferenceSourceArgument : ReferenceSource
		{
			public Param Argument;
			public Boolean IsReference;
			public ReferenceSourceArgument(Param argmument, Boolean isref)
				: base(ReferenceSourceType.Argument)
			{
				this.IsReference = isref;
				this.Argument = argmument;
			}
		}

		/// <summary>
		/// A representation of a used reference to a local variable
		/// </summary>
		public class ReferenceSourceLocal : ReferenceSource
		{
			/// <summary>
			/// Name of variable, not sure if it should be the exact casing or not.
			/// </summary>
			public String Source;
			/// <summary>
			/// Whether this variable is the site of initialization. (First time it's assigned)
			/// </summary>
			public Boolean IsVIS;
			/// <summary>
			/// The local frame it belongs to, not call frame.
			/// </summary>
			public LocalFrame TargetFrame;
			/// <summary>
			/// Whether the usage of this local is a reference
			/// </summary>
			public Boolean IsReference;
			/// <summary>
			/// Make sure localName is exactly as listed
			/// in the local frame dictionary's key.
			/// </summary>
			public ReferenceSourceLocal(String localName, Boolean isSiteOfInitialization, LocalFrame target, Boolean isRef)
				: base(ReferenceSourceType.Local)
			{
				this.IsVIS = isSiteOfInitialization;
				this.Source = localName;
				this.TargetFrame = target;
				this.IsReference = isRef;
			}
		}

		/// <summary>
		/// A representation of a used reference to a constant
		/// </summary>
		public class ReferenceSourceConstant : ReferenceSource
		{
			/// <summary>
			/// The module where the constant is declared
			/// </summary>
			public ContextModule Module;
			public String Name;
			public ReferenceSourceConstant(String name, ContextModule module)
				: base(ReferenceSourceType.Constant)
			{
				if (module == null)
					throw new Exception("Created constant reference source with null module.");
				this.Module = module;
				this.Name = name;
				if (!module.ConstMap.ContainsKey(Name))
					throw new Exception("Created reference source of type constant that doesn't exist.");
			}
		}

		/// <summary>
		/// Represents an instance of a reference to a global variable
		/// </summary>
		public class ReferenceSourceGlobal : ReferenceSource
		{
			public String Name;
			public ReferenceSourceGlobal(String name)
				: base(ReferenceSourceType.Global)
			{
				this.Name = name;
			}
		}

		/// <summary>
		/// Represents an instance of the usage/reference of a function
		/// </summary>
		public class ReferenceSourceFunction : ReferenceSource
		{
			public ContextModule Module;
			public FunctionDefinition Definition;
			public FunctionReference Reference;
			public FunctionCall CallSite;

			//TODO: Not sure if I should include the call too
			public ReferenceSourceFunction(ContextModule module, String name, FunctionReference reference, FunctionCall callsite = null)
				: base(ReferenceSourceType.Function)
			{
				if (module == null)
					throw new Exception("Attempted to create function reference source with null module.");
				this.Module = module;
				this.Definition = Module.FuncMap[name];
				this.Reference = reference;
				this.CallSite = callsite;
			}
		}

		#endregion
	}
}
