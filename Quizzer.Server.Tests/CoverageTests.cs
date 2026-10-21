using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Quizzer.Server.Tests
{
    [TestClass]
    public sealed class CoverageTests
    {
        /// <summary>
        /// Deny by default, in beide Richtungen gehalten: jeder Endpunkt ohne <c>AllowAnonymous</c>
        /// faellt unter die Rueckfallregel, und die anonymen sind genau die benannten. Ein neuer
        /// anonymer Endpunkt macht diesen Test rot, bis er in <see cref="Endpoints.AnonymousRoutes"/> steht.
        /// </summary>
        [TestMethod]
        public async Task OnlyTheNamedRoutesAreAnonymous()
        {
            await using var factory = ServerTestHost.CreateFactory();
            _ = factory.CreateClient();

            var sources = factory.Services.GetServices<EndpointDataSource>();
            var anonymous = new HashSet<string>();
            var all = new List<string>();

            foreach (var endpoint in sources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>())
            {
                var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"];

                foreach (var method in methods)
                {
                    var name = $"{method} /{endpoint.RoutePattern.RawText!.TrimStart('/')}";
                    all.Add(name);

                    if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null)
                        anonymous.Add(name);
                }
            }

            Assert.IsTrue(all.Count > 10, "Es wurden kaum Endpunkte gefunden: " + string.Join(", ", all));
            CollectionAssert.AreEquivalent(Endpoints.AnonymousRoutes.ToList(), anonymous.ToList());
        }
    }
}
