using Microsoft.CodeAnalysis.Text;

namespace Zongsoft.CodeAnalysis;

/// <summary>共享源文本的行、水平空白及缩进读取。</summary>
internal static class SourceTextUtility
{
	public static bool IsSingleLine(SourceText text, TextSpan span) =>
		text.Lines.GetLineFromPosition(span.Start).LineNumber == text.Lines.GetLineFromPosition(span.End).LineNumber;

	public static bool IsHorizontalWhitespace(SourceText text, TextSpan span)
	{
		for(var position = span.Start; position < span.End; position++)
		{
			if(text[position] != ' ' && text[position] != '\t')
				return false;
		}

		return true;
	}

	public static string GetIndentation(SourceText text, int position)
	{
		var line = text.Lines.GetLineFromPosition(position);
		var end = line.Start;

		while(end < line.End && (text[end] == ' ' || text[end] == '\t'))
			end++;

		return text.ToString(TextSpan.FromBounds(line.Start, end));
	}

	public static string GetNewLine(SourceText text, int position)
	{
		//使用指定位置附近已有的换行符；没有可用换行时采用仓库约定的 CRLF。
		for(var index = text.Lines.GetLineFromPosition(position).LineNumber; index >= 0; index--)
		{
			var line = text.Lines[index];

			if(line.EndIncludingLineBreak > line.End)
				return text.ToString(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak));
		}

		return "\r\n";
	}
}
