using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace SimianBookings.Tests.TestDoubles;

internal sealed class FakeHttpResponseData : HttpResponseData
{
    public FakeHttpResponseData(FunctionContext functionContext)
        : base(functionContext)
    {
        Headers = new HttpHeadersCollection();
        Body = new MemoryStream();
        Cookies = new FakeHttpCookies();
    }

    public override HttpStatusCode StatusCode { get; set; }

    public override HttpHeadersCollection Headers { get; set; }

    public override Stream Body { get; set; }

    public override HttpCookies Cookies { get; }
}

internal sealed class FakeHttpCookies : HttpCookies
{
    public override void Append(string name, string value)
    {
    }

    public override void Append(IHttpCookie cookie)
    {
    }

    public override IHttpCookie CreateNew()
    {
        return new FakeHttpCookie();
    }
}

internal sealed class FakeHttpCookie : IHttpCookie
{
    public string Name { get; } = string.Empty;

    public string Value { get; } = string.Empty;

    public DateTimeOffset? Expires { get; } = null;

    public bool? HttpOnly { get; } = null;

    public double? MaxAge { get; } = null;

    public string? Domain { get; } = null;

    public string? Path { get; } = null;

    public bool? Secure { get; } = null;

    public SameSite SameSite { get; } = SameSite.None;
}
