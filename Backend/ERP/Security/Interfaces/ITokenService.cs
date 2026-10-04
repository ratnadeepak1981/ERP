using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Security.Interfaces
{
    public interface ITokenService
    {
        string GenerateAccessToken(
            Guid userId,
            Guid? tenantId,
            IEnumerable<string> roles,
            IEnumerable<string> permissions);

        string GenerateRefreshToken();
    }
}
