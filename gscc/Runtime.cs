using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameScriptCompiler
{
	namespace Runtime
	{
		public enum VarType
		{
			Undefined,
			DirectiveVar,
			ResourceVar,
			MetaString,
			String,
			Integer,
			Float,
			Bool,
			FuncReference,
			Object,
			Reference,
			Array,
			Truple
		}

		public struct OpInst
		{
			GSIL.Instr Instr;
			Object Operand;
		}

		public struct RTInstrPointer
		{
			int Function, Offset;
		}

		public class RTObject
		{
			public VarType Type;
			public Object Value;
			public RTObject(VarType type, Object val)
			{
				this.Type = type;
				this.Value = val;
			}
			public RTObject(RTObject rtobj)
			{
				this.Type = rtobj.Type;
				this.Value = rtobj.Value;
			}

			public virtual bool op_add(RTObject obj) { return false; }
			public virtual bool op_sub(RTObject obj) { return false; }
			public virtual bool op_mul(RTObject obj) { return false; }
			public virtual bool op_div(RTObject obj) { return false; }
			public virtual bool op_mod(RTObject obj) { return false; }
			public virtual bool op_xor(RTObject obj) { return false; }
			public virtual bool op_and(RTObject obj) { return false; }
			public virtual bool op_neg(RTObject obj) { return false; }
			public virtual bool op_inv(RTObject obj) { return false; }
			public virtual bool op_true(RTObject obj) { return false; }
			public virtual bool op_cgt(RTObject obj) { return false; }
			public virtual bool op_clt(RTObject obj) { return false; }
			public virtual bool op_ceq(RTObject obj) { return false; }
		}

		public class RTUndefined : RTObject
		{
			public RTUndefined()
				: base(VarType.Undefined, null) { }
		}

		public class RTString : RTObject
		{
			public RTString(String str)
				: base(VarType.String, str) { }

		}

		public class RTFuncReference : RTObject
		{
			public RTFuncReference(int index)
				: base(VarType.FuncReference, index) { }
		}

		public class RTReference : RTObject
		{
			public RTReference(RTObject referral)
				: base(VarType.Reference, referral) { }
		}

		public class RTInt : RTObject
		{
			public RTInt(int number)
				: base(VarType.Integer, number) { }
		}

		public class RTFloat : RTObject
		{
			public RTFloat(float number)
				: base(VarType.Float, number) { }
		}

		public class FunctionEntry
		{
			public String Name;
			public Byte[] Bytes;
		}


	}
}
