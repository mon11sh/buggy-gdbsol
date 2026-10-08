using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthService.Domain.Ports;

public interface IUserServicePort
{
    Task<Dictionary<string, object>?> VerifyUserCredentialsAsync(string loginId, string password, CancellationToken ct = default);
}
