using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Reflection;
using GameScriptCompiler.ContextAnalyzation;
using GameScriptCompiler.CodeDOM;
using GameScriptCompiler.Text;

namespace GameScriptCompiler
{
	#region Extensions
	public static partial class Extensions
	{
		public static InlineEvaluator.DataType GetIEDataType(this Expression expr)
		{
			if (expr.IsConstant())
			{
				var c = expr.AsConstant();
				switch (c.ConstantType)
				{
					case Runtime.VarType.Integer:
						return InlineEvaluator.DataType.Integer;
					case Runtime.VarType.Float:
						return InlineEvaluator.DataType.Float;
					case Runtime.VarType.String:
						return InlineEvaluator.DataType.String;
					case Runtime.VarType.Bool:
						return InlineEvaluator.DataType.Boolean;
				}
				return InlineEvaluator.DataType.Invalid;
			}
			return expr.GetExpressionType() == Expression.ExpressionTypes.Truple
				? InlineEvaluator.DataType.Truple : InlineEvaluator.DataType.Invalid;
		}
	}
	#endregion

	internal class SimpleFieldToPropDescriptor : PropertyDescriptor
	{
		FieldInfo Field;
		public SimpleFieldToPropDescriptor(FieldInfo fieldinfo, String name, Attribute[] attrs)
			: base(name, attrs)
		{
			Field = fieldinfo;
		}

		public override bool CanResetValue(object component)
		{
			return false;
		}

		public override Type ComponentType
		{
			get { return typeof(InlineEvaluator.EvaluationOptions); }
		}

		public override object GetValue(object component)
		{
			return Field.GetValue(component);
		}

		public override bool IsReadOnly
		{
			get { return false; }
		}

		public override Type PropertyType
		{
			get { return Field.FieldType; }
		}

		public override void ResetValue(object component)
		{
			throw new NotImplementedException();
		}

		public override void SetValue(object component, object value)
		{
			Field.SetValue(component, value);
		}

		public override bool ShouldSerializeValue(object component)
		{
			return true;
		}
	}

	internal class NameAttribute : Attribute
	{
		public String Name;
		public NameAttribute(String name)
		{
			Name = name;
		}
	}

	internal class IEOptionFieldToPropTypeConverter : TypeConverter
	{
		PropertyDescriptorCollection PDLs = null;
		public IEOptionFieldToPropTypeConverter()
		{
			if (PDLs != null)
				return;
			PDLs = new PropertyDescriptorCollection(new PropertyDescriptor[] { });
			foreach (var field in typeof(InlineEvaluator.EvaluationOptions).GetFields())
			{
				List<Attribute> attrs = new List<Attribute>();
				foreach (var a in field.GetCustomAttributes(typeof(Attribute), true))
				{
					if (a is NameAttribute)
						attrs.Add(new DisplayNameAttribute((a as NameAttribute).Name));
					else
						attrs.Add(a as Attribute);
				}

				PDLs.Add(new SimpleFieldToPropDescriptor(field, field.Name, attrs.ToArray()));
			}
		}

		public override bool GetPropertiesSupported(ITypeDescriptorContext context)
		{
			return context.Instance != null;
		}

		public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
		{
			return PDLs;
		}
	}

	/// <summary>
	/// Inline evaluators resolve expression graphs that consist of constants
	/// and defined constants.
	/// </summary>
	public class InlineEvaluator
	{
		delegate void UnaryOp();
		delegate void BinaryOp(Expression OpB);
		delegate Boolean EqualOp(Expression OpB);
		delegate void Process(Expression expr);
		Dictionary<Expression.ExpressionTypes, Process> ExprTypeMap;
		Dictionary<DataType, DataTypeOperations> DataTypeMap;
		Dictionary<EvaluationOptions.NamedConstantModeType, Action<VariableReference>> ConstDeclModeMap;
		CompilerOptions Options;
		IEnumerable<DeclaredConstant> ConstDeclLookup;

		public enum DataType
		{
			Integer,
			Float,
			String,
			Truple,
			Boolean,
			Invalid
		}

		[TypeConverter(typeof(IEOptionFieldToPropTypeConverter))]
		public class EvaluationOptions
		{
			public enum NamedConstantModeType
			{
				All,
				ResolvedSourceReference
			}
			[Description("Allows sequential evaluation of inline arrays.")]
			[Name("Allow Inline Arrays")]
			public Boolean Allow_InlineArrays = false;
			[Description("Whether to automatically convert quotient results to integers when there is no fraction part."
				+ " This will prevent type mismatch errors when performing operations that rely on integral operands in"
				+ " which an operand is a division operation.")]
			[Name("Enable Auto-Convert Quotient to Integral.")]
			public Boolean Enable_AutoConvertQuotientToInteger = false;
			[Description("Whether to support logical AND, OR and NOT operators, by specification these are not supported"
				+ "in inline evaluation used in COD's GSC compiler to detect code paths.")]
			[Name("Enable Logical-AND/OR/NOT Operators")]
			public Boolean Enable_Logical = false;
			[Description("How to treat variable references."
				+ "A lookup mode of All will treat all variable references as constant names if supplied a ConstDeclLookup, returning errors "
				+ "if a variable reference is not listed as a constant. A lookup mode of "
				+ "ResolvedSourceReference will treat variable references with a source reference of type "
				+ "Constant as a constant name, whilst returning RuntimeRequired for all other reference types.")]
			[Name("Named Constant Mode Type")]
			public NamedConstantModeType Mode_DeclConstantLookupType = NamedConstantModeType.All;
			
			[Description("Whether to pass RRs as successes on OVs. This will " +
				"allow passiveness on non-constant types to perform OVs on entire operation graphs.")]
			[Name("[Internal] Enable Ignoring RuntimeRequires")]
			internal Boolean Allow_OperationTypeRuntimeRequires = false;
		}

		//TODO: Instead of keeping an Expression object
		//as the first operand, each implementation of
		//the DataTypeOperations class can hold their own
		//'a' operand that corresponds directly with
		//their data type, instead of having to constantly
		//cast the first operand over and over again.

