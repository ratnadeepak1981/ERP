using System;

namespace ERP.SecurityTests;

public static class TestConstants
{
    // Connection strings strictly targeting *_Test databases
    public const string PlatformDbTest = "Server=(localdb)\\MSSQLLocalDB;Database=PlatformDB_Test;Trusted_Connection=True;TrustServerCertificate=True;";
    public const string SecurityDbTest = "Server=(localdb)\\MSSQLLocalDB;Database=SecurityDB_Test;Trusted_Connection=True;TrustServerCertificate=True;";
    public const string DomainDbTest = "Server=(localdb)\\MSSQLLocalDB;Database=DomainDB_Test;Trusted_Connection=True;TrustServerCertificate=True;";

    // Tenants
    public static readonly Guid TenantAlphaId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid TenantBetaId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // Companies (Tenant Alpha has Company A & B; Tenant Beta has Company C)
    public static readonly Guid CompanyAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid CompanyBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid CompanyCId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    // Branches
    public static readonly Guid BranchA1Id = Guid.Parse("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1");
    public static readonly Guid BranchA2Id = Guid.Parse("a2a2a2a2-a2a2-a2a2-a2a2-a2a2a2a2a2a2");
    public static readonly Guid BranchB1Id = Guid.Parse("b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1");
    public static readonly Guid BranchB2Id = Guid.Parse("b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2");
    public static readonly Guid BranchC1Id = Guid.Parse("c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c1c1");

    // Category & Products
    public static readonly Guid CategoryAlphaId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid ProductAlphaId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static readonly Guid CategoryBetaId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid ProductBetaId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    // Users
    public static readonly Guid PlatformAdminId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    public static readonly Guid TenantAdminAlphaId = Guid.Parse("10101010-1010-1010-1010-101010101010");
    public static readonly Guid TenantAdminBetaId = Guid.Parse("20202020-2020-2020-2020-202020202020");

    public static readonly Guid TenantUserAlphaId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    public static readonly Guid CompanyUserAId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    public static readonly Guid BranchUserA1Id = Guid.Parse("14141414-1414-1414-1414-141414141414");
    public static readonly Guid MultiBranchUserId = Guid.Parse("15151515-1515-1515-1515-151515151515");
    public static readonly Guid MixedScopeUserId = Guid.Parse("16161616-1616-1616-1616-161616161616");
    public static readonly Guid ReadOnlyUserId = Guid.Parse("17171717-1717-1717-1717-171717171717");
}
