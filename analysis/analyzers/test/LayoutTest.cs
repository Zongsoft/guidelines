using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class LayoutTest
{
	[Theory]
	[InlineData("public void Foo() => GC.KeepAlive(1);")]
	[InlineData("public void Foo() =>\n\t\tGC.KeepAlive(1);")]
	public async Task AllowsAdjacentExpressionBodiedMethods(string method) =>
		await AssertCleanAsync(Wrap(method + "\n\tpublic void Bar()\n\t{\n\t}\n"));

	[Theory]
	[InlineData("public struct First { }\npublic class Second { }\npublic record Third(int Value) { }")]
	[InlineData("public record First { }\npublic record struct Second(int Value) { }\npublic struct Third { }")]
	[InlineData("public class First(int value) { }\npublic struct Second(int value) { }\npublic record Third(int Value) { }\npublic record struct Fourth(int Value) { }")]
	public async Task AllowsAdjacentEmptyTypeDeclarations(string declarations)
	{
		var result = await AnalyzeAsync("namespace Samples;\n\n" + declarations + "\n");
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("IDE0055", result.Output);
		Assert.DoesNotContain("ZS2003", result.Output);
		Assert.DoesNotContain("error CS", result.Output);
	}

	[Theory]
	[InlineData("private readonly TimeSpan       _sliding = TimeSpan.Zero;\n\tpublic TimeSpan Sliding => _sliding;")]
	[InlineData("private const byte DELETABLE_VALUE  = 0x01;\n\tprivate const byte UPDATABLE_VALUE  = 0x02;\n\tpublic byte Value => DELETABLE_VALUE | UPDATABLE_VALUE;")]
	[InlineData("public int Read()\n\t{\n\t\tint    value  = 1;\n\t\treturn value;\n\t}")]
	public async Task AllowsDeclarationAlignment(string members) => await AssertCleanAsync(Wrap(members));

	[Fact]
	public async Task AllowsEmptyWhile() => await AssertCleanAsync(Wrap("public void Read(global::System.Collections.Concurrent.ConcurrentQueue<int> queue)\n\t{\n\t\twhile(queue.TryDequeue(out _));\n\t}"));

	[Theory]
	[InlineData("#pragma warning disable CS0168", "#pragma warning restore CS0168")]
	[InlineData("#region Properties", "#endregion")]
	[InlineData("#nullable enable", "#nullable restore")]
	[InlineData("#line 100", "#line default")]
	[InlineData("#if NET10_0_OR_GREATER", "#endif")]
	public async Task AllowsIndentedDirectives(string begin, string end) => await AssertCleanAsync(Wrap(begin + "\n\tpublic int Value => 1;\n\t" + end));

	[Theory]
	[InlineData("[global::System.Runtime.InteropServices.In][global::System.Runtime.InteropServices.Out]ref int value")]
	[InlineData("[global::System.Runtime.InteropServices.In]int value")]
	public async Task AllowsCompactParameterAttributes(string parameter) => await AssertCleanAsync(Wrap("public int Read(" + parameter + ") => value;"));

	[Theory]
	[InlineData("try { onError?.Invoke(exception); } catch { }")]
	[InlineData("try { onError?.Invoke(exception); }\n\t\tcatch(ObjectDisposedException) { }")]
	[InlineData("try { onError?.Invoke(exception); } finally { }")]
	[InlineData("try { onError?.Invoke(exception); }\n\t\tcatch\n\t\t{\n\t\t\tGC.KeepAlive(exception);\n\t\t\tGC.KeepAlive(onError);\n\t\t}")]
	[InlineData("try\n\t\t{\n\t\t\tGC.KeepAlive(exception);\n\t\t\tGC.KeepAlive(onError);\n\t\t}\n\t\tcatch { }")]
	public async Task AllowsCompactAndPartialExceptionBlocks(string body) => await AssertCleanAsync(Wrap("public void Run(Action<Exception> onError, Exception exception)\n\t{\n\t\t" + body + "\n\t}"));

	[Theory]
	[InlineData("public void Run(int value)\n\t{\n\t\ttry { value++; GC.KeepAlive(value); } catch { value--; GC.KeepAlive(value); } finally { value = 0; GC.KeepAlive(value); }\n\t}")]
	[InlineData("public void Run(int value)\n\t{\n\t\twhile(value-- > 0)\n\t\t{\n\t\t\ttry { GC.KeepAlive(value); }\n\t\t\tcatch(InvalidOperationException) { GC.KeepAlive(value); continue; }\n\t\t\tcatch(Exception) { GC.KeepAlive(value); break; }\n\t\t}\n\t}")]
	[InlineData("public global::System.Collections.Generic.IEnumerable<int> Read()\n\t{\n\t\ttry { GC.KeepAlive(1); }\n\t\tcatch { GC.KeepAlive(2); yield break; }\n\t}")]
	[InlineData("public void Run(int value)\n\t{\n\t\ttry { if(value > 0) GC.KeepAlive(value); } catch { while(value-- > 0) GC.KeepAlive(value); } finally { if(value < 0) GC.KeepAlive(value); }\n\t}")]
	[InlineData("public void Run(int value)\n\t{\n\t\ttry { if(value > 0) { GC.KeepAlive(value); } GC.KeepAlive(value); } catch { }\n\t}")]
	[InlineData("public int Read(int value)\n\t{\n\t\ttry { global::System.Action action = () => { if(value > 0) value++; }; }\n\t\tfinally { value++; }\n\t\treturn value;\n\t}")]
	public async Task AllowsCompactExceptionStatements(string members) => await AssertCleanAsync(Wrap(members));

	[Theory]
	[InlineData("try { value++; value++; value++; } catch { }")]
	[InlineData("try { } catch { value++; value++; value++; }")]
	[InlineData("try { } finally { value++; value++; value++; }")]
	public async Task RejectsThreeStatementExceptionClauses(string body)
	{
		var result = await AnalyzeAsync(Wrap("public void Run(int value)\n\t{\n\t\t" + body + "\n\t}"));
		Assert.Contains("error IDE2001", result.Output);
		Assert.DoesNotContain("error CS", result.Output);
	}

	[Fact]
	public async Task AllowsLineEndingConditionalColon() => await AssertCleanAsync(Wrap("public int Read(int interval, int longest)\n\t{\n\t\tlongest = interval > 0 ?\n\t\t\tMath.Max(interval, longest):\n\t\t\tMath.Min(interval, longest);\n\t\treturn longest;\n\t}"));

	[Theory]
	[InlineData("var result = type.IsPrimitive || type.IsEnum ||\n\t\t             type == typeof(string) || type == typeof(decimal);\n\t\treturn result;")]
	[InlineData("return type.IsPrimitive ||\n\t\t       type == typeof(string);")]
	public async Task AllowsOperandAlignedContinuations(string body) => await AssertCleanAsync(Wrap("public bool Read(Type type)\n\t{\n\t\t" + body + "\n\t}"));

	[Theory]
	[InlineData(2)]
	[InlineData(8)]
	public async Task HonorsContinuationTabWidth(int width)
	{
		var source = Wrap("public bool Read(Type type)\n\t{\n\t\treturn type.IsPrimitive ||\n\t\t       type.IsEnum;\n\t}");
		var result = await AnalyzerRunner.AnalyzeAsync(source, true, true,
			"root = true\n[*]\nindent_style = tab\nindent_size = " + width + "\ntab_width = " + width + "\nend_of_line = crlf\n", false, false, null);
		Assert.True(result.Success, result.Output);
		Assert.Empty(result.Output);
	}

	[Fact]
	public async Task AllowsSpacedTupleNames() => await AssertCleanAsync(Wrap("public object Read(object entity, object property) => (Entity : entity, Property : property);"));

	[Theory]
	[InlineData("SIZE", "private")]
	[InlineData("DEFAULT_CAPACITY", "private readonly")]
	[InlineData("HTTP2_BUFFER", "private static")]
	[InlineData("MAX_VALUE", "private static readonly")]
	[InlineData("MAX_VALUE", "private const")]
	[InlineData("_VALUE", "private")]
	public async Task AllowsUppercasePrivateFields(string name, string modifiers) =>
		await AssertCleanAsync(Wrap(modifiers + " int " + name + " = 1;\n\tpublic int Value => " + name + ";"));

	[Theory]
	[InlineData("_GaugeMethod_", "private")]
	[InlineData("_gauge_method_", "private readonly")]
	[InlineData("_Gauge1_Method_", "private static")]
	[InlineData("__GetHandlersMethodTemplate__", "private static readonly")]
	[InlineData("__getHandlers__", "private")]
	[InlineData("__Get_Handlers__", "private readonly")]
	[InlineData("_GaugeMethod_", "private const")]
	[InlineData("_", "private")]
	[InlineData("__", "private")]
	[InlineData("_混合名称_", "private")]
	public async Task AllowsDelimitedPrivateFields(string name, string modifiers) =>
		await AssertCleanAsync(Wrap(modifiers + " int " + name + " = 1;\n\tpublic int Value => " + name + ";"));

	[Theory]
	[InlineData("private int GaugeMethod = 1;\n\tpublic int Value => GaugeMethod;")]
	[InlineData("private int _GaugeMethod = 1;\n\tpublic int Value => _GaugeMethod;")]
	[InlineData("private int GaugeMethod_ = 1;\n\tpublic int Value => GaugeMethod_;")]
	[InlineData("private int DEFAULT_Value = 1;\n\tpublic int Value => DEFAULT_Value;")]
	[InlineData("public int _GaugeMethod_ = 1;")]
	[InlineData("protected int _GaugeMethod_ = 1;")]
	[InlineData("internal const int _GaugeMethod_ = 1;")]
	[InlineData("protected static readonly int __GetHandlersMethodTemplate__ = 1;")]
	[InlineData("public int _GaugeMethod_ => 1;")]
	[InlineData("public int Read(int _GaugeMethod_) => _GaugeMethod_;")]
	[InlineData("public int Read()\n\t{\n\t\tvar MAX_VALUE = 1;\n\t\treturn MAX_VALUE;\n\t}")]
	[InlineData("public int Read()\n\t{\n\t\tvar _GaugeMethod_ = 1;\n\t\treturn _GaugeMethod_;\n\t}")]
	public async Task RetainsOtherNamingRestrictions(string members)
	{
		var result = await AnalyzeAsync(Wrap(members));
		Assert.Contains("error IDE1006", result.Output);
		Assert.DoesNotContain("error CS", result.Output);
	}

	[Theory]
	[InlineData("if(value == 0)\n\t\t\treturn;\n\t\tif(value == 1)\n\t\t\treturn;", true)]
	[InlineData("if(value == 0)\n\t\t{\n\t\t\treturn;\n\t\t}\n\t\tif(value == 1)\n\t\t\treturn;", false)]
	[InlineData("if(value == 0)\n\t\t\treturn;\n\t\twhile(value > 1)\n\t\t\tvalue--;", false)]
	[InlineData("if(value == 0)\n\t\t\treturn;\n\t\telse\n\t\t\tvalue++;\n\t\tif(value == 1)\n\t\t\treturn;", false)]
	[InlineData("while(value > 0)\n\t\t\tif(value-- == 1)\n\t\t\t\treturn;\n\t\twhile(value > 0)\n\t\t\tvalue--;", false)]
	[InlineData("while(value > 0)\n\t\t{\n\t\t\tvalue--;\n\t\t}\n\t\twhile(value > 0)\n\t\t\tvalue--;", false)]
	public async Task ChecksConsecutiveUnbracedControls(string body, bool allowed)
	{
		var result = await AnalyzeAsync(Wrap("public void Run(int value)\n\t{\n\t\t" + body + "\n\t}"));
		if(allowed)
			Assert.True(result.Success, result.Output);
		else
			Assert.Contains("error ZS2003", result.Output);
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(0, 1)]
	[InlineData(0, 2)]
	[InlineData(0, 3)]
	[InlineData(1, 0)]
	[InlineData(1, 1)]
	[InlineData(1, 2)]
	[InlineData(1, 3)]
	[InlineData(2, 0)]
	[InlineData(2, 1)]
	[InlineData(2, 2)]
	[InlineData(2, 3)]
	[InlineData(3, 0)]
	[InlineData(3, 1)]
	[InlineData(3, 2)]
	[InlineData(3, 3)]
	public async Task ChecksSimpleControlStatementSeparation(int first, int second)
	{
		string[] statements =
		[
			"if(enabled)\n\t\t\tGC.KeepAlive(items);",
			"for(var index = 0; index < items.Length; index++)\n\t\t\titems[index]++;",
			"foreach(var item in items)\n\t\t\tGC.KeepAlive(item);",
			"while(enabled)\n\t\t\t;",
		];

		foreach(var separated in new[] { false, true })
		{
			var body = statements[first] + (separated ? "\n\n" : "\n") + "\t\t" + statements[second];
			var result = await AnalyzeAsync(Wrap("public void Run(int[] items, bool enabled)\n\t{\n\t\t" + body + "\n\t}"));
			var expected = !separated && first != second ? 1 : 0;
			Assert.True(result.Success == (expected == 0), result.Output);
			var positions = System.Text.RegularExpressions.Regex.Matches(result.Output, @"Example\.cs\(\d+,\d+\): error ZS2003")
				.Select(match => match.Value).Distinct().ToArray();
			Assert.Equal(expected, positions.Length);
		}
	}

	[Theory]
	[InlineData("using System.Threading.Tasks;")]
	[InlineData("using System.Text;")]
	[InlineData("using Microsoft.CSharp.RuntimeBinder;")]
	[InlineData("using global::System.Collections.Generic;")]
	public async Task AllowsUnusedOrdinaryNamespaces(string directive) => await AssertCleanAsync(directive + "\n\n" + Wrap("public int Value => 1;"));

	[Theory]
	[InlineData("global using System.Threading.Tasks;")]
	[InlineData("using Tasks = System.Threading.Tasks;")]
	[InlineData("using static System.Threading.Tasks.Task;")]
	public async Task RejectsNonOrdinarySystemImports(string directive)
	{
		var result = await AnalyzeAsync(directive + "\n\n" + Wrap("public int Value => 1;"));
		Assert.Contains("error ZS0005", result.Output);
	}

	[Fact]
	public async Task AllowsNestedOrdinaryNamespace()
	{
		var result = await AnalyzeAsync("using Samples.System;\n\nnamespace Samples.System;\n\npublic class Sample { }\n");
		Assert.True(result.Success, result.Output);
	}

	[Theory]
	[InlineData("public int Read(int value) => value+1;", "IDE0055")]
	[InlineData("public int Read()\n\t{\n\t\tint value= 1;\n\t\treturn value;\n\t}", "IDE0055")]
	[InlineData("public int[] Read()\n\t{\n\t\tint[]values = [];\n\t\treturn values;\n\t}", "IDE0055")]
	[InlineData("public int Read([global::System.Runtime.InteropServices.In]  int value) => value;", "IDE0055")]
	[InlineData("public int Read(int value) => value > 0 ? 1:0;", "IDE0055")]
	[InlineData("public int Read(int value)\n\t{\n\t\treturn value > 0 ||\n\t\t      value < -1 ? 1 : 0;\n\t}", "IDE0055")]
	[InlineData("public void Run(int value)\n\t{\n\t\twhile(value > 0) value--;\n\t}", "IDE2001")]
	[InlineData("public void Run()\n\t{\n\t\twhile(((Func<bool>)(() => { if(true) return false; return false; }))());\n\t}", "IDE2001")]
	[InlineData("public void Run(int value)\n\t{\n\t\ttry { value++; value++; value++; } catch { }\n\t}", "IDE2001")]
	[InlineData("public void Run(int value)\n\t{\n\t\ttry { value+=1; } catch { }\n\t}", "IDE0055")]
	[InlineData("public int Read(int value) => Math.Abs(value : value);", "IDE0055")]
	[InlineData("public bool Read(int value)\n\t{\n\t\treturn value > 0 ||\n\t\t       value<-1;\n\t}", "IDE0055")]
	[InlineData("public void Run()\n\t{\n\t\t  GC.KeepAlive(1);\n\t}", "IDE0055")]
	[InlineData("public string Read(string first, string second)\n\t{\n\t\treturn string.Concat(first,\n                             second);\n\t}", "IDE0055")]
	[InlineData("public int Read(int value) { value++; value++; return value; }", "IDE2001")]
	[InlineData("public Action Read() => () => { GC.KeepAlive(1); GC.KeepAlive(2); GC.KeepAlive(3); };", "IDE2001")]
	[InlineData("public Action Read() => () => { if(true) GC.KeepAlive(1); };", "IDE2001")]
	[InlineData("public int Read(int value) { value+=1; return value; }", "IDE0055")]
	[InlineData("public Func<int, int> Read() => value => { value+=1; return value; };", "IDE0055")]
	[InlineData("public int Read(int value)\n\t{\n\t\ttry {\n\t\t\treturn value;\n\t\t}\n\t\tfinally { GC.KeepAlive(value); }\n\t}", "IDE0055")]
	[InlineData("public Sample() :\n\t\tthis(1)\t{ }\n\tpublic Sample(int value) { }", "IDE0055")]
	[InlineData("private int value = 1;\n\tpublic int Value => value;", "IDE1006")]
	[InlineData("private int _Value = 1;\n\tpublic int Value => _Value;", "IDE1006")]
	[InlineData("public int Read()\n\t{\n\t\tvar VALUE = 1;\n\t\treturn VALUE;\n\t}", "IDE1006")]
	[InlineData("private const int value = 1;\n\tpublic int Value => value;", "IDE1006")]
	[InlineData("protected int _value = 1;", "IDE1006")]
	[InlineData("public void Run(int value)\n\t{\n\t\tif(value == 0)\n\t\t\treturn;\n\t\telse\n\t\t\tvalue++;\n\t\tif(value == 1)\n\t\t\treturn;\n\t}", "ZS2003")]
	public async Task RetainsNearbyViolations(string members, string diagnostic)
	{
		var result = await AnalyzeAsync(Wrap(members));
		Assert.Contains("error " + diagnostic, result.Output);
	}

	[Theory]
	[InlineData("public void Run(Exception error)\n\t{\n\t\tthrow error is InvalidOperationException ? error :\n\t\t      new InvalidOperationException(error.Message, error);\n\t}")]
	[InlineData("public bool Read(Type type)\n\t{\n\t\treturn !(type.IsPrimitive || type.IsArray || type.IsEnum ||\n\t\t         type == typeof(string) ||\n\t\t         type == typeof(decimal));\n\t}")]
	[InlineData("public string Read(string first, string second)\n\t{\n\t\treturn string.Concat(first,\n\t\t                     second);\n\t}")]
	[InlineData("public string Read(string first, string second)\n\t{\n\t\tvar result = string.Concat(first,\n\t\t                           second,\n\t\t                           first);\n\t\treturn result;\n\t}")]
	[InlineData("public string Read(char[] values)\n\t{\n\t\tvar result = new string(values,\n\t\t                 0,\n\t\t                 values.Length);\n\t\treturn result;\n\t}")]
	[InlineData("public Sample() :\n\t\tthis(1) { }\n\tpublic Sample(int value) { }")]
	[InlineData("public Sample() :\n\t\tbase() { }")]
	[InlineData("public int Read(Type type)\n\t{\n\t\treturn type.IsGenericType && type.IsValueType ?\n\t\t       1 : 0;\n\t}")]
	[InlineData("public bool Read(bool value)\n\t{\n\t\tvalue = value ||\n\t\t      (value && value) ? value : false;\n\t\treturn value;\n\t}")]
	[InlineData("public Action<object> Read() => delegate(object sender)\n\t{\n\t\tGC.KeepAlive(sender);\n\t};")]
	[InlineData("public int Read(int value) { value++; return value; }")]
	[InlineData("public int Read() { return 0; }")]
	[InlineData("public int Read()\n\t{\n\t\tint Local(int value) { value++; return value; }\n\t\treturn Local(1);\n\t}")]
	[InlineData("public Func<int, int> Read() => value => { value++; return value; };")]
	[InlineData("public Action Read() => () => { GC.KeepAlive(1); GC.KeepAlive(2); };")]
	[InlineData("public Func<int, int> Read() => delegate(int value) { value++; return value; };")]
	public async Task AllowsCompactAndAlignedExpressions(string members) => await AssertCleanAsync(Wrap(members));

	private static string Wrap(string members) => "using System;\nusing System.Linq;\n\nnamespace Samples;\n\npublic class Sample\n{\n\t" + members + "\n}\n";
	private static Task<AnalysisResult> AnalyzeAsync(string source) => AnalyzerRunner.AnalyzeAsync(source, true, true, null, false, false, null);
	private static async Task AssertCleanAsync(string source)
	{
		var result = await AnalyzeAsync(source);
		Assert.True(result.Success, result.Output);
		Assert.Empty(result.Output);
	}
}