		private abstract class DataTypeOperations
		{
			protected InlineEvaluator IE;
			public Dictionary<DataType, Dictionary<BinaryOperationType, BinaryOp>> BiOpMap;
			public Dictionary<UnaryOperationType, UnaryOp> UnOpMap;
			public Dictionary<DataType, EqualOp> EqOpMap;

			public Stack<Expression> OpAStack;
			protected Expression OpA_Expr = null;

			protected DocLocation OpLoc
			{
				get
				{
					return OpA_Expr.Location;
				}
			}
			//Create our own enum that doesn't overlap
			public enum DataTypeBiOpType
			{
				/*BoolAnd,//a && b
				BoolOr,//a || b
				BoolEquals,//a == b
				BoolNotEquals,//a != b*/
				BoolLess = BinaryOperationType.BoolLess,//a < b
				BoolGreater,//a > b
				/*BoolLessEquals,//a <= b
				BoolGreaterEquals,//a >= a */
				//Assign = BinaryOperationType.Assign,//TODO: Inline eval B and success it.
				Addition = BinaryOperationType.Addition, //a + b
				Subtraction,//a - b
				Division,//a / b
				Multiplication,//a * b
				BitAND,//a & b
				BitOR,//a | b
				BitXOR,//a ^ b
				BitShiftRight, // a >> b
				BitShiftLeft, // a << b
				Modulo,//a % b
			}

			public enum DataTypeUnOpType
			{
				Positive = UnaryOperationType.Positive,//+a
				Negative,//-b
				Invert,//~
				//Not,//!b
				PostIncrement = UnaryOperationType.PostIncrement,
				PostDecrement
			}

			public DataTypeOperations(InlineEvaluator ie)
			{
				OpAStack = new Stack<Expression>();
				IE = ie;
				//TODO: Point boolean-wise types to IEs methods instead.
				//The operations for OR and AND are homogeneous.
				//[fixed: actually we can have them here, AND and OR work regardless of type, just trueness.
				BiOpMap = new Dictionary<DataType, Dictionary<BinaryOperationType, BinaryOp>>();
				UnOpMap = new Dictionary<UnaryOperationType, UnaryOp>();
				EqOpMap = new Dictionary<DataType, EqualOp>();


				if (!IE.EvalOptions.Enable_Logical)
				{
					RegisterBiOpMapAllTypes(BinaryOperationType.BoolAnd, this.bi_runtime_fail);
					RegisterBiOpMapAllTypes(BinaryOperationType.BoolOr, this.bi_runtime_fail);
					RegisterUnOpMap(UnaryOperationType.Not, this.un_runtime_fail);
				}
				else
				{
					RegisterBiOpMapAllTypes(BinaryOperationType.BoolAnd, this.bi_and);
					RegisterBiOpMapAllTypes(BinaryOperationType.BoolOr, this.bi_or);
					RegisterUnOpMap(UnaryOperationType.Not, this.un_not);
				}

				RegisterBiOpMapAllTypes(BinaryOperationType.BoolNotEquals, this.bi_not_equal);
				RegisterBiOpMapAllTypes(BinaryOperationType.BoolEquals, this.bi_equal);
				RegisterBiOpMapAllTypes(BinaryOperationType.BoolGreaterEquals, this.bi_gte);
				RegisterBiOpMapAllTypes(BinaryOperationType.BoolLessEquals, this.bi_lte);
			}

			private void bi_runtime_fail(Expression b)
			{
				IE.Fail_RequiresRuntime(OpA_Expr);
			}

			private void un_runtime_fail()
			{
				IE.Fail_RequiresRuntime(OpA_Expr);
			}

			private void RegisterBiOpMap(DataType datatype, BinaryOperationType operation, BinaryOp targetMethod)
			{
				if (!BiOpMap.ContainsKey(datatype))
					BiOpMap.Add(datatype, new Dictionary<BinaryOperationType, BinaryOp>());
				BiOpMap[datatype][operation] = targetMethod;
			}

			private void RegisterBiOpMapAllTypes(BinaryOperationType operation, BinaryOp targetMethod)
			{
				foreach (DataType dt in Enum.GetValues(typeof(DataType)))
					RegisterBiOpMap(dt, operation, targetMethod);
			}

			protected void RegisterBiOpMap(DataType datatype, DataTypeBiOpType operation, BinaryOp targetMethod)
			{
				RegisterBiOpMap(datatype, (BinaryOperationType)operation, targetMethod);
			}

			private void RegisterUnOpMap(UnaryOperationType operation, UnaryOp targetMethod)
			{
				UnOpMap[operation] = targetMethod;
			}

			protected void RegisterUnOpMap(DataTypeUnOpType operation, UnaryOp targetMethod)
			{
				RegisterUnOpMap((UnaryOperationType)operation, targetMethod);
			}

			protected void RegisterEqMap(DataType datatype, EqualOp targetMethod)
			{
				EqOpMap[datatype] = targetMethod;
			}

			public virtual void BinaryCall(BinaryOperationType type, Expression operandA, Expression operandB)
			{
				DataType dt = operandB.GetIEDataType();
				if (BiOpMap.ContainsKey(dt) &&
					BiOpMap[dt].ContainsKey(type))
				{
					Push(operandA);
					BiOpMap[dt][type](operandB);
					Pop();
				}
				else
					IE.Fail_InvalidOperation_Binary(operandB, operandA.GetIEDataType(), operandB.GetIEDataType(), type);
			}

			public virtual void UnaryCall(UnaryOperationType type, Expression operandA)
			{
				if (UnOpMap.ContainsKey(type))
				{
					Push(operandA);
					UnOpMap[type]();
					Pop();
				}
				else
					IE.Fail_InvalidOperation_Unary(operandA, operandA.GetIEDataType(), type);
			}

			private void Push(Expression newOpA)
			{
				if (OpA_Expr != null)
					OpAStack.Push(newOpA);
				OpA_Expr = newOpA;
			}

			private void Pop()
			{
				if (OpAStack.Count > 0)
					OpA_Expr = OpAStack.Pop();
			}

			void un_not()
			{
				IE.Success(IE.CreateBool(!this.is_true(), OpA_Expr.Location));
			}

			
			void bi_or(Expression b)
			{
				var dt = b.GetIEDataType();
				IE.Success(IE.CreateBool(this.is_true() || IE.DataTypeMap[dt].is_true(b), OpA_Expr.Location));
			}

