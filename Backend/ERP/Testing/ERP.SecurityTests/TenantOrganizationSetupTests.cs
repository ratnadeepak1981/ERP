using System;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Address;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.Setup;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.MasterData.Address;
using ERP.Application.Features.MasterData.Branch;
using ERP.Application.Features.MasterData.Company;
using ERP.Application.Features.Setup;
using ERP.Infrastructure.Repositories.MasterData;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.SecurityTests;

public class TenantOrganizationSetupTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public TenantOrganizationSetupTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    private DomainDbContext CreateDomainDb()
    {
        DatabaseSafetyGuard.AssertSafeTestDatabase(TestConstants.DomainDbTest);
        var options = new DbContextOptionsBuilder<DomainDbContext>()
            .UseSqlServer(TestConstants.DomainDbTest)
            .Options;
        return new DomainDbContext(options);
    }

    private ITenantOrganizationSetupService CreateSetupService(DomainDbContext db)
    {
        var companyRepo = new ERP.Infrastructure.Repositories.MasterData.CompanyRepository(db);
        var currencyRepo = new ERP.Infrastructure.Repositories.MasterData.CurrencyRepository(db);
        var branchRepo = new ERP.Infrastructure.Repositories.MasterData.BranchRepository(db);
        var addressRepo = new ERP.Infrastructure.Repositories.MasterData.AddressRepository(db);

        var companyService = new ERP.Application.Features.MasterData.Company.CompanyService(companyRepo, currencyRepo);
        var branchService = new ERP.Application.Features.MasterData.Branch.BranchService(branchRepo);

        return new TenantOrganizationSetupService(
            companyRepo,
            companyService,
            branchRepo,
            branchService,
            currencyRepo,
            addressRepo,
            db);
    }

    [Fact]
    public async Task SetupOrganizationAsync_CreatesCompanyBranchAndAddresses_WhenMissing()
    {
        var testTenantId = Guid.NewGuid();
        using var db = CreateDomainDb();

        try
        {
            var service = CreateSetupService(db);

            var request = new TenantOrganizationSetupRequest
            {
                CompanyName = "Test Corporation",
                CompanyCode = "TCORP",
                BranchName = "Test Main Branch",
                BranchCode = "MAIN",
                BaseCurrencyCode = "LKR",
                City = "Colombo",
                AddressLine1 = "100 Galle Road"
            };

            var result = await service.SetupOrganizationAsync(testTenantId, request);

            Assert.True(result.CompanyCreated);
            Assert.True(result.BranchCreated);
            Assert.True(result.CompanyAddressCreated);
            Assert.True(result.BranchAddressCreated);

            // Verify Company in DB
            var company = await db.Companies.FirstOrDefaultAsync(c => c.TenantId == testTenantId && c.Code == "TCORP");
            Assert.NotNull(company);
            Assert.Equal("Test Corporation", company.Name);
            Assert.NotEqual(Guid.Empty, company.BaseCurrencyId);

            // Verify Branch in DB
            var branch = await db.Branches.FirstOrDefaultAsync(b => b.CompanyId == company.Id && b.Code == "MAIN");
            Assert.NotNull(branch);
            Assert.Equal("Test Main Branch", branch.Name);

            // Verify CompanyAddress link in DB
            var compAddrLink = await db.CompanyAddresses.FirstOrDefaultAsync(ca => ca.TenantId == testTenantId && ca.CompanyId == company.Id);
            Assert.NotNull(compAddrLink);
            Assert.True(compAddrLink.IsDefault);

            // Verify BranchAddress link in DB
            var branchAddrLink = await db.BranchAddresses.FirstOrDefaultAsync(ba => ba.TenantId == testTenantId && ba.BranchId == branch.Id);
            Assert.NotNull(branchAddrLink);
            Assert.True(branchAddrLink.IsDefault);
        }
        finally
        {
            // Cleanup test tenant records
            var branchAddrs = await db.BranchAddresses.Where(x => x.TenantId == testTenantId).ToListAsync();
            db.BranchAddresses.RemoveRange(branchAddrs);

            var compAddrs = await db.CompanyAddresses.Where(x => x.TenantId == testTenantId).ToListAsync();
            db.CompanyAddresses.RemoveRange(compAddrs);

            var addrs = await db.Addresses.Where(x => x.TenantId == testTenantId).ToListAsync();
            db.Addresses.RemoveRange(addrs);

            var companies = await db.Companies.Where(x => x.TenantId == testTenantId).ToListAsync();
            var compIds = companies.Select(c => c.Id).ToList();
            var branches = await db.Branches.Where(x => compIds.Contains(x.CompanyId)).ToListAsync();
            db.Branches.RemoveRange(branches);
            db.Companies.RemoveRange(companies);

            var currencies = await db.Currencies.Where(x => x.TenantId == testTenantId).ToListAsync();
            db.Currencies.RemoveRange(currencies);

            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task SetupOrganizationAsync_IsIdempotent_DoesNotDuplicateOnRepeatedExecution()
    {
        var testTenantId = Guid.NewGuid();
        using var db = CreateDomainDb();

        try
        {
            var service = CreateSetupService(db);

            var request = new TenantOrganizationSetupRequest
            {
                CompanyName = "Idempotent Retail",
                CompanyCode = "IDEMP",
                BranchName = "Main Branch",
                BranchCode = "MAIN"
            };

            // First run
            var result1 = await service.SetupOrganizationAsync(testTenantId, request);
            Assert.True(result1.CompanyCreated);
            Assert.True(result1.BranchCreated);

            int compCount1 = await db.Companies.CountAsync(c => c.TenantId == testTenantId);
            int branchCount1 = await db.Branches.CountAsync(b => b.CompanyId == result1.CompanyId);
            int compAddrCount1 = await db.CompanyAddresses.CountAsync(ca => ca.TenantId == testTenantId);
            int branchAddrCount1 = await db.BranchAddresses.CountAsync(ba => ba.TenantId == testTenantId);

            // Second run (repeated execution)
            var result2 = await service.SetupOrganizationAsync(testTenantId, request);
            Assert.False(result2.CompanyCreated);
            Assert.False(result2.BranchCreated);
            Assert.False(result2.CompanyAddressCreated);
            Assert.False(result2.BranchAddressCreated);

            int compCount2 = await db.Companies.CountAsync(c => c.TenantId == testTenantId);
            int branchCount2 = await db.Branches.CountAsync(b => b.CompanyId == result1.CompanyId);
            int compAddrCount2 = await db.CompanyAddresses.CountAsync(ca => ca.TenantId == testTenantId);
            int branchAddrCount2 = await db.BranchAddresses.CountAsync(ba => ba.TenantId == testTenantId);

            Assert.Equal(compCount1, compCount2);
            Assert.Equal(branchCount1, branchCount2);
            Assert.Equal(compAddrCount1, compAddrCount2);
            Assert.Equal(branchAddrCount1, branchAddrCount2);
        }
        finally
        {
            var branchAddrs = await db.BranchAddresses.Where(x => x.TenantId == testTenantId).ToListAsync();
            db.BranchAddresses.RemoveRange(branchAddrs);

            var compAddrs = await db.CompanyAddresses.Where(x => x.TenantId == testTenantId).ToListAsync();
            db.CompanyAddresses.RemoveRange(compAddrs);

            var addrs = await db.Addresses.Where(x => x.TenantId == testTenantId).ToListAsync();
            db.Addresses.RemoveRange(addrs);

            var companies = await db.Companies.Where(x => x.TenantId == testTenantId).ToListAsync();
            var compIds = companies.Select(c => c.Id).ToList();
            var branches = await db.Branches.Where(x => compIds.Contains(x.CompanyId)).ToListAsync();
            db.Branches.RemoveRange(branches);
            db.Companies.RemoveRange(companies);

            var currencies = await db.Currencies.Where(x => x.TenantId == testTenantId).ToListAsync();
            db.Currencies.RemoveRange(currencies);

            await db.SaveChangesAsync();
        }
    }
}
