namespace SRNSMudApp.Tests.Auth;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging.Abstractions;

using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services.Auth;

using Xunit;

public class ExternalTokenVerificationServiceTests
{
    private sealed class CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task VerifyLineTokenAsync_UsesPerRequestAuthorizationHeaderAndDoesNotMutateDefaultHeaders()
    {
        // Arrange: THREAD-01 shared HttpClient の DefaultRequestHeaders を変更せず、リクエストヘッダーに Bearer を載せる
        HttpRequestMessage? capturedRequest = null;
        var token = "sample-line-token-12345";

        var handler = new CapturingHttpMessageHandler(req =>
        {
            capturedRequest = req;
            var responseJson = JsonSerializer.Serialize(new { userId = "line-uid-001" });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var service = new ExternalTokenVerificationService(httpClient, NullLogger<ExternalTokenVerificationService>.Instance);

        // Act
        var result = await service.VerifyTokenAsync("LINE", token);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Equal("https://api.line.me/v2/profile", capturedRequest.RequestUri?.ToString());

        // リクエスト固有ヘッダーにトークンが設定されていること
        Assert.NotNull(capturedRequest.Headers.Authorization);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization.Scheme);
        Assert.Equal(token, capturedRequest.Headers.Authorization.Parameter);

        // 共有 HttpClient の DefaultRequestHeaders は変更されていない（null のまま）こと
        Assert.Null(httpClient.DefaultRequestHeaders.Authorization);

        // 結果が成功であること
        var success = result switch
        {
            Success<ExternalTokenPayload> s => s,
            _ => throw new InvalidOperationException($"Expected Success but got {result}")
        };
        Assert.Equal("line-uid-001", success.Value.ProviderKey);
    }

    [Fact]
    public async Task VerifyLineTokenAsync_ConcurrentRequests_DoNotMutateSharedHeadersOrCrossContaminate()
    {
        // Arrange: 複数スレッドからの並行リクエストで race condition が起きないことを検証
        var handler = new CapturingHttpMessageHandler(req =>
        {
            var authHeader = req.Headers.Authorization?.Parameter ?? string.Empty;
            var responseJson = JsonSerializer.Serialize(new { userId = $"uid-for-{authHeader}" });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var service = new ExternalTokenVerificationService(httpClient, NullLogger<ExternalTokenVerificationService>.Instance);

        // Act: 20件の並行リクエストを実行
        var tasks = Enumerable.Range(1, 20).Select(async i =>
        {
            var token = $"line-token-concurrent-{i}";
            var result = await service.VerifyTokenAsync("LINE", token);
            var success = result switch
            {
                Success<ExternalTokenPayload> s => s,
                _ => throw new InvalidOperationException($"Expected Success but got {result}")
            };
            Assert.Equal($"uid-for-{token}", success.Value.ProviderKey);

            // DefaultRequestHeaders が常に null であることを各スレッドで確認
            Assert.Null(httpClient.DefaultRequestHeaders.Authorization);
        });

        await Task.WhenAll(tasks);

        // 完了後も DefaultRequestHeaders は null のまま
        Assert.Null(httpClient.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task VerifyTokenAsync_UnsupportedProvider_ReturnsFailure()
    {
        // Arrange
        var httpClient = new HttpClient();
        var service = new ExternalTokenVerificationService(httpClient, NullLogger<ExternalTokenVerificationService>.Instance);

        // Act
        var result = await service.VerifyTokenAsync("UNKNOWN_PROVIDER", "token");

        // Assert
        var failure = result switch
        {
            Failure f => f,
            _ => throw new InvalidOperationException($"Expected Failure but got {result}")
        };
        Assert.Equal("Unsupported provider", failure.ErrorMessage);
    }

    [Fact]
    public async Task VerifyLineTokenAsync_WhenLineReturnsError_ReturnsFailure()
    {
        // Arrange
        var handler = new CapturingHttpMessageHandler(req =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var httpClient = new HttpClient(handler);
        var service = new ExternalTokenVerificationService(httpClient, NullLogger<ExternalTokenVerificationService>.Instance);

        // Act
        var result = await service.VerifyTokenAsync("LINE", "bad-token");

        // Assert
        var failure = result switch
        {
            Failure f => f,
            _ => throw new InvalidOperationException($"Expected Failure but got {result}")
        };
        Assert.Contains("Invalid LINE token", failure.ErrorMessage);
        Assert.Null(httpClient.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task VerifyLineTokenAsync_WhenTokenContainsInvalidFormat_ReturnsFailure()
    {
        var httpClient = new HttpClient();
        var service = new ExternalTokenVerificationService(httpClient, NullLogger<ExternalTokenVerificationService>.Instance);

        // Act: 改行文字などヘッダーとして不正な文字列を含むトークン
        var result = await service.VerifyTokenAsync("LINE", "invalid\r\ntoken");

        // Assert: 例外でクラッシュせず Failure が返ること
        var failure = result switch
        {
            Failure f => f,
            _ => throw new InvalidOperationException($"Expected Failure but got {result}")
        };
        Assert.Contains("Invalid LINE token format", failure.ErrorMessage);
    }

    [Fact]
    public async Task VerifyGithubTokenAsync_WhenTokenContainsInvalidFormat_ReturnsFailure()
    {
        var httpClient = new HttpClient();
        var service = new ExternalTokenVerificationService(httpClient, NullLogger<ExternalTokenVerificationService>.Instance);

        // Act
        var result = await service.VerifyTokenAsync("GITHUB", "invalid\ntoken");

        // Assert
        var failure = result switch
        {
            Failure f => f,
            _ => throw new InvalidOperationException($"Expected Failure but got {result}")
        };
        Assert.Contains("Invalid GitHub token format", failure.ErrorMessage);
    }
}