			void bi_and(Expression b)
			{
				var dt = b.GetIEDataType();
				IE.Success(IE.CreateBool(this.is_true() && IE.DataTypeMap[dt].is_true(b), OpA_Expr.Location));
			}

			public void bi_equal(Expression b)
			{
				var dt = b.GetIEDataType();
				if (!EqOpMap.ContainsKey(dt))
					IE.Fail_InvalidOperation_Binary(OpA_Expr, OpA_Expr.GetIEDataType(), dt, BinaryOperationType.Assign);
				else
					IE.Success(IE.CreateBool(EqOpMap[dt](b), OpA_Expr.Location));
			}

			void bi_not_equal(Expression b)
			{
				var dt = b.GetIEDataType();
				if (!EqOpMap.ContainsKey(dt))
					IE.Fail_InvalidOperation_Binary(OpA_Expr, OpA_Expr.GetIEDataType(), dt, BinaryOperationType.BoolNotEquals);
				else
					IE.Success(IE.CreateBool(!EqOpMap[dt](b), OpA_Expr.Location));
			}

			//TODO: Clean this shit up, this is an embarassment. Why not make a fucking EqOp for this instead?
			void bi_xte(Expression b, BinaryOperationType relat)
			{
				var dt = b.GetIEDataType();
				//It must support '>' for this dt
				if (!BiOpMap[dt].ContainsKey(relat))
					IE.Fail_InvalidOperation_Binary(OpA_Expr, OpA_Expr.GetIEDataType(), dt, relat);
				else
				{
					BiOpMap[dt][relat](b);
					if (IE.Successfull() && !(bool)IE.Result.Result.AsConstant().InternalValue)
						bi_equal(b);
				}
			}

			void bi_gte(Expression b)
			{
				bi_xte(b, BinaryOperationType.BoolGreater);
			}

			void bi_lte(Expression b)
			{
				bi_xte(b, BinaryOperationType.BoolLess);
			}

			protected abstract Boolean is_true();

			public Boolean is_true(Expression a)
			{
				Boolean result;
				Push(a);
				result = is_true();
				Pop();
				return result;
			}

			protected void SelfSuccess()
			{
				IE.Success(OpA_Expr);
			}
		}

		EvaluationOptions EvalOptions;


		public InlineEvaluator(CompilerOptions options, EvaluationOptions evalOptions, IEnumerable<DeclaredConstant> lookup = null)
		{
			ConstDeclLookup = lookup;
			EvalOptions = evalOptions;
			Options = options;
			Result = new EvaluationInfo();
			Result.Success(null);

			ExprTypeMap = new Dictionary<Expression.ExpressionTypes, Process>();
			ExprTypeMap[Expression.ExpressionTypes.BinaryOperator] = this.OnBinaryOperator;
			ExprTypeMap[Expression.ExpressionTypes.Constant] = this.OnConstant;
			ExprTypeMap[Expression.ExpressionTypes.Group] = this.OnGroup;
			ExprTypeMap[Expression.ExpressionTypes.InlineArray] = this.OnInlineArray;
			ExprTypeMap[Expression.ExpressionTypes.Reference] = this.OnReference;
			ExprTypeMap[Expression.ExpressionTypes.Statement] = this.OnStatement;
			ExprTypeMap[Expression.ExpressionTypes.Truple] = this.OnTruple;
			ExprTypeMap[Expression.ExpressionTypes.UnaryOperator] = this.OnUnaryOperator;

			ConstDeclModeMap = new Dictionary<EvaluationOptions.NamedConstantModeType, Action<VariableReference>>();
			ConstDeclModeMap[EvaluationOptions.NamedConstantModeType.All] = this.OnNamedConst_All;
			ConstDeclModeMap[EvaluationOptions.NamedConstantModeType.ResolvedSourceReference] = this.OnNamedConst_SourceRef;

			DataTypeMap = new Dictionary<DataType, DataTypeOperations>();
			InitDataTypes();
		}

		/// <summary>
		/// Setting a constant-declaration lookup will case IE treat all variable
		/// references as constant names. Any unknown constants will return
		/// a related error. Operation without a constant-lookup will treat
		/// cause all variable references to return a RequireRuntimeError.
		/// </summary>
		/// <param name="constants"></param>
		public void SetConstDeclLookup(IEnumerable<DeclaredConstant> constants)
		{
			ConstDeclLookup = constants;
		}

		public EvaluationInfo Result;
		public enum EvaluationResult
		{
			Success,
			InvalidOperation,
			RequiresRuntimeEvaluation,
			DivisionByZero,
			/// <summary>
			/// Leaves unknown constant as VR in ErrorExpr
			/// </summary>
			ConstantDoesNotExist
		}

		public enum InvalidErrorType
		{
			IncompatibleTypesForBinaryOperation,
			IncompatibleTypeForUnaryOperation
		}

		public class InvalidMsgInfo
		{
			public Object[] Exprs;
			public InvalidErrorType MsgType;
			public InvalidMsgInfo(InvalidErrorType msg, params Object[] exprs)
			{
				Exprs = exprs;
				MsgType = msg;
			}

			public override string ToString()
			{
				List<String> strs = new List<string>();
				foreach (var obj in Exprs)
					if (obj is Expression)
						strs.Add((obj as Expression).Express(0));
					else
						strs.Add(obj.ToString());
				return StringMap.InvalidOpMsgs[MsgType].Fmt(strs.ToArray());
			}
		}

		public class EvaluationInfo
		{
			public Expression ErrorExpr, Result;
			public EvaluationResult ResultType;
			public InvalidMsgInfo InvalidMsg;
			public void Success(Expression result)
			{
				InvalidMsg = null;
				ErrorExpr = null;
				Result = result;
				ResultType = EvaluationResult.Success;
			}

			public void Fail(EvaluationResult type, Expression error)
			{
				InvalidMsg = null;
				Result = null;
				ErrorExpr = error;
				ResultType = type;
			}
		}

		private Boolean Successfull()
		{
			return Result.ResultType == EvaluationResult.Success;
		}

