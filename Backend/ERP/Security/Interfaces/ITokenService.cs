using System;
using System.Collections.Generic;

namespace Security.Interfaces
{
    public interface ITokenService
    {
        string GenerateAccessToken(
            Guid userId,
            string username,
            Guid? tenantId,
            IEnumerable<string> roles,
            IEnumerable<string> permissions);

        string GenerateRefreshToken();
    }
}