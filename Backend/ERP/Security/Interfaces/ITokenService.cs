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

        string GenerateServiceToken(
            string serviceName,
            IEnumerable<string> roles,
            IEnumerable<string> permissions,
            TimeSpan? lifetime = null);

        string GenerateCustomerAccessToken(
            Guid userId,
            string username,
            Guid tenantId,
            Guid customerId,
            Guid contactId,
            IEnumerable<string> roles,
            IEnumerable<string> permissions);

        string GenerateRefreshToken();

        string HashToken(string token);

        int GetAccessTokenLifetimeMinutes();
    }
}
