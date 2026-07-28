namespace CodeTutor.Application.UseCases;

using CodeTutor.Application.Ai;

/// <summary>
/// 截取当前帧 → 保存图片 → OCR → 文本拼接 → 更新会话。
/// </summary>
public interface ICaptureAndOcrUseCase
{
    Task ExecuteAsync(CancellationToken ct);
}

/// <summary>
/// 撤销最近一次截屏事务，恢复到上一个 checkpoint。
/// </summary>
public interface IUndoLastCaptureUseCase
{
    Task ExecuteAsync(CancellationToken ct);
}

/// <summary>
/// 归档当前会话并创建新空会话。
/// </summary>
public interface IClearSessionUseCase
{
    Task ExecuteAsync(CancellationToken ct);
}

/// <summary>
/// 将累计 OCR 文本发送给 DeepSeek 文本 API。
/// </summary>
public interface ISolveTextUseCase
{
    Task ExecuteAsync(CancellationToken ct);
}

/// <summary>
/// 将全部截图发送给视觉模型 API（OCR 不准时的兜底）。
/// </summary>
public interface ISolveVisionUseCase
{
    Task ExecuteAsync(CancellationToken ct);
}

/// <summary>
/// 加载历史会话。
/// </summary>
public interface ILoadSessionUseCase
{
    Task ExecuteAsync(Guid sessionId, CancellationToken ct);
}

/// <summary>
/// 保存 API 配置并测试连接。
/// </summary>
public interface ISaveAndTestApiUseCase
{
    Task ExecuteAsync(AiProviderKind provider, string apiKey, string model, CancellationToken ct);
}

/// <summary>
/// 右下角追问。
/// </summary>
public interface ISendFollowUpUseCase
{
    Task ExecuteAsync(string message, CancellationToken ct);
}

/// <summary>
/// 用户手动编辑累计题干后保存。
/// </summary>
public interface IUpdateQuestionTextUseCase
{
    Task ExecuteAsync(string text, CancellationToken ct);
}

/// <summary>
/// 删除单条历史会话。
/// </summary>
public interface IDeleteSessionUseCase
{
    Task ExecuteAsync(Guid sessionId, CancellationToken ct);
}

/// <summary>
/// 清空全部历史会话（需二次确认，由 UI 负责）。
/// </summary>
public interface IClearAllHistoryUseCase
{
    Task ExecuteAsync(CancellationToken ct);
}
