namespace SRNSMudApp.Tests.Auth;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging.Abstractions;

using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services.Auth;

using Xunit;

public class ExternalTokenVerificationConcurrencyTests
{
    private sealed class JitterDelayHttpMessageHandler : HttpMessageHandler
    {
        private readonly ConcurrentBag<string> _observedHeaderViolations = new();

        public IReadOnlyCollection<string> HeaderViolations => _observedHeaderViolations;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Verify request header
            var authHeader = request.Headers.Authorization?.Parameter;

            // Random micro-delay to maximize concurrency overlap
            await Task.Delay(Random.Shared.Next(1, 15), cancellationToken);

            var responseJson = JsonSerializer.Serialize(new { userId = $"uid-for-{authHeader}" });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    [Fact]
    public async Task THREAD01_StressTest_HighConcurrency_LineTokenVerification_NoHeaderMutationOrLeak()
    {
        var handler = new JitterDelayHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var service = new ExternalTokenVerificationService(httpClient, NullLogger<ExternalTokenVerificationService>.Instance);

        const int concurrency = 100;
        var violations = new ConcurrentBag<string>();

        var tasks = Enumerable.Range(1, concurrency).Select(async i =>
        {
            var token = $"token-guid-{Guid.NewGuid():N}-{i}";

            // Check before call
            if (httpClient.DefaultRequestHeaders.Authorization != null)
            {
                violations.Add($"Before call {i}: DefaultRequestHeaders.Authorization was not null!");
            }

            var result = await service.VerifyTokenAsync("LINE", token);

            // Check after call
            if (httpClient.DefaultRequestHeaders.Authorization != null)
            {
                violations.Add($"After call {i}: DefaultRequestHeaders.Authorization was not null!");
            }

            switch (result)
            {
                case Success<ExternalTokenPayload> success:
                    if (success.Value.ProviderKey != $"uid-for-{token}")
                    {
                        violations.Add($"Call {i}: Cross-contamination detected! Expected uid-for-{token}, got {success.Value.ProviderKey}");
                    }
                    break;
                case Failure failure:
                    violations.Add($"Call {i}: Unexpected failure: {failure.ErrorMessage}");
                    break;
            }
        });

        await Task.WhenAll(tasks);

        Assert.Empty(violations);
        Assert.Null(httpClient.DefaultRequestHeaders.Authorization);
    }
}