		private void Success(Expression result)
		{
			Result.Success(result);
		}

		private void Undefined()
		{
			Success(Constant.Undefined);
		}

		private void Fail_RequiresRuntime(Expression target)
		{
			Result.Fail(EvaluationResult.RequiresRuntimeEvaluation, target);
		}

		private void Fail_ZeroDivide(Expression target)
		{
			Result.Fail(EvaluationResult.DivisionByZero, target);
		}

		private void Fail_InvalidOperation(Expression target, InvalidErrorType type, params object[] args)
		{
			Result.Fail(EvaluationResult.InvalidOperation, target);
			Result.InvalidMsg = new InvalidMsgInfo(type, args);
		}

		private void Fail_InvalidOperation_Binary(Expression target, DataType a, DataType b, BinaryOperationType type)
		{
			Fail_InvalidOperation(target, InvalidErrorType.IncompatibleTypesForBinaryOperation, a, b, StringMap.OpKeys[type]);
		}

		private void Fail_InvalidOperation_Unary(Expression target, DataType a, UnaryOperationType type)
		{
			Fail_InvalidOperation(target, InvalidErrorType.IncompatibleTypeForUnaryOperation, a, StringMap.UnaryOpKeys[type]);
		}

		public void Evaluate(Expression expr)
		{
			ExprTypeMap[expr.GetExpressionType()](expr);
		}

		#region On Methods

		private void OnBinaryOperator(Expression expr)
		{
			BinaryOperation bo = expr.AsBinaryOperation();
			Expression a, b;
			Evaluate(bo.Left);
			if (!Successfull())
				return;
			a = Result.Result;
			Evaluate(bo.Right);
			if (!Successfull())
				return;
			b = Result.Result;
			var dt = a.GetIEDataType();

			if (DataTypeMap.ContainsKey(dt))
				DataTypeMap[dt].BinaryCall(bo.OperationType, a, b);
			else
				Fail_InvalidOperation_Binary(a, a.GetIEDataType(), b.GetIEDataType(), bo.OperationType);
			if (Successfull())
				Evaluate(Result.Result);
		}

		private void OnConstant(Expression expr)
		{
			var c = expr.AsConstant();
			if (c.GetIEDataType() == DataType.Boolean)
				Success(CreateInteger((bool)c.InternalValue ? 1 : 0, c.Location));
			else
				Success(expr);
		}

		private void OnGroup(Expression expr)
		{
			Evaluate(expr.AsGroup().Value);
		}

		private void OnInlineArray(Expression expr)
		{
			if (!EvalOptions.Allow_InlineArrays)
			{
				Fail_RequiresRuntime(expr);
				return;
			}
			var ia = expr.AsInlineArray();
			List<Expression> newItems = new List<Expression>();
			foreach (var item in ia.Items)
			{
				Evaluate(item);
				if (!Successfull())
					return;
				newItems.Add(Result.Result);
			}
			Success(new InlineArray() { Items = newItems });
		}

		private void OnReference(Expression expr)
		{
			var r = expr.AsReference();
			var rt = r.GetReferenceType();
			if (rt == Reference.ReferenceTypes.Variable)
				ConstDeclModeMap[EvalOptions.Mode_DeclConstantLookupType](r.AsVariableReference());
			else
				Fail_RequiresRuntime(expr);
		}

		private void OnNamedConst_All(VariableReference vr)
		{
			if (ConstDeclLookup != null)
			{
				var dc = ConstDeclLookup.LookUpConstant(vr.Name);
				if (dc != null)
					Evaluate(dc.Value);
				else
					Result.Fail(EvaluationResult.ConstantDoesNotExist, vr);
			}
			else
				Fail_RequiresRuntime(vr);
		}

		private void OnNamedConst_SourceRef(VariableReference vr)
		{
			if (vr.Source != null && vr.Source.SourceType == ReferenceSourceType.Constant)
			{
				var cr = vr.Source.AsConstant();
				Evaluate(cr.Module.ConstMap[cr.Name].Value);
			}
			else
				Fail_RequiresRuntime(vr);
		}

		private void OnStatement(Expression expr)
		{
			Fail_RequiresRuntime(expr);
		}

		private void OnTruple(Expression expr)
		{
			var t = expr.AsTruple();
			for (int i = 0; i < t.Items.Count; ++i )
			{
				var item = t.Items[i];
				this.Evaluate(item);
				if (!Successfull())
					return;
				t.Items[i] = Result.Result;
			}
			this.Success(t);
		}

		private void OnUnaryOperator(Expression expr)
		{
			var u = expr.AsUnaryOperation();
			Evaluate(u.Operand);
			if (!Successfull())
				return;
			var op = Result.Result;
			var dt = op.GetIEDataType();
			if (!DataTypeMap.ContainsKey(dt))
			{
				if (op.IsConstant())
					Fail_RequiresRuntime(expr);
				else //If it's a constant and not supported then we go nuts.
					Fail_InvalidOperation_Unary(expr, dt, u.OperationType);
				return;
			}
			DataTypeMap[dt].UnaryCall(u.OperationType, op);
			if (Successfull())
				Evaluate(Result.Result);
		}

		#endregion

		#region Create and Copy

		private Constant CreateCopy(Constant src, Object internalValue)
		{
			return new Constant(src.ConstantType, src.Value)
			{
				ID = src.ID,
				InternalValue = internalValue,
				Location = src.Location,
				IsResolved = src.IsResolved
			};
		}

		private Constant CreateBool(Boolean value, Text.DocLocation location)
		{
			var c = new Constant(Runtime.VarType.Bool,
				StringMap.NamedConstantKeys[value ? NamedConstantTypes.True : NamedConstantTypes.False])
				{
					Location = location
				};
			Constant.EvaluateContentWarnings notused;
			c.EvaluateContent(this.Options, out notused);
			return c;
		}

		private Constant CreateInteger(int integer, Text.DocLocation location)
		{
			return new Constant(Runtime.VarType.Integer,
				integer.ToString()) { InternalValue = integer, Location = location };
		}

		private Constant CreateFloat(float flt, Text.DocLocation location)
		{
			return new Constant(Runtime.VarType.Float,
				flt.ToString()) { InternalValue = flt, Location = location };
		}

