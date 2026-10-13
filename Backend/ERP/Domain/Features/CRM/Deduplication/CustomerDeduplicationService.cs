using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.CRM.Deduplication;

public class CustomerDeduplicationService : ICustomerDeduplicationService
{
    private readonly DomainDbContext _context;

    public CustomerDeduplicationService(DomainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<CustomerMatchResult> FindDuplicateAsync(
        Guid tenantId,
        CustomerMatchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (criteria == null) throw new ArgumentNullException(nameof(criteria));

        var matches = await FindAllPotentialMatchesAsync(tenantId, criteria, cancellationToken);

        // Prioritize exact matches first, then highest confidence potential match
        var exactMatch = matches.FirstOrDefault(m => m.Confidence == CustomerMatchConfidence.ExactMatch);
        if (exactMatch != null)
        {
            return exactMatch;
        }

        var potentialMatch = matches.FirstOrDefault(m => m.Confidence == CustomerMatchConfidence.PotentialMatch);
        return potentialMatch ?? CustomerMatchResult.NoMatch();
    }

    public async Task<List<CustomerMatchResult>> FindAllPotentialMatchesAsync(
        Guid tenantId,
        CustomerMatchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (criteria == null) throw new ArgumentNullException(nameof(criteria));

        var results = new List<CustomerMatchResult>();

        string? normTax = NormalizeString(criteria.TaxNumber);
        string? normEmail = NormalizeString(criteria.Email);
        string? normPhone = NormalizePhone(criteria.Phone);
        string? normName = NormalizeString(criteria.CustomerName);

        // 1. Check exact Tax Number match (Strongest match)
        if (!string.IsNullOrEmpty(normTax))
        {
            var taxMatch = await _context.Customers
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.IsActive && c.TaxNumber != null && c.TaxNumber.Trim() == normTax)
                .FirstOrDefaultAsync(cancellationToken);

            if (taxMatch != null)
            {
                results.Add(CustomerMatchResult.Exact(
                    CustomerMatchType.TaxNumberExact,
                    taxMatch.CustomerId,
                    taxMatch.CustomerCode,
                    taxMatch.CustomerName,
                    $"Exact tax number match found for '{normTax}'."));
            }
        }

        // 2. Check exact Email match
        if (!string.IsNullOrEmpty(normEmail))
        {
            var emailMatch = await _context.Customers
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.IsActive && c.Email != null && c.Email.Trim().ToLower() == normEmail.ToLower())
                .FirstOrDefaultAsync(cancellationToken);

            if (emailMatch != null && results.All(r => r.MatchedCustomerId != emailMatch.CustomerId))
            {
                results.Add(CustomerMatchResult.Exact(
                    CustomerMatchType.EmailExact,
                    emailMatch.CustomerId,
                    emailMatch.CustomerCode,
                    emailMatch.CustomerName,
                    $"Exact email match found for '{normEmail}'."));
            }
        }

        // 3. Check Phone match (Potential duplicate)
        string? rawPhone = criteria.Phone?.Trim();
        string? normPhoneDigits = NormalizePhone(criteria.Phone);
        if (!string.IsNullOrEmpty(rawPhone) || !string.IsNullOrEmpty(normPhoneDigits))
        {
            var phoneMatch = await _context.Customers
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.IsActive && c.Phone != null &&
                    (c.Phone.Trim() == rawPhone ||
                     (!string.IsNullOrEmpty(normPhoneDigits) &&
                      c.Phone.Replace("-", "").Replace(" ", "").Replace("(", "").Replace(")", "").Replace("+", "") == normPhoneDigits.Replace("+", ""))))
                .FirstOrDefaultAsync(cancellationToken);

            if (phoneMatch != null && results.All(r => r.MatchedCustomerId != phoneMatch.CustomerId))
            {
                results.Add(CustomerMatchResult.Potential(
                    CustomerMatchType.PhoneExact,
                    phoneMatch.CustomerId,
                    phoneMatch.CustomerCode,
                    phoneMatch.CustomerName,
                    $"Phone number match found for '{criteria.Phone}'."));
            }
        }

        // 4. Check Normalized Customer Name match (Potential duplicate)
        if (!string.IsNullOrEmpty(normName))
        {
            var nameMatch = await _context.Customers
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.IsActive && c.CustomerName.Trim().ToLower() == normName.ToLower())
                .FirstOrDefaultAsync(cancellationToken);

            if (nameMatch != null && results.All(r => r.MatchedCustomerId != nameMatch.CustomerId))
            {
                results.Add(CustomerMatchResult.Potential(
                    CustomerMatchType.CustomerNameNormalized,
                    nameMatch.CustomerId,
                    nameMatch.CustomerCode,
                    nameMatch.CustomerName,
                    $"Matching customer name found for '{nameMatch.CustomerName}'."));
            }
        }

        return results;
    }

    private static string? NormalizeString(string? input)
    {
        return string.IsNullOrWhiteSpace(input) ? null : input.Trim();
    }

    private static string? NormalizePhone(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        // Strip common delimiters
        return new string(input.Where(c => char.IsDigit(c) || c == '+').ToArray());
    }
}
