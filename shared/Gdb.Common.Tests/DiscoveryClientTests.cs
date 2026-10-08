using System.Net;
using System.Text.Json;
using Gdb.Common.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gdb.Common.Tests;

[TestClass]
public class DiscoveryClientTests
{
    private static IHttpClientFactory CreateMockFactory(Func<HttpRequestMessage, HttpResponseMessage> handlerFunc)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync((HttpRequestMessage request, CancellationToken token) => handlerFunc(request));

        var client = new HttpClient(handlerMock.Object);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        return factoryMock.Object;
    }

    [TestMethod]
    public async Task RegistryResolver_UsesRegistryThenCaches()
    {
        int calls = 0;
        var factory = CreateMockFactory(req =>
        {
            calls++;
            Assert.AreEqual("/resolve/accounts", req.RequestUri?.PathAndQuery);
            var content = new StringContent(JsonSerializer.Serialize(new { name = "accounts", url = "http://live-accounts:8001" }));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        var resolver = new RegistryResolver(factory, "http://registry:8010", new Dictionary<string, string>());
        
        var r1 = await resolver.ResolveAsync("accounts");
        var r2 = await resolver.ResolveAsync("accounts");

        Assert.AreEqual("http://live-accounts:8001", r1);
        Assert.AreEqual("http://live-accounts:8001", r2);
        Assert.AreEqual(1, calls); // Served from cache
    }

    [TestMethod]
    public async Task RegistryResolver_FallsBackWhenRegistryDown()
    {
        var factory = CreateMockFactory(req => throw new HttpRequestException("registry down"));

        var resolver = new RegistryResolver(factory, "http://registry:8010", new Dictionary<string, string> { { "accounts", "http://env:8001" } });

        var r1 = await resolver.ResolveAsync("accounts", "http://explicit:8001");
        var r2 = await resolver.ResolveAsync("accounts");
        var r3 = await resolver.ResolveAsync("unknown");

        Assert.AreEqual("http://explicit:8001", r1);
        Assert.AreEqual("http://env:8001", r2);
        Assert.IsNull(r3);
    }

}
