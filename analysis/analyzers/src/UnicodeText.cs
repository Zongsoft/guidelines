namespace Zongsoft.CodeAnalysis.Analyzers;

internal static partial class UnicodeText
{
	public static bool ContainsLocalizedText(string text)
	{
		var followsLetter = false;

		for(var index = 0; index < text.Length; index++)
		{
			var value = (int)text[index];
			if(char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
				value = char.ConvertToUtf32(text[index++], text[index]);

			if(Contains(EMOJI, value))
			{
				followsLetter = false;
				continue;
			}

			//变体选择符影响显示样式，本身不是文字附加符号。
			if(value >= 0xFE00 && value <= 0xFE0F || value >= 0xE0100 && value <= 0xE01EF)
				continue;

			if(Contains(LETTERS, value))
			{
				if(value > 0x7F)
					return true;

				followsLetter = true;
			}
			else if(Contains(MARKS, value))
			{
				if(followsLetter)
					return true;
			}
			else
				followsLetter = false;
		}

		return false;
	}

	private static bool Contains(int[] ranges, int value)
	{
		var lower = 0;
		var upper = ranges.Length / 2 - 1;

		while(lower <= upper)
		{
			var middle = lower + (upper - lower) / 2;
			if(value < ranges[middle * 2])
				upper = middle - 1;
			else if(value > ranges[middle * 2 + 1])
				lower = middle + 1;
			else
				return true;
		}

		return false;
	}
}
