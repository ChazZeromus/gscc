using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace GameScriptCompiler
{
	namespace GSIL
	{
		public enum Instr
		{
			NoOp,
			LoadLocal, //Loads local variable from local variable stack to working stack
			LoadInt, //Loads integer into working stack
			LoadBool, //Loads boolean into working stack
			LoadFloat, //Loads float into working stack
			LoadData, //Loads data from current module resources into working stack
			LoadUndefined, //Loads undefined onto working stack
			LoadLocalReference, //Loads a reference from a local variable
			LoadArgReference, //Loads a reference from an argument
			LoadGlobalReference, //Loads a reference from a global variable
			LoadFunctionReference, //Loads a function reference from a function index
			LoadArray,//Loads an empty array
			StoreLocal,//Stores top variable of working stack onto designated local variable
			StoreSelf,//Stores top variable of working as self
			StoreArg, //Stores top variable of working stack onto argument
			LoadArg, //Loads argument onto  working stack
			//Stores an object referred to by the reference object located at their respective locations.
			StoreArgDereferenced,
			StoreLocalDereferenced,
			StoreGlobalDereferenced,
			LoadAccess,//Access object s[-1] with pure string reference s[-2], then loads value
			LoadAccessRef, //Same as Access but loads the reference instead of value
			StoreElement,//Sets a truple/array element using s[-3] as array, s[-2] as index and s[-1] as value then clears
			LoadElement,//Accesses an truple/array object via index/key with index/key as [-1] and array as [-2], pops both then loads value
			LoadElementRef, //Same as Get but loads the value object's reference instead
			Pop, //Removes top item from working stack
			Swap, //Swaps the first the second working stack objects
			Reserve, //Reserves n objects onto local variable store
			Truple, //Takes the top three objects and pop them then push a truple of those three objects
			//Performs arithmetic with the second to top object with top object then
			ArithAdd,
			ArithSub,
			ArithMul,
			ArithDiv,
			ArithMod,
			BitAnd,
			BitOr,
			BitXor,
			BitNegate,
			CompareEquals,
			CompareGreater,
			CompareLess,
			CompareGreaterEqual,
			CompareLessEqual,
			Jump,
			//These conditional jumps will check for Trueness on top of the stack and pop then jump if need be.
			JumpTrue,
			JumpFalse,
			//Calls always return onto the stack, if there is no return then undefined is returned
			Call,
			CallThread,
			CallNative,
			Return
		}

		public enum Encoding
		{
			None,
			Int32,
			Int16,
			Int8
		}

		public class OpcodeGen : IDisposable
		{
			private Stream Target;
			private Byte[] Buffer;
			public static Dictionary<Instr, String> Aliases;
			public static Dictionary<String, Instr> Mapping;
			public static Dictionary<Instr, Encoding> Encodings;
			public static Dictionary<Encoding, int> EncodingSize;

			public OpcodeGen(Stream stream)
			{
				this.Target = stream;
				Buffer = new Byte[5];
			}

			public void Write(Instr instr, Int32 data = 0)
			{
				this.Buffer[0] = (Byte)instr;

				using (var ms = new MemoryStream(this.Buffer, 1, 4))
					ms.Write(BitConverter.GetBytes(data), 0, 4);
				this.Target.Write(this.Buffer, 0, 1 + OpcodeGen.EncodingSize[OpcodeGen.Encodings[instr]]);
			}

			public static Instr? GetInstr(String name)
			{
				name = name.ToLower();
				return Mapping.ContainsKey(name) ? (Instr?)Mapping[name] : null;
			}

			public void Dispose() { }

			public static void InitAliases()
			{
				Aliases = new Dictionary<Instr, string>();
				Encodings = new Dictionary<Instr, Encoding>();
				EncodingSize = new Dictionary<Encoding, int>();
				Mapping = new Dictionary<string, Instr>();

				Aliases[Instr.LoadAccess] = "lac";
				Aliases[Instr.LoadAccessRef] = "lacr";
				Aliases[Instr.ArithAdd] = "add";
				Aliases[Instr.ArithDiv] = "div";
				Aliases[Instr.ArithMod] = "mod";
				Aliases[Instr.ArithMul] = "mul";
				Aliases[Instr.ArithSub] = "sub";
				Aliases[Instr.BitAnd] = "and";
				Aliases[Instr.BitNegate] = "neg";
				Aliases[Instr.BitOr] = "or";
				Aliases[Instr.BitXor] = "xor";
				Aliases[Instr.Call] = "call";
				Aliases[Instr.CallThread] = "tcall";
				Aliases[Instr.CallNative] = "ncall";
				Aliases[Instr.CompareEquals] = "eq";
				Aliases[Instr.CompareGreater] = "gt";
				Aliases[Instr.CompareLess] = "lt";
				Aliases[Instr.CompareGreaterEqual] = "gte";
				Aliases[Instr.CompareLessEqual] = "lte";
				Aliases[Instr.LoadElement] = "lde";
				Aliases[Instr.LoadElementRef] = "lder";
				Aliases[Instr.Jump] = "jmp";
				Aliases[Instr.JumpTrue] = "jt";
				Aliases[Instr.JumpFalse] = "jf";
				Aliases[Instr.LoadArg] = "ldarg";
				Aliases[Instr.LoadArgReference] = "ldargr";
				Aliases[Instr.LoadBool] = "ldcb";
				Aliases[Instr.LoadFloat] = "ldflt";
				Aliases[Instr.LoadFunctionReference] = "ldfun";
				Aliases[Instr.LoadGlobalReference] = "ldgr";
				Aliases[Instr.LoadInt] = "ldci";
				Aliases[Instr.LoadLocal] = "ldloc";
				Aliases[Instr.LoadLocalReference] = "ldlocr";
				Aliases[Instr.LoadData] = "lddat";
				Aliases[Instr.LoadUndefined] = "ldund";
				Aliases[Instr.Pop] = "pop";
				Aliases[Instr.Reserve] = "resrv";
				Aliases[Instr.StoreElement] = "ste";
				Aliases[Instr.StoreArg] = "starg";
				Aliases[Instr.StoreLocal] = "stloc";
				Aliases[Instr.StoreSelf] = "stself";
				Aliases[Instr.StoreArgDereferenced] = "stargd";
				Aliases[Instr.StoreGlobalDereferenced] = "stgd";
				Aliases[Instr.StoreLocalDereferenced] = "stlocd";
				Aliases[Instr.Truple] = "tru";
				Aliases[Instr.Swap] = "swp";
				Aliases[Instr.LoadArray] = "array";
				Aliases[Instr.Return] = "ret";
				Aliases[Instr.NoOp] = "nop";

				foreach (Instr i in Enum.GetValues(typeof(Instr)))
					Mapping[Aliases[i]] = i;


				Encodings[Instr.LoadArray] = Encoding.None;
				Encodings[Instr.LoadAccess] = Encoding.None;
				Encodings[Instr.LoadAccessRef] = Encoding.None;
				Encodings[Instr.ArithAdd] = Encoding.None;
				Encodings[Instr.ArithDiv] = Encoding.None;
				Encodings[Instr.ArithMod] = Encoding.None;
				Encodings[Instr.ArithMul] = Encoding.None;
				Encodings[Instr.ArithSub] = Encoding.None;
				Encodings[Instr.BitAnd] = Encoding.None;
				Encodings[Instr.BitNegate] = Encoding.None;
				Encodings[Instr.BitOr] = Encoding.None;
				Encodings[Instr.BitXor] = Encoding.None;
				Encodings[Instr.Call] = Encoding.Int32;
				Encodings[Instr.CallThread] = Encoding.Int32;
				Encodings[Instr.CallNative] = Encoding.Int32;
				Encodings[Instr.CompareEquals] = Encoding.None;
				Encodings[Instr.CompareGreater] = Encoding.None;
				Encodings[Instr.CompareGreaterEqual] = Encoding.None;
				Encodings[Instr.CompareLess] = Encoding.None;
				Encodings[Instr.CompareLessEqual] = Encoding.None;
				Encodings[Instr.LoadElement] = Encoding.None;
				Encodings[Instr.LoadElementRef] = Encoding.None;
				Encodings[Instr.Jump] = Encoding.Int32;
				Encodings[Instr.JumpFalse] = Encoding.Int32;
				Encodings[Instr.JumpTrue] = Encoding.Int32;
				Encodings[Instr.LoadArg] = Encoding.Int8;
				Encodings[Instr.LoadArgReference] = Encoding.Int8;
				Encodings[Instr.LoadArray] = Encoding.Int16;
				Encodings[Instr.LoadBool] = Encoding.Int8;
				Encodings[Instr.LoadFloat] = Encoding.Int32;
				Encodings[Instr.LoadFunctionReference] = Encoding.Int32;
				Encodings[Instr.LoadGlobalReference] = Encoding.Int32;
				Encodings[Instr.LoadInt] = Encoding.Int32;
				Encodings[Instr.LoadLocal] = Encoding.Int16;
				Encodings[Instr.LoadLocalReference] = Encoding.Int16;
				Encodings[Instr.LoadData] = Encoding.Int32;
				Encodings[Instr.LoadUndefined] = Encoding.None;
				Encodings[Instr.NoOp] = Encoding.None;
				Encodings[Instr.Pop] = Encoding.None;
				Encodings[Instr.Reserve] = Encoding.Int16;
				Encodings[Instr.StoreArg] = Encoding.Int8;
				Encodings[Instr.StoreLocal] = Encoding.Int16;
				Encodings[Instr.StoreSelf] = Encoding.None;
				Encodings[Instr.StoreElement] = Encoding.None;
				Encodings[Instr.Swap] = Encoding.None;
				Encodings[Instr.Truple] = Encoding.None;
				Encodings[Instr.Return] = Encoding.None;
				Encodings[Instr.NoOp] = Encoding.None;
				Encodings[Instr.StoreArgDereferenced] = Encoding.None;
				Encodings[Instr.StoreGlobalDereferenced] = Encoding.None;
				Encodings[Instr.StoreLocalDereferenced] = Encoding.None;
				Encodings[Instr.Truple] = Encoding.None;

				foreach (Instr i in Enum.GetValues(typeof(Instr)))
				{
					var enc = Encodings[i];
				}

				EncodingSize[Encoding.None] = 0;
				EncodingSize[Encoding.Int8] = 1;
				EncodingSize[Encoding.Int32] = 4;
				EncodingSize[Encoding.Int16] = 2;
			}
		}

		public class Op
		{
			Int32[] operand;
			static Byte[] Buffer = new Byte[4];
			public Op(GSIL.Instr instr, params Int32[] operand)
			{
				this.operand = operand;
			}

			public static Op CreateOp(GSIL.Instr instr, params Int32[] data)
			{
				return new Op(instr, data);
			}

			public static Op Read(Byte[] bytes)
			{
				if (bytes.Length < 1)
					throw new Exception("Cannot read empty bytes");
				Instr instr = (Instr)bytes[0];
				int length;
				if (!OpcodeGen.Encodings.ContainsKey(instr))
					throw new Exception("Invalid opcode {X:0}".Fmt(instr));
				length = OpcodeGen.EncodingSize[OpcodeGen.Encodings[instr]];
				if (bytes.Length - 1 < length)
					throw new Exception("Opcode data encoding is smaller than given.");
				Array.Copy(bytes, 1, Buffer, 0, length);
				for (int i = length; i < Buffer.Length; ++i)
					Buffer[i] = 0;
				return Op.CreateOp(instr, BitConverter.ToInt32(Buffer, 0));
			}
		}
	}
}
