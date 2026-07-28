using System.Collections.Concurrent;
using CodeTutor.Application.Abstractions;

namespace CodeTutor.Infrastructure.Secrets;

/// <summary>
/// Linux 开发：优先环境变量，会话内可暂存；Windows DPAPI 在后续阶段实现。
/// </summary>
public sealed class EnvironmentSecretStore : ISecretStore
{
    private static readonly ConcurrentDictionary<string, string> SessionCache = new();

    private static readonly Dictionary<string, string> EnvMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["deepseek.api_key"] = "DEEPSEEK_API_KEY",
        ["volcano.api_key"] = "VOLCANO_API_KEY",
        ["vision.api_key"] = "VISION_API_KEY"
    };

    public Task SaveAsync(string name, string value, CancellationToken ct)
    {
        SessionCache[name] = value;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string name, CancellationToken ct)
    {
        if (SessionCache.TryGetValue(name, out var cached))
            return Task.FromResult<string?>(cached);

        if (EnvMappings.TryGetValue(name, out var envName))
        {
            var env = Environment.GetEnvironmentVariable(envName);
            if (!string.IsNullOrEmpty(env))
                return Task.FromResult<string?>(env);
        }

        return Task.FromResult<string?>(null);
    }

    public Task DeleteAsync(string name, CancellationToken ct)
    {
        SessionCache.TryRemove(name, out _);
        return Task.CompletedTask;
    }
}
