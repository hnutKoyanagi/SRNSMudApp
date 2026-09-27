namespace SRNSMudApp.Services.Push;

using System.Collections.Concurrent;

using SRNSMudApp.Models.Push;

/// <summary>
/// スレッドセーフなインメモリ辞書を用いたサブスクリプションストア実装。
/// </summary>
public sealed class InMemoryPushSubscriptionStore : IPushSubscriptionStore
{
    private sealed record StoredSubscription(PushSubscriptionDto Dto, string? UserId);

    private readonly ConcurrentDictionary<string, StoredSubscription> _subscriptions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task AddOrUpdateAsync(PushSubscriptionDto subscription, string? userId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        if (string.IsNullOrWhiteSpace(subscription.Endpoint))
        {
            throw new ArgumentException("Endpoint must not be empty.", nameof(subscription));
        }

        string? effectiveUserId = userId ?? subscription.UserId;
        var dtoWithUserId = subscription with { UserId = effectiveUserId };
        _subscriptions[subscription.Endpoint] = new StoredSubscription(dtoWithUserId, effectiveUserId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            _ = _subscriptions.TryRemove(endpoint, out _);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<PushSubscriptionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<PushSubscriptionDto> result = _subscriptions.Values
            .Select(s => s.Dto)
            .ToList()
            .AsReadOnly();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<PushSubscriptionDto>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            IReadOnlyCollection<PushSubscriptionDto> empty = [];
            return Task.FromResult(empty);
        }

        IReadOnlyCollection<PushSubscriptionDto> result = _subscriptions.Values
            .Where(s => string.Equals(s.UserId, userId, StringComparison.Ordinal))
            .Select(s => s.Dto)
            .ToList()
            .AsReadOnly();
        return Task.FromResult(result);
    }
}