		private Constant CastFloat(Constant intExpr)
		{
			var c = intExpr.AsConstant().ConstantType;
			if (c == Runtime.VarType.Integer)
				return CreateFloat((float)(int)intExpr.InternalValue, intExpr.Location);
			if (c == Runtime.VarType.Float)
				return intExpr;
			return null;
		}

		/// <summary>
		/// Must be content evaluated first.
		/// </summary>
		/// <param name="src">String constant to create a copy of.</param>
		/// <returns>New copy</returns>
		private Constant CreateStringCopy(Constant src)
		{
			var c = CreateCopy(src, null);
			c.ConstantType = Runtime.VarType.String;
			c.Value = src.InternalValue.ToString();
			return c;
		}

		private Constant CreateString(String str, DocLocation location)
		{
			return new Constant(Runtime.VarType.String, str) { Location = location };
		}

		private Truple CreateTruple(DocLocation location, Expression item)
		{

			var t = new Truple() { Location = location };
			for (int i = 0; i < 3; i++)
				t.Items.Add(item);
			return t;
		}

		private Truple CreateTruple(DocLocation location, params Expression[] items)
		{
			var t = new Truple() { Location = location };
			if (items.Length != 3)
				throw new Exception("Not 3 items!");
			foreach (var item in items)
				t.Add(item);
			return t;
		}

		#endregion


		#region DataTypes

		private void InitDataTypes()
		{
			DataTypeMap[DataType.Integer] = new IntegerDataType(this);
			DataTypeMap[DataType.Float] = new FloatDataType(this);
			DataTypeMap[DataType.String] = new StringDataType(this);
			DataTypeMap[DataType.Truple] = new TrupleDataType(this);
		}

