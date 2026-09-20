#pragma warning disable CA1848

using System.Diagnostics.CodeAnalysis;
using System.Numerics.Tensors;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SRNSMudApp.Data;

using Tag = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Services;

/// <summary>
///     タグの読み取り処理を担当する Query サービス。
///     書き込み処理と依存関係を分離し、検索系コンポーネントをテストしやすくする。
/// </summary>
public class TagSearchQueryService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ITagEmbeddingService tagEmbeddingService,
    IMemoryCache? memoryCache = null,
    ILogger<TagSearchQueryService>? logger = null) : ITagSearchQueryService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory =
        dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
    private readonly ITagEmbeddingService _tagEmbeddingService =
        tagEmbeddingService ?? throw new ArgumentNullException(nameof(tagEmbeddingService));
    private readonly IMemoryCache? _memoryCache = memoryCache;
    private readonly ILogger<TagSearchQueryService> _logger =
        logger ?? NullLogger<TagSearchQueryService>.Instance;

    private const string LinkConversionCandidatesCacheKey = "TagSearchQueryService_CandidateTagsForLinkConversion";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(3);

    public async Task<List<Tag>> GetAllTagsAsync()
    {
        await using ApplicationDbContext dbContext = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        return await dbContext.Tags.AsNoTracking().ToListAsync().ConfigureAwait(false);
    }

    public async Task<List<Tag>> SearchTagsAsync(string searchText)
    {
        float[] queryVector = (await _tagEmbeddingService.GenerateEmbeddingAsync(searchText).ConfigureAwait(false)).ToArray();

        await using ApplicationDbContext dbContext = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);

        List<Tag> textMatches = await dbContext.Tags
            .Where(x => x.Name.Contains(searchText) || x.Content.Contains(searchText))
            .AsNoTracking()
            .ToListAsync()
            .ConfigureAwait(false);

        List<Tag> vectorTags = await dbContext.Tags.Where(x => x.Embedding != null).AsNoTracking().ToListAsync().ConfigureAwait(false);

        List<Tag> vectorMatches = vectorTags
            .Where(x => x.Embedding.Length == queryVector.Length)
            .OrderByDescending(x => TensorPrimitives.CosineSimilarity(x.Embedding, queryVector))
            .Take(50)
            .ToList();

        return
        [
            .. textMatches.Concat(vectorMatches)
                .DistinctBy(x => x.Id)
                .Take(50)
        ];
    }

    public async Task<Tag?> FindTagByNameAsync(string tagName)
    {
        await using ApplicationDbContext dbContext = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        return await dbContext.Tags.FirstOrDefaultAsync(t => t.Name == tagName).ConfigureAwait(false);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "検索失敗時にテキスト検索へフォールバックするため広く捕捉する")]
    public async Task<List<Tag>> SearchTagsWithFallbackAsync(string? value, CancellationToken token = default)
    {
        if (token.IsCancellationRequested)
        {
            return [];
        }

        await using ApplicationDbContext dbContext = await _dbFactory.CreateDbContextAsync(token).ConfigureAwait(false);
        IQueryable<Tag> query = dbContext.Tags.Include(t => t.Owner).AsQueryable();

        if (string.IsNullOrEmpty(value))
        {
            return await query.OrderBy(t => t.Name).AsNoTracking().Take(50).ToListAsync(token).ConfigureAwait(false);
        }

        try
        {
            float[] queryVector = (await _tagEmbeddingService.GenerateEmbeddingAsync(value).ConfigureAwait(false)).ToArray();

            List<Tag> textMatches = await query
                .Where(x => x.Name.Contains(value) || x.Content.Contains(value))
                .OrderBy(x => x.Name)
                .AsNoTracking()
                .ToListAsync(token)
                .ConfigureAwait(false);

            List<Tag> vectorTags = await query.Where(x => x.Embedding != null).AsNoTracking().ToListAsync(token).ConfigureAwait(false);

            List<Tag> vectorMatches = vectorTags
                .Where(x => x.Embedding.Length == queryVector.Length)
                .OrderByDescending(x => TensorPrimitives.CosineSimilarity(x.Embedding, queryVector))
                .Take(50)
                .ToList();

            return
            [
                .. textMatches.Concat(vectorMatches)
                    .DistinctBy(x => x.Id)
                    .Take(50)
            ];
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception ex)
        {
            if (token.IsCancellationRequested)
            {
                return [];
            }

            _logger.LogWarning(ex, "Vector search failed: {Message}", ex.Message);
            query = query.Where(x => x.Name.Contains(value) || x.Content.Contains(value));
            return await query.OrderBy(x => x.Name).AsNoTracking().Take(50).ToListAsync(token).ConfigureAwait(false);
        }
    }

    public async Task<List<Tag>> GetTagsWithDetailsAsync()
    {
        await using ApplicationDbContext dbContext = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        return await dbContext.Tags
            .Include(t => t.Owner)
            .Include(t => t.TargetTagRelations)
            .ThenInclude(tr => tr.Tag)
            .ThenInclude(t => t.Owner)
            .AsNoTracking()
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<Tag>> GetCandidateTagsForLinkConversionAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache != null && _memoryCache.TryGetValue(LinkConversionCandidatesCacheKey, out List<Tag>? cached) && cached != null)
        {
            return cached;
        }

        await using ApplicationDbContext dbContext = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<Tag> tags = await dbContext.Tags
            .AsNoTracking()
            .Where(t => t.Name != Tag.RootTagName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Tag> candidateTags = tags
            .Where(t => !Tag.VoteTagNames.Contains(t.Name)
                        && !Tag.ReactionTagNames.Contains(t.Name)
                        && t.Name.Length >= 2)
            .ToList();

        if (_memoryCache != null)
        {
            _memoryCache.Set(LinkConversionCandidatesCacheKey, candidateTags, CacheDuration);
        }

        return candidateTags;
    }
}