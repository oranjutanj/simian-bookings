using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace SimianBookings.Tests.TestDoubles;

internal sealed class FakeHttpRequestData : HttpRequestData
{
    public FakeHttpRequestData(FunctionContext functionContext, string method, Uri url, string? body = null)
        : base(functionContext)
    {
        Method = method;
        Url = url;
        Headers = new HttpHeadersCollection();
        Body = new MemoryStream();

        if (!string.IsNullOrEmpty(body))
        {
            using var writer = new StreamWriter(Body, leaveOpen: true);
            writer.Write(body);
            writer.Flush();
            Body.Position = 0;
        }
    }

    public override Stream Body { get; }

    public override HttpHeadersCollection Headers { get; }

    public override IReadOnlyCollection<IHttpCookie> Cookies => Array.Empty<IHttpCookie>();

    public override Uri Url { get; }

    public override IEnumerable<ClaimsIdentity> Identities => Array.Empty<ClaimsIdentity>();

    public override string Method { get; }

    public override HttpResponseData CreateResponse()
    {
        return new FakeHttpResponseData(FunctionContext)
        {
            StatusCode = HttpStatusCode.OK
        };
    }
}
