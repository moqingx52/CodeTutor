namespace CodeTutor.Application.Ocr;

/// <summary>
/// OCR 换行位置通常不可靠，拼接前去掉识别结果中的换行符。
/// </summary>
public static class OcrTextNormalizer
{
    public static string Flatten(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return string.Concat(text.Where(c => c is not '\r' and not '\n'));
    }
}
