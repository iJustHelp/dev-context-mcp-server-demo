using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Demo.CityApi.Tests;

public sealed class OpenMeteoStubHttpMessageHandler(
    params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
    : HttpMessageHandler
{
    private readonly ConcurrentQueue<Func<HttpRequestMessage, HttpResponseMessage>>
        _responses = new(responses);
    private readonly ConcurrentQueue<string> _requestedCityNames = new();
    private int _requestCount;

    public int RequestCount => Volatile.Read(ref _requestCount);

    public IReadOnlyCollection<string> RequestedCityNames =>
        _requestedCityNames.ToArray();

    public static Func<HttpRequestMessage, HttpResponseMessage> Json(
        string responseBody,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return _ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                responseBody,
                Encoding.UTF8,
                "application/json"),
        };
    }

    public static Func<HttpRequestMessage, HttpResponseMessage> Failure(
        Exception exception)
    {
        return _ => throw exception;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _requestCount);

        var query = QueryHelpers.ParseQuery(request.RequestUri?.Query);
        if (query.TryGetValue("name", out var name))
        {
            _requestedCityNames.Enqueue(name.ToString());
        }

        if (!_responses.TryDequeue(out var response))
        {
            throw new InvalidOperationException(
                "No Open-Meteo response was configured for this request.");
        }

        return Task.FromResult(response(request));
    }
}