		#region Integer
		private class IntegerDataType : DataTypeOperations
		{
			public IntegerDataType(InlineEvaluator ie)
				: base(ie)
			{
				this.RegisterUnOpMap(DataTypeUnOpType.Invert,	this.un_inv);
				this.RegisterUnOpMap(DataTypeUnOpType.Negative, this.un_neg);
				this.RegisterUnOpMap(DataTypeUnOpType.Positive, this.un_pos);

				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.Addition,		this.bi_int_add);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.BitAND,			this.bi_int_bit_and);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.BitOR,			this.bi_int_bit_or);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.BitShiftLeft,	this.bi_int_bit_shl);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.BitShiftRight,	this.bi_int_bit_shr);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.BitXOR,			this.bi_int_bit_xor);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.BoolGreater,	this.bi_int_bool_gt);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.BoolLess,		this.bi_int_bool_lt);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.Division,		this.bi_int_div);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.Modulo,			this.bi_int_mod);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.Multiplication,	this.bi_int_mul);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.Subtraction,	this.bi_int_sub);

				this.RegisterBiOpMap(DataType.Float, DataTypeBiOpType.Addition,			this.bi_float_add);
				this.RegisterBiOpMap(DataType.Float, DataTypeBiOpType.BoolGreater,		this.bi_float_bool_gt);
				this.RegisterBiOpMap(DataType.Float, DataTypeBiOpType.BoolLess,			this.bi_float_bool_lt);
				this.RegisterBiOpMap(DataType.Float, DataTypeBiOpType.Division,			this.bi_float_div);
				this.RegisterBiOpMap(DataType.Float, DataTypeBiOpType.Modulo,			this.bi_float_mod);
				this.RegisterBiOpMap(DataType.Float, DataTypeBiOpType.Multiplication,	this.bi_float_mul);
				this.RegisterBiOpMap(DataType.Float, DataTypeBiOpType.Subtraction,		this.bi_float_sub);

				this.RegisterBiOpMap(DataType.String, DataTypeBiOpType.Addition,		bi_str_add);

				this.RegisterBiOpMap(DataType.Truple, DataTypeBiOpType.Multiplication,	this.bi_truple_mul);
				this.RegisterBiOpMap(DataType.Truple, DataTypeBiOpType.Division,		this.bi_truple_div);

				this.RegisterEqMap(DataType.Float, this.eq_float);
				this.RegisterEqMap(DataType.Integer, this.eq_int);
				this.RegisterEqMap(DataType.Truple, this.eq_truple);
			}

			Constant OpA
			{
				get
				{
					return OpA_Expr.AsConstant();
				}
			}

			int OpInt
			{
				get
				{
					return (int)OpA.InternalValue;
				}
			}

			void SuccessInt(int i)
			{
				IE.Success(IE.CreateInteger(i, OpLoc));
			}

			void SuccessFloat(float f)
			{
				IE.Success(IE.CreateFloat(f, OpLoc));
			}

			#region Unaries

			void un_inv()
			{
				SuccessInt(~OpInt);
			}

			void un_neg()
			{
				SuccessInt(-OpInt);
			}

			void un_pos()
			{
				this.SelfSuccess();
			}

			#endregion

			#region Integer Arith

			void bi_int_add(Expression b)
			{
				SuccessInt(OpInt + (int)b.AsConstant().InternalValue);
			}

			void bi_int_sub(Expression b)
			{
				SuccessInt(OpInt - (int)b.AsConstant().InternalValue);
			}

			void bi_int_mul(Expression b)
			{
				SuccessInt(OpInt * (int)b.AsConstant().InternalValue);
			}

			void bi_int_div(Expression b)
			{
				int v = (int)b.AsConstant().InternalValue;
				if (v == 0)
					IE.Fail_ZeroDivide(OpA);
				else
				{
					float c = (float)OpInt / (float)v;

					if (IE.EvalOptions.Enable_AutoConvertQuotientToInteger
						&& (c - Math.Truncate(c)) == 0.0f)
						SuccessInt((int)c);
					else
						SuccessFloat(c);
				}
			}

			void bi_int_mod(Expression b)
			{
				int v = (int)b.AsConstant().InternalValue;
				if (v == 0)
					IE.Fail_ZeroDivide(OpA);
				else
					SuccessInt(OpInt % v);
			}

			#endregion

			#region Float Arith

			public void bi_float_add(Expression b)
			{
				IE.DataTypeMap[DataType.Float].BinaryCall(BinaryOperationType.Addition, b, IE.CastFloat(OpA));
			}

			public void bi_float_sub(Expression b)
			{
				IE.DataTypeMap[DataType.Float].BinaryCall(BinaryOperationType.Addition, IE.CastFloat(OpA), b);
			}

			public void bi_float_mul(Expression b)
			{
				IE.DataTypeMap[DataType.Float].BinaryCall(BinaryOperationType.Multiplication, b, IE.CastFloat(OpA));
			}

			public void bi_float_div(Expression b)
			{
				IE.DataTypeMap[DataType.Float].BinaryCall(BinaryOperationType.Division, IE.CastFloat(OpA), b);
			}

			public void bi_float_mod(Expression b)
			{
				IE.DataTypeMap[DataType.Float].BinaryCall(BinaryOperationType.Modulo, IE.CastFloat(OpA), b);
			}

			#endregion

			#region BitWise

			void bi_int_bit_and(Expression b)
			{
				SuccessInt(OpInt & (int)b.AsConstant().InternalValue);
			}

			void bi_int_bit_or(Expression b)
			{
				SuccessInt(OpInt | (int)b.AsConstant().InternalValue);
			}

			void bi_int_bit_xor(Expression b)
			{
				SuccessInt(OpInt ^ (int)b.AsConstant().InternalValue);
			}

			void bi_int_bit_shl(Expression b)
			{
				SuccessInt(OpInt << (int)b.AsConstant().InternalValue);
			}

			void bi_int_bit_shr(Expression b)
			{
				SuccessInt(OpInt >> (int)b.AsConstant().InternalValue);
			}

			#endregion

			#region Comparisons

			void bi_float_bool_gt(Expression b)
			{
				IE.Success(IE.CreateBool(OpInt > (int)(float)b.AsConstant().InternalValue, OpA.Location));
			}

			void bi_float_bool_lt(Expression b)
			{
				IE.Success(IE.CreateBool(OpInt < (int)(float)b.AsConstant().InternalValue, OpA.Location));
			}

			Boolean eq_int(Expression b)
			{
				return OpInt == (int)b.AsConstant().InternalValue;
			}

			Boolean eq_float(Expression b)
			{
				return OpInt == (float)b.AsConstant().InternalValue;
			}

			//Remember, we assume that the truple has pre-evaluated items.
			Boolean eq_truple(Expression b)
			{
				foreach (Expression item in b.AsTruple().Items)
				{
					var dt = item.GetIEDataType();
					if (dt == DataType.Invalid)
					{
						IE.Fail_RequiresRuntime(item);
						return false;
					}
					if (!EqOpMap[dt](item))
						return false;
				}
				return true;
			}

			void bi_int_bool_gt(Expression b)
			{
				IE.Success(IE.CreateBool(OpInt > (int)b.AsConstant().InternalValue, OpA.Location));
			}

			void bi_int_bool_lt(Expression b)
			{
				IE.Success(IE.CreateBool(OpInt < (int)b.AsConstant().InternalValue, OpA.Location));
			}

			protected override bool is_true()
			{
				return (int)OpA.InternalValue != 0;
			}

			#endregion

			#region String

			void bi_str_add(Expression b)
			{
				IE.DataTypeMap[DataType.String].BinaryCall(BinaryOperationType.Addition, IE.CreateString(OpInt.ToString(), OpLoc), b);
			}

			#endregion

			#region Truple

			void bi_truple_mul(Expression b)
			{
				IE.DataTypeMap[DataType.Truple].BinaryCall(BinaryOperationType.Multiplication, b, OpA);
			}

			void bi_truple_div(Expression b)
			{
				IE.DataTypeMap[DataType.Truple].BinaryCall(BinaryOperationType.Multiplication, IE.CreateTruple(OpLoc, OpA), b);
			}
			#endregion
		}
		#endregion

		#region Float

		private class FloatDataType : DataTypeOperations
		{
			public FloatDataType(InlineEvaluator ie)
				: base(ie)
			{
				this.RegisterUnOpMap(DataTypeUnOpType.Negative, this.un_neg);
				this.RegisterUnOpMap(DataTypeUnOpType.Positive, this.un_pos);

				this.RegisterBiOpMap(DataType.Float, DataTypeBiOpType.Addition, this.bi_float_add);
				this.RegisterBiOpMap(DataType.Float, DataTypeBiOpType.BoolGreater, this.bi_float_bool_gt);
				this.RegisterBiOpMap(DataType.Float, DataTypeBiOpType.BoolLess, this.bi_float_bool_lt);
				this.RegisterBiOpMap(DataType.Float, DataTypeBiOpType.Division, this.bi_float_div);
				this.RegisterBiOpMap(DataType.Float, DataTypeBiOpType.Modulo, this.bi_float_mod);
				this.RegisterBiOpMap(DataType.Float, DataTypeBiOpType.Multiplication, this.bi_float_mul);
				this.RegisterBiOpMap(DataType.Float, DataTypeBiOpType.Subtraction, this.bi_float_sub);

				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.Addition, this.bi_int_add);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.BoolGreater, this.bi_int_bool_gt);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.BoolLess, this.bi_int_bool_lt);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.Division, this.bi_int_div);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.Modulo, this.bi_int_mod);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.Multiplication, this.bi_int_mul);
				this.RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.Subtraction, this.bi_int_sub);

				this.RegisterBiOpMap(DataType.String, DataTypeBiOpType.Addition, bi_str_add);

				this.RegisterBiOpMap(DataType.Truple, DataTypeBiOpType.Multiplication, this.bi_truple_mul);
				this.RegisterBiOpMap(DataType.Truple, DataTypeBiOpType.Division, this.bi_truple_div);

				this.RegisterEqMap(DataType.Float, this.eq_float);
				this.RegisterEqMap(DataType.Integer, this.eq_int);
				this.RegisterEqMap(DataType.Truple, this.eq_truple);
			}

			Constant OpA
			{
				get
				{
					return OpA_Expr.AsConstant();
				}
			}

			float OpFloat
			{
				get
				{
					return (float)OpA.InternalValue;
				}
			}

			void SuccessFloat(float flt)
			{
				IE.Success(IE.CreateFloat(flt, OpLoc));
			}

			#region Unaries

			void un_neg()
			{
				SuccessFloat(-OpFloat);
			}

			void un_pos()
			{
				SelfSuccess();
			}

			#endregion

			#region Float Arith

			void bi_float_add(Expression b)
			{
				SuccessFloat(OpFloat + (float)b.AsConstant().InternalValue);
			}

			void bi_float_sub(Expression b)
			{
				SuccessFloat(OpFloat - (float)b.AsConstant().InternalValue);
			}

			void bi_float_div(Expression b)
			{
				var bf = (float)b.AsConstant().InternalValue;
				if (bf == 0)
					IE.Fail_ZeroDivide(b);
				else
					SuccessFloat(OpFloat / bf);
			}

			void bi_float_mul(Expression b)
			{
				SuccessFloat(OpFloat * (float)b.AsConstant().InternalValue);
			}

			void bi_float_mod(Expression b)
			{
				var bf = (float)b.AsConstant().InternalValue;
				if (bf == 0)
					IE.Fail_ZeroDivide(b);
				else
					SuccessFloat(OpFloat % bf);
			}
			#endregion

			#region Integer Arith

			void bi_int_add(Expression b)
			{
				SuccessFloat(OpFloat + (int)b.AsConstant().InternalValue);
			}

			void bi_int_sub(Expression b)
			{
				SuccessFloat(OpFloat - (int)b.AsConstant().InternalValue);
			}

			void bi_int_div(Expression b)
			{
				var bf = (float)(int)b.AsConstant().InternalValue;
				if (bf == 0)
					IE.Fail_ZeroDivide(b);
				else
					SuccessFloat(OpFloat / bf);
			}

			void bi_int_mul(Expression b)
			{
				SuccessFloat(OpFloat * (int)b.AsConstant().InternalValue);
			}

			void bi_int_mod(Expression b)
			{
				var bf = (int)b.AsConstant().InternalValue;
				if (bf == 0)
					IE.Fail_ZeroDivide(b);
				else
					SuccessFloat(OpFloat % bf);
			}
			#endregion

			#region Comparisons

			protected override bool is_true()
			{
				return OpFloat != 0;
			}

			void bi_float_bool_gt(Expression b)
			{
				IE.Success(IE.CreateBool(OpFloat > (float)b.AsConstant().InternalValue, OpLoc));
			}

			void bi_float_bool_lt(Expression b)
			{
				IE.Success(IE.CreateBool(OpFloat < (float)b.AsConstant().InternalValue, OpLoc));
			}

			void bi_int_bool_gt(Expression b)
			{
				IE.Success(IE.CreateBool(OpFloat > (int)b.AsConstant().InternalValue, OpLoc));
			}

			void bi_int_bool_lt(Expression b)
			{
				IE.Success(IE.CreateBool(OpFloat < (int)b.AsConstant().InternalValue, OpLoc));
			}

			Boolean eq_float(Expression b)
			{
				return OpFloat == (float)b.AsConstant().InternalValue;
			}

			Boolean eq_int(Expression b)
			{
				return OpFloat == (int)b.AsConstant().InternalValue;
			}

			Boolean eq_truple(Expression b)
			{
				foreach (Expression item in b.AsTruple().Items)
				{
					var dt = item.GetIEDataType();
					if (dt == DataType.Invalid)
					{
						IE.Fail_RequiresRuntime(item);
						return false;
					}
					if (!EqOpMap[dt](item))
						return false;
				}
				return true;
			}

			#endregion

			#region String Arith

			void bi_str_add(Expression b)
			{
				IE.DataTypeMap[DataType.String].BinaryCall(BinaryOperationType.Addition, IE.CreateString(OpFloat.ToString(), OpLoc), b);
			}

			#endregion

			#region Truple Arith

			void bi_truple_mul(Expression b)
			{
				IE.DataTypeMap[DataType.Truple].BinaryCall(BinaryOperationType.Multiplication, b, OpA);
			}

			void bi_truple_div(Expression b)
			{
				IE.DataTypeMap[DataType.Truple].BinaryCall(BinaryOperationType.Multiplication, IE.CreateTruple(OpLoc, OpA), b);
			}

			#endregion
		}

		#endregion

		#region String

		class StringDataType : DataTypeOperations
		{
			public StringDataType(InlineEvaluator ie)
				: base(ie)
			{
				RegisterEqMap(DataType.String, this.eq_str);

				RegisterBiOpMap(DataType.String, DataTypeBiOpType.Addition, this.bi_str_add);
				RegisterBiOpMap(DataType.Truple, DataTypeBiOpType.Addition, this.bi_truple_add);
				RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.Addition, this.bi_int_add);
				RegisterBiOpMap(DataType.Float, DataTypeBiOpType.Addition, this.bi_float_add);
			}

			Constant OpA
			{
				get
				{
					return OpA_Expr.AsConstant();
				}
			}

			String OpVal
			{
				get
				{
					return OpA.Value;
				}
			}

			void SuccessString(String str)
			{
				IE.Success(IE.CreateString(str, OpLoc));
			}

			#region Comparisons

			protected override bool is_true()
			{
				return OpVal.Length != 0;
			}

			Boolean eq_str(Expression b)
			{
				return OpVal == b.AsConstant().Value;
			}


			#endregion

			#region String Appending

			void bi_str_add(Expression b)
			{
				SuccessString(OpVal + b.AsConstant().Value);
			}

			void bi_int_add(Expression b)
			{
				SuccessString(OpVal + b.AsConstant().InternalValue.ToString());
			}

			void bi_float_add(Expression b)
			{
				SuccessString(OpVal + b.AsConstant().InternalValue.ToString());
			}

			void bi_truple_add(Expression b)
			{
				//TODO: Finish this shit
				SuccessString(OpVal + b.AsTruple().Express(0));
			}

			void bi_bool_add(Expression b)
			{
				SuccessString(OpVal + ((bool)b.AsConstant().InternalValue ? 1 : 0));
			}

			#endregion
		}

		#endregion

		#region Truple

		class TrupleDataType : DataTypeOperations
		{
			public TrupleDataType(InlineEvaluator ie)
				: base(ie)
			{
				//Only division and multiplication are scalar
				RegisterBiOpMap(DataType.Float, DataTypeBiOpType.Division, this.bi_float_div);
				RegisterBiOpMap(DataType.Float, DataTypeBiOpType.Multiplication, this.bi_float_mul);

				RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.Division, this.bi_int_div);
				RegisterBiOpMap(DataType.Integer, DataTypeBiOpType.Multiplication, this.bi_int_mul);

				RegisterBiOpMap(DataType.Truple, DataTypeBiOpType.Addition, this.bi_truple_add);
				RegisterBiOpMap(DataType.Truple, DataTypeBiOpType.Division, this.bi_truple_div);
				RegisterBiOpMap(DataType.Truple, DataTypeBiOpType.Multiplication, this.bi_truple_mul);
				RegisterBiOpMap(DataType.Truple, DataTypeBiOpType.Subtraction, this.bi_truple_sub);

				RegisterEqMap(DataType.Float, this.eq_float);
				RegisterEqMap(DataType.Integer, this.eq_int);
				RegisterEqMap(DataType.Truple, this.eq_truple);
			}

			Truple OpA
			{
				get
				{
					return OpA_Expr.AsTruple();
				}
			}

			void ParallelEach(Truple other, Action<Expression, Expression> each)
			{
				var ii = other.Items.GetEnumerator();
				foreach (var item in OpA.Items)
				{
					ii.MoveNext();
					each(item, ii.Current);
				}
			}

			#region Truple Arith

			void bi_truple_add(Expression b)
			{
				var result = new Truple() { Location = OpLoc };
				ParallelEach(b.AsTruple(), (mine, yours) =>
					{
						IE.DataTypeMap[mine.GetIEDataType()].BinaryCall(BinaryOperationType.Addition, mine, yours);
						if (!IE.Successfull())
							return;
						result.Items.Add(IE.Result.Result);
					});
				IE.Success(result);
			}

			void bi_truple_sub(Expression b)
			{
				var result = new Truple() { Location = OpLoc };
				ParallelEach(b.AsTruple(), (mine, yours) =>
					{
						IE.DataTypeMap[mine.GetIEDataType()].BinaryCall(BinaryOperationType.Subtraction, mine, yours);
						if (!IE.Successfull())
							return;
						result.Items.Add(IE.Result.Result);
					});
				IE.Success(result);
			}

			void bi_truple_mul(Expression b)
			{
				var result = new Truple() { Location = OpLoc };
				ParallelEach(b.AsTruple(), (mine, yours) =>
				{
					IE.DataTypeMap[mine.GetIEDataType()].BinaryCall(BinaryOperationType.Multiplication, mine, yours);
					if (!IE.Successfull())
						return;
					result.Items.Add(IE.Result.Result);
				});
				IE.Success(result);
			}

			void bi_truple_div(Expression b)
			{
				var result = new Truple() { Location = OpLoc };
				ParallelEach(b.AsTruple(), (mine, yours) =>
				{
					IE.DataTypeMap[mine.GetIEDataType()].BinaryCall(BinaryOperationType.Multiplication, mine, yours);
					if (!IE.Successfull())
						return;
					result.Items.Add(IE.Result.Result);
				});
				IE.Success(result);
			}

			#endregion

			#region Float Arith

			void bi_float_mul(Expression b)
			{
				var result = new Truple() { Location = OpLoc };
				foreach (var item in OpA.Items)
				{
					IE.DataTypeMap[item.GetIEDataType()].BinaryCall(BinaryOperationType.Multiplication, item, b);
					if (!IE.Successfull())
						return;
					result.Items.Add(IE.Result.Result);
				}
				IE.Success(result);
			}

			void bi_float_div(Expression b)
			{
				var result = new Truple() { Location = OpLoc };
				foreach (var item in OpA.Items)
				{
					IE.DataTypeMap[item.GetIEDataType()].BinaryCall(BinaryOperationType.Division, item, b);
					if (!IE.Successfull())
						return;
					result.Items.Add(IE.Result.Result);
				}
				IE.Success(result);
			}


			#endregion

			#region Integer Arith

			void bi_int_mul(Expression b)
			{
				var result = new Truple() { Location = OpLoc };
				foreach (var item in OpA.Items)
				{
					IE.DataTypeMap[item.GetIEDataType()].BinaryCall(BinaryOperationType.Multiplication, item, b);
					if (!IE.Successfull())
						return;
					result.Items.Add(IE.Result.Result);
				}
				IE.Success(result);
			}

			void bi_int_div(Expression b)
			{
				var result = new Truple() { Location = OpLoc };
				foreach (var item in OpA.Items)
				{
					IE.DataTypeMap[item.GetIEDataType()].BinaryCall(BinaryOperationType.Division, item, b);
					if (!IE.Successfull())
						return;
					result.Items.Add(IE.Result.Result);
				}
				IE.Success(result);
			}

			#endregion

			#region Comparisons

			Boolean eq_float(Expression b)
			{
				return IE.DataTypeMap[DataType.Float].EqOpMap[DataType.Truple](OpA);
			}

			Boolean eq_int(Expression b)
			{
				return IE.DataTypeMap[DataType.Integer].EqOpMap[DataType.Truple](OpA);
			}

			Boolean eq_truple(Expression b)
			{
				var ii = b.AsTruple().Items.GetEnumerator();
				foreach (var item in OpA.Items)
				{
					ii.MoveNext();
					if (!IE.DataTypeMap[item.GetIEDataType()].EqOpMap[ii.Current.GetIEDataType()](ii.Current))
						return false;
				}
				return true;
			}

			protected override bool is_true()
			{
				foreach (var item in OpA.Items)
				{
					var dt = item.GetIEDataType();
					if (!IE.DataTypeMap.ContainsKey(dt))
					{
						IE.Fail_RequiresRuntime(OpA);
						return false;
					}
					
					bool result = IE.DataTypeMap[dt].is_true(item);
					if (IE.Result.ResultType != EvaluationResult.Success
						|| result)
						return false;
				}
				return true;
			}

			#endregion
		}

		#endregion

		#endregion
	}
}
