namespace CentralGatewayService.Resolvers;

public interface IServiceResolver
{
    Task<string?> ResolveAsync(string serviceName);
}
