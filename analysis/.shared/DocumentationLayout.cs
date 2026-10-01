using System.Linq;

using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis;

/// <summary>统一 XML 文档中单行非空内容的定位。</summary>
internal static class DocumentationLayout
{
	public static bool TryGetContentSpan(XmlElementSyntax element, SourceText text, out TextSpan span)
	{
		span = default;
		var start = -1;
		var end = -1;

		foreach(var token in element.Content.SelectMany(content => content.DescendantTokens()))
		{
			//只使用内容 token，排除文档前缀和空白行，直接定位原文以保留实体及行内 XML。
			var value = token.Text;
			var first = 0;
			var last = value.Length;

			while(first < last && char.IsWhiteSpace(value[first]))
				first++;
			while(last > first && char.IsWhiteSpace(value[last - 1]))
				last--;

			if(first == last)
				continue;

			if(start < 0)
				start = token.SpanStart + first;

			end = token.SpanStart + last;
			if(!SourceTextUtility.IsSingleLine(text, TextSpan.FromBounds(start, end - 1)))
				return false;
		}

		if(start < 0)
			return false;

		span = TextSpan.FromBounds(start, end);
		return true;
	}
}
