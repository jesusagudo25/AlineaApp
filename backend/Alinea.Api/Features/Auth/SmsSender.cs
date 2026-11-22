using System.Collections.Concurrent;

namespace Alinea.Api.Features.Auth;

public interface ISmsSender
{
    Task<bool> SendAsync(string phone, string code, Guid challengeId, string deviceHash, CancellationToken cancellationToken);
}

public sealed class DisabledSmsSender : ISmsSender
{
    public Task<bool> SendAsync(string phone, string code, Guid challengeId, string deviceHash, CancellationToken cancellationToken) => Task.FromResult(false);
}

// Exclusivo de Development/Testing: no escribe códigos, destinos ni mensajes a logs o disco.
public sealed class DevelopmentSmsSender(TimeProvider clock) : ISmsSender
{
    private sealed record Message(string Code, string DeviceHash, DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<Guid, Message> messages = new();
    public Task<bool> SendAsync(string phone, string code, Guid challengeId, string deviceHash, CancellationToken cancellationToken)
    {
        foreach (var item in messages.Where(x => x.Value.ExpiresAt <= clock.GetUtcNow())) messages.TryRemove(item.Key, out _);
        messages[challengeId] = new(code, deviceHash, clock.GetUtcNow().AddMinutes(5));
        return Task.FromResult(true);
    }
    public string? Read(Guid id, string deviceHash) => messages.TryGetValue(id, out var item)
        && item.ExpiresAt > clock.GetUtcNow() && AuthCrypto.Equal(item.DeviceHash, deviceHash) ? item.Code : null;
}
