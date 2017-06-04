using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using GameScriptCompiler;
using GameScriptCompiler.CodeDOM;
using GameScriptCompiler.ContextAnalyzation;

namespace TestCompile
{
	class StubGen
	{
		private static Boolean CheckUnSpecifics(IEnumerable<ErrorMessage> errors, ContextAnalyzer.ErrorType[] exclusives)
		{
			return errors.FirstOrDefault(em => !exclusives.Contains(em.ErrorType)) == null;
		}
		public static Boolean CheckUnSpecifics(IEnumerable<ErrorMessage> errors)
		{
			return CheckUnSpecifics(errors, new ContextAnalyzer.ErrorType[]
			{
				ContextAnalyzer.ErrorType.UnknownFunctionReferenceLocal,
				ContextAnalyzer.ErrorType.UnknownFunctionInvocation
			});
		}
		public static void StubOut(StreamWriter sw, IEnumerable<ErrorMessage> errors, CompilerOptions options)
		{
			Dictionary<String, FunctionCall> final = new Dictionary<string, FunctionCall>(options.GetComparer());
			foreach (var msg in errors)
			{
				if (msg.ErrorType != ContextAnalyzer.ErrorType.UnknownFunctionInvocation)
					continue;
				FunctionCall fc = msg.Target.AsFunctionCall();
				FunctionCall ffc;
				Boolean result = final.TryGetValue(fc.Function.Name, out ffc);
				if (!result || (result && fc.Arguments.Count > ffc.Arguments.Count))
					final[fc.Function.Name] = fc;
			}

			foreach (var fc in final)
			{
				StringBuilder sb = new StringBuilder("{0}(".Fmt(fc.Key));
				foreach (var p in fc.Value.Arguments.AsIndexable())
					sb.Append("param{0}".Fmt(p.Index+1) + (p.Index < p.Total - 1 ? ", " : ""));
				sb.Append("){}");
				sw.WriteLine(sb.ToString());
			}
		}
	}
}
