using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterDataFoundationAndAddressContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseLocations_WarehouseZones_WarehouseZoneId",
                table: "WarehouseLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseLocations_Warehouses_WarehouseId",
                table: "WarehouseLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseZones_Warehouses_WarehouseId",
                table: "WarehouseZones");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseZones_WarehouseId",
                table: "WarehouseZones");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseLocations_WarehouseId",
                table: "WarehouseLocations");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseLocations_WarehouseZoneId",
                table: "WarehouseLocations");

            migrationBuilder.DropIndex(
                name: "IX_Products_CategoryId",
                table: "Products");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LocationTypeId",
                table: "WarehouseLocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentLocationId",
                table: "WarehouseLocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanConsumeInMaintenance",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanConsumeInProduction",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStockTracked",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductType",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TrackingMode",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "UnitOfMeasureId",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_WarehouseZones_TenantId_WarehouseId_WarehouseZoneId",
                table: "WarehouseZones",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Warehouses_TenantId_WarehouseId",
                table: "Warehouses",
                columns: new[] { "TenantId", "WarehouseId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_WarehouseLocationId",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId", "WarehouseLocationId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Suppliers_TenantId_SupplierId",
                table: "Suppliers",
                columns: new[] { "TenantId", "SupplierId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Products_TenantId_ProductId",
                table: "Products",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Customers_TenantId_CustomerId",
                table: "Customers",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Companies_TenantId_Id",
                table: "Companies",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Categories_TenantId_CategoryId",
                table: "Categories",
                columns: new[] { "TenantId", "CategoryId" });

            migrationBuilder.CreateTable(
                name: "AddressTypes",
                columns: table => new
                {
                    AddressTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressTypes", x => x.AddressTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PreferredContactMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.ContactId);
                    table.UniqueConstraint("AK_Contacts_TenantId_ContactId", x => new { x.TenantId, x.ContactId });
                });

            migrationBuilder.CreateTable(
                name: "ContactTypes",
                columns: table => new
                {
                    ContactTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactTypes", x => x.ContactTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    CountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alpha2Code = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    Alpha3Code = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    NumericCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    CountryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OfficialName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.CountryId);
                });

            migrationBuilder.CreateTable(
                name: "LocationTypes",
                columns: table => new
                {
                    LocationTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CanStoreInventory = table.Column<bool>(type: "bit", nullable: false),
                    CanPick = table.Column<bool>(type: "bit", nullable: false),
                    CanPutAway = table.Column<bool>(type: "bit", nullable: false),
                    CanContainChildren = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationTypes", x => x.LocationTypeId);
                });

            migrationBuilder.CreateTable(
                name: "UnitsOfMeasure",
                columns: table => new
                {
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitsOfMeasure", x => x.UnitOfMeasureId);
                    table.UniqueConstraint("AK_UnitsOfMeasure_TenantId_UnitOfMeasureId", x => new { x.TenantId, x.UnitOfMeasureId });
                });

            migrationBuilder.CreateTable(
                name: "CustomerContacts",
                columns: table => new
                {
                    CustomerContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerContacts", x => x.CustomerContactId);
                    table.ForeignKey(
                        name: "FK_CustomerContacts_ContactTypes_ContactTypeId",
                        column: x => x.ContactTypeId,
                        principalTable: "ContactTypes",
                        principalColumn: "ContactTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerContacts_Contacts_TenantId_ContactId",
                        columns: x => new { x.TenantId, x.ContactId },
                        principalTable: "Contacts",
                        principalColumns: new[] { "TenantId", "ContactId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerContacts_Customers_TenantId_CustomerId",
                        columns: x => new { x.TenantId, x.CustomerId },
                        principalTable: "Customers",
                        principalColumns: new[] { "TenantId", "CustomerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierContacts",
                columns: table => new
                {
                    SupplierContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierContacts", x => x.SupplierContactId);
                    table.ForeignKey(
                        name: "FK_SupplierContacts_ContactTypes_ContactTypeId",
                        column: x => x.ContactTypeId,
                        principalTable: "ContactTypes",
                        principalColumn: "ContactTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierContacts_Contacts_TenantId_ContactId",
                        columns: x => new { x.TenantId, x.ContactId },
                        principalTable: "Contacts",
                        principalColumns: new[] { "TenantId", "ContactId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierContacts_Suppliers_TenantId_SupplierId",
                        columns: x => new { x.TenantId, x.SupplierId },
                        principalTable: "Suppliers",
                        principalColumns: new[] { "TenantId", "SupplierId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AddressLine1 = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    AddressLine2 = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StateProvince = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnmatchedCountryText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.AddressId);
                    table.UniqueConstraint("AK_Addresses_TenantId_AddressId", x => new { x.TenantId, x.AddressId });
                    table.ForeignKey(
                        name: "FK_Addresses_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "CountryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierProductPrices",
                columns: table => new
                {
                    SupplierProductPriceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierItemCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MinimumOrderQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: true),
                    IsPreferred = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierProductPrices", x => x.SupplierProductPriceId);
                    table.ForeignKey(
                        name: "FK_SupplierProductPrices_Products_TenantId_ProductId",
                        columns: x => new { x.TenantId, x.ProductId },
                        principalTable: "Products",
                        principalColumns: new[] { "TenantId", "ProductId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierProductPrices_Suppliers_TenantId_SupplierId",
                        columns: x => new { x.TenantId, x.SupplierId },
                        principalTable: "Suppliers",
                        principalColumns: new[] { "TenantId", "SupplierId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierProductPrices_UnitsOfMeasure_TenantId_UnitOfMeasureId",
                        columns: x => new { x.TenantId, x.UnitOfMeasureId },
                        principalTable: "UnitsOfMeasure",
                        principalColumns: new[] { "TenantId", "UnitOfMeasureId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitOfMeasureConversions",
                columns: table => new
                {
                    ConversionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitOfMeasureConversions", x => x.ConversionId);
                    table.ForeignKey(
                        name: "FK_UnitOfMeasureConversions_UnitsOfMeasure_TenantId_FromUnitId",
                        columns: x => new { x.TenantId, x.FromUnitId },
                        principalTable: "UnitsOfMeasure",
                        principalColumns: new[] { "TenantId", "UnitOfMeasureId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitOfMeasureConversions_UnitsOfMeasure_TenantId_ToUnitId",
                        columns: x => new { x.TenantId, x.ToUnitId },
                        principalTable: "UnitsOfMeasure",
                        principalColumns: new[] { "TenantId", "UnitOfMeasureId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BranchAddresses",
                columns: table => new
                {
                    BranchAddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchAddresses", x => x.BranchAddressId);
                    table.ForeignKey(
                        name: "FK_BranchAddresses_AddressTypes_AddressTypeId",
                        column: x => x.AddressTypeId,
                        principalTable: "AddressTypes",
                        principalColumn: "AddressTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchAddresses_Addresses_TenantId_AddressId",
                        columns: x => new { x.TenantId, x.AddressId },
                        principalTable: "Addresses",
                        principalColumns: new[] { "TenantId", "AddressId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchAddresses_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyAddresses",
                columns: table => new
                {
                    CompanyAddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyAddresses", x => x.CompanyAddressId);
                    table.ForeignKey(
                        name: "FK_CompanyAddresses_AddressTypes_AddressTypeId",
                        column: x => x.AddressTypeId,
                        principalTable: "AddressTypes",
                        principalColumn: "AddressTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyAddresses_Addresses_TenantId_AddressId",
                        columns: x => new { x.TenantId, x.AddressId },
                        principalTable: "Addresses",
                        principalColumns: new[] { "TenantId", "AddressId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyAddresses_Companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalTable: "Companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAddresses",
                columns: table => new
                {
                    CustomerAddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAddresses", x => x.CustomerAddressId);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_AddressTypes_AddressTypeId",
                        column: x => x.AddressTypeId,
                        principalTable: "AddressTypes",
                        principalColumn: "AddressTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Addresses_TenantId_AddressId",
                        columns: x => new { x.TenantId, x.AddressId },
                        principalTable: "Addresses",
                        principalColumns: new[] { "TenantId", "AddressId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Customers_TenantId_CustomerId",
                        columns: x => new { x.TenantId, x.CustomerId },
                        principalTable: "Customers",
                        principalColumns: new[] { "TenantId", "CustomerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierAddresses",
                columns: table => new
                {
                    SupplierAddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierAddresses", x => x.SupplierAddressId);
                    table.ForeignKey(
                        name: "FK_SupplierAddresses_AddressTypes_AddressTypeId",
                        column: x => x.AddressTypeId,
                        principalTable: "AddressTypes",
                        principalColumn: "AddressTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierAddresses_Addresses_TenantId_AddressId",
                        columns: x => new { x.TenantId, x.AddressId },
                        principalTable: "Addresses",
                        principalColumns: new[] { "TenantId", "AddressId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierAddresses_Suppliers_TenantId_SupplierId",
                        columns: x => new { x.TenantId, x.SupplierId },
                        principalTable: "Suppliers",
                        principalColumns: new[] { "TenantId", "SupplierId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseAddresses",
                columns: table => new
                {
                    WarehouseAddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseAddresses", x => x.WarehouseAddressId);
                    table.ForeignKey(
                        name: "FK_WarehouseAddresses_AddressTypes_AddressTypeId",
                        column: x => x.AddressTypeId,
                        principalTable: "AddressTypes",
                        principalColumn: "AddressTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseAddresses_Addresses_TenantId_AddressId",
                        columns: x => new { x.TenantId, x.AddressId },
                        principalTable: "Addresses",
                        principalColumns: new[] { "TenantId", "AddressId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseAddresses_Warehouses_TenantId_WarehouseId",
                        columns: x => new { x.TenantId, x.WarehouseId },
                        principalTable: "Warehouses",
                        principalColumns: new[] { "TenantId", "WarehouseId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseZones_TenantId_WarehouseId_WarehouseZoneId",
                table: "WarehouseZones",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseZones_TenantId_WarehouseZoneId",
                table: "WarehouseZones",
                columns: new[] { "TenantId", "WarehouseZoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_BranchId",
                table: "Warehouses",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_TenantId_CompanyId",
                table: "Warehouses",
                columns: new[] { "TenantId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_TenantId_WarehouseId",
                table: "Warehouses",
                columns: new[] { "TenantId", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_LocationTypeId",
                table: "WarehouseLocations",
                column: "LocationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_ParentLocationId",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId", "ParentLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_WarehouseLocationId",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId", "WarehouseLocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseLocationId",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseLocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_SupplierId",
                table: "Suppliers",
                columns: new[] { "TenantId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_CategoryId",
                table: "Products",
                columns: new[] { "TenantId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_ProductId",
                table: "Products",
                columns: new[] { "TenantId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_UnitOfMeasureId",
                table: "Products",
                columns: new[] { "TenantId", "UnitOfMeasureId" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_CustomerId",
                table: "Customers",
                columns: new[] { "TenantId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TenantId_Id",
                table: "Companies",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_TenantId_CategoryId",
                table: "Categories",
                columns: new[] { "TenantId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_CountryId",
                table: "Addresses",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_TenantId_AddressId",
                table: "Addresses",
                columns: new[] { "TenantId", "AddressId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AddressTypes_TenantId_Code",
                table: "AddressTypes",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BranchAddresses_AddressTypeId",
                table: "BranchAddresses",
                column: "AddressTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchAddresses_BranchId",
                table: "BranchAddresses",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchAddresses_TenantId_AddressId",
                table: "BranchAddresses",
                columns: new[] { "TenantId", "AddressId" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchAddresses_TenantId_BranchId_AddressId_AddressTypeId",
                table: "BranchAddresses",
                columns: new[] { "TenantId", "BranchId", "AddressId", "AddressTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchAddresses_TenantId_BranchId_AddressTypeId",
                table: "BranchAddresses",
                columns: new[] { "TenantId", "BranchId", "AddressTypeId" },
                unique: true,
                filter: "[IsDefault] = 1 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAddresses_AddressTypeId",
                table: "CompanyAddresses",
                column: "AddressTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAddresses_TenantId_AddressId",
                table: "CompanyAddresses",
                columns: new[] { "TenantId", "AddressId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAddresses_TenantId_CompanyId_AddressId_AddressTypeId",
                table: "CompanyAddresses",
                columns: new[] { "TenantId", "CompanyId", "AddressId", "AddressTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAddresses_TenantId_CompanyId_AddressTypeId",
                table: "CompanyAddresses",
                columns: new[] { "TenantId", "CompanyId", "AddressTypeId" },
                unique: true,
                filter: "[IsDefault] = 1 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_TenantId_ContactId",
                table: "Contacts",
                columns: new[] { "TenantId", "ContactId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContactTypes_TenantId_Code",
                table: "ContactTypes",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Countries_Alpha2Code",
                table: "Countries",
                column: "Alpha2Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Countries_Alpha3Code",
                table: "Countries",
                column: "Alpha3Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Countries_CountryName",
                table: "Countries",
                column: "CountryName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_AddressTypeId",
                table: "CustomerAddresses",
                column: "AddressTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_TenantId_AddressId",
                table: "CustomerAddresses",
                columns: new[] { "TenantId", "AddressId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_TenantId_CustomerId_AddressId_AddressTypeId",
                table: "CustomerAddresses",
                columns: new[] { "TenantId", "CustomerId", "AddressId", "AddressTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_TenantId_CustomerId_AddressTypeId",
                table: "CustomerAddresses",
                columns: new[] { "TenantId", "CustomerId", "AddressTypeId" },
                unique: true,
                filter: "[IsDefault] = 1 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_ContactTypeId",
                table: "CustomerContacts",
                column: "ContactTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_TenantId_ContactId",
                table: "CustomerContacts",
                columns: new[] { "TenantId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_TenantId_CustomerId_ContactId_ContactTypeId",
                table: "CustomerContacts",
                columns: new[] { "TenantId", "CustomerId", "ContactId", "ContactTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_TenantId_CustomerId_ContactTypeId",
                table: "CustomerContacts",
                columns: new[] { "TenantId", "CustomerId", "ContactTypeId" },
                unique: true,
                filter: "[IsPrimary] = 1 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_LocationTypes_TenantId_Code",
                table: "LocationTypes",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAddresses_AddressTypeId",
                table: "SupplierAddresses",
                column: "AddressTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAddresses_TenantId_AddressId",
                table: "SupplierAddresses",
                columns: new[] { "TenantId", "AddressId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAddresses_TenantId_SupplierId_AddressId_AddressTypeId",
                table: "SupplierAddresses",
                columns: new[] { "TenantId", "SupplierId", "AddressId", "AddressTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAddresses_TenantId_SupplierId_AddressTypeId",
                table: "SupplierAddresses",
                columns: new[] { "TenantId", "SupplierId", "AddressTypeId" },
                unique: true,
                filter: "[IsDefault] = 1 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContacts_ContactTypeId",
                table: "SupplierContacts",
                column: "ContactTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContacts_TenantId_ContactId",
                table: "SupplierContacts",
                columns: new[] { "TenantId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContacts_TenantId_SupplierId_ContactId_ContactTypeId",
                table: "SupplierContacts",
                columns: new[] { "TenantId", "SupplierId", "ContactId", "ContactTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContacts_TenantId_SupplierId_ContactTypeId",
                table: "SupplierContacts",
                columns: new[] { "TenantId", "SupplierId", "ContactTypeId" },
                unique: true,
                filter: "[IsPrimary] = 1 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductPrices_TenantId_ProductId",
                table: "SupplierProductPrices",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductPrices_TenantId_SupplierId_ProductId",
                table: "SupplierProductPrices",
                columns: new[] { "TenantId", "SupplierId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductPrices_TenantId_UnitOfMeasureId",
                table: "SupplierProductPrices",
                columns: new[] { "TenantId", "UnitOfMeasureId" });

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureConversions_TenantId_FromUnitId_ToUnitId",
                table: "UnitOfMeasureConversions",
                columns: new[] { "TenantId", "FromUnitId", "ToUnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureConversions_TenantId_ToUnitId",
                table: "UnitOfMeasureConversions",
                columns: new[] { "TenantId", "ToUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_TenantId_Code",
                table: "UnitsOfMeasure",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_TenantId_UnitOfMeasureId",
                table: "UnitsOfMeasure",
                columns: new[] { "TenantId", "UnitOfMeasureId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseAddresses_AddressTypeId",
                table: "WarehouseAddresses",
                column: "AddressTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseAddresses_TenantId_AddressId",
                table: "WarehouseAddresses",
                columns: new[] { "TenantId", "AddressId" });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseAddresses_TenantId_WarehouseId_AddressId_AddressTypeId",
                table: "WarehouseAddresses",
                columns: new[] { "TenantId", "WarehouseId", "AddressId", "AddressTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseAddresses_TenantId_WarehouseId_AddressTypeId",
                table: "WarehouseAddresses",
                columns: new[] { "TenantId", "WarehouseId", "AddressTypeId" },
                unique: true,
                filter: "[IsDefault] = 1 AND [IsActive] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_TenantId_CategoryId",
                table: "Products",
                columns: new[] { "TenantId", "CategoryId" },
                principalTable: "Categories",
                principalColumns: new[] { "TenantId", "CategoryId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_UnitsOfMeasure_TenantId_UnitOfMeasureId",
                table: "Products",
                columns: new[] { "TenantId", "UnitOfMeasureId" },
                principalTable: "UnitsOfMeasure",
                principalColumns: new[] { "TenantId", "UnitOfMeasureId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseLocations_LocationTypes_LocationTypeId",
                table: "WarehouseLocations",
                column: "LocationTypeId",
                principalTable: "LocationTypes",
                principalColumn: "LocationTypeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseLocations_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_ParentLocationId",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId", "ParentLocationId" },
                principalTable: "WarehouseLocations",
                principalColumns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId", "WarehouseLocationId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseLocations_WarehouseZones_TenantId_WarehouseId_WarehouseZoneId",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId" },
                principalTable: "WarehouseZones",
                principalColumns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseLocations_Warehouses_TenantId_WarehouseId",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId" },
                principalTable: "Warehouses",
                principalColumns: new[] { "TenantId", "WarehouseId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_Branches_BranchId",
                table: "Warehouses",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_Companies_TenantId_CompanyId",
                table: "Warehouses",
                columns: new[] { "TenantId", "CompanyId" },
                principalTable: "Companies",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseZones_Warehouses_TenantId_WarehouseId",
                table: "WarehouseZones",
                columns: new[] { "TenantId", "WarehouseId" },
                principalTable: "Warehouses",
                principalColumns: new[] { "TenantId", "WarehouseId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
-- Seed Countries
IF NOT EXISTS (SELECT 1 FROM Countries WHERE Alpha2Code = 'LK')
BEGIN
    INSERT INTO Countries (CountryId, Alpha2Code, Alpha3Code, NumericCode, CountryName, OfficialName, IsActive, CreatedAt, CreatedBy)
    VALUES 
    (NEWID(), 'LK', 'LKA', '144', 'Sri Lanka', 'Democratic Socialist Republic of Sri Lanka', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'US', 'USA', '840', 'United States', 'United States of America', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'GB', 'GBR', '826', 'United Kingdom', 'United Kingdom of Great Britain and Northern Ireland', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'IN', 'IND', '356', 'India', 'Republic of India', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'DE', 'DEU', '276', 'Germany', 'Federal Republic of Germany', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'SG', 'SGP', '702', 'Singapore', 'Republic of Singapore', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'AU', 'AUS', '036', 'Australia', 'Commonwealth of Australia', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'AE', 'ARE', '784', 'United Arab Emirates', 'United Arab Emirates', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'JP', 'JPN', '392', 'Japan', 'Japan', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'CN', 'CHN', '156', 'China', 'People''s Republic of China', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000');
END

-- Seed Global AddressTypes
IF NOT EXISTS (SELECT 1 FROM AddressTypes WHERE TenantId IS NULL AND Code = 'GENERAL')
BEGIN
    INSERT INTO AddressTypes (AddressTypeId, TenantId, Code, Name, Description, IsActive, CreatedAt, CreatedBy)
    VALUES
    ('11111111-0000-0000-0000-000000000001', NULL, 'GENERAL', 'General', 'General business address', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('11111111-0000-0000-0000-000000000002', NULL, 'REG_OFFICE', 'Registered Office', 'Official registered office', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('11111111-0000-0000-0000-000000000003', NULL, 'BILLING', 'Billing', 'Billing address for invoices', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('11111111-0000-0000-0000-000000000004', NULL, 'SHIPPING', 'Shipping', 'Shipping and delivery address', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('11111111-0000-0000-0000-000000000005', NULL, 'FACTORY', 'Factory', 'Manufacturing plant address', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('11111111-0000-0000-0000-000000000006', NULL, 'WAREHOUSE', 'Warehouse', 'Storage warehouse address', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('11111111-0000-0000-0000-000000000007', NULL, 'BRANCH', 'Branch', 'Branch office address', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('11111111-0000-0000-0000-000000000008', NULL, 'PROJECT_SITE', 'Project Site', 'Project operating site', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('11111111-0000-0000-0000-000000000009', NULL, 'OTHER', 'Other', 'Other address type', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000');
END

-- Seed Global ContactTypes
IF NOT EXISTS (SELECT 1 FROM ContactTypes WHERE TenantId IS NULL AND Code = 'GENERAL')
BEGIN
    INSERT INTO ContactTypes (ContactTypeId, TenantId, Code, Name, Description, IsActive, CreatedAt, CreatedBy)
    VALUES
    ('22222222-0000-0000-0000-000000000001', NULL, 'GENERAL', 'General', 'General contact', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('22222222-0000-0000-0000-000000000002', NULL, 'PURCHASING', 'Purchasing', 'Purchasing representative', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('22222222-0000-0000-0000-000000000003', NULL, 'SALES', 'Sales', 'Sales representative', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('22222222-0000-0000-0000-000000000004', NULL, 'ACCOUNTS', 'Accounts', 'Finance and accounting contact', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('22222222-0000-0000-0000-000000000005', NULL, 'WAREHOUSE', 'Warehouse', 'Warehouse contact', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('22222222-0000-0000-0000-000000000006', NULL, 'TECHNICAL', 'Technical', 'Technical contact', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000');
END

-- Seed Global LocationTypes
IF NOT EXISTS (SELECT 1 FROM LocationTypes WHERE TenantId IS NULL AND Code = 'RACK')
BEGIN
    INSERT INTO LocationTypes (LocationTypeId, TenantId, Code, Name, Description, CanStoreInventory, CanPick, CanPutAway, CanContainChildren, IsActive, CreatedAt, CreatedBy)
    VALUES
    ('33333333-0000-0000-0000-000000000001', NULL, 'RACK', 'Rack', 'Warehouse rack storage', 1, 1, 1, 1, 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('33333333-0000-0000-0000-000000000002', NULL, 'SHELF', 'Shelf', 'Shelf in a rack or bay', 1, 1, 1, 1, 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('33333333-0000-0000-0000-000000000003', NULL, 'BIN', 'Bin', 'Small parts bin', 1, 1, 1, 0, 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('33333333-0000-0000-0000-000000000004', NULL, 'FLOOR', 'Floor Location', 'Floor area storage', 1, 1, 1, 1, 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('33333333-0000-0000-0000-000000000005', NULL, 'STAGING', 'Staging Area', 'Staging and buffer area', 1, 1, 1, 1, 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('33333333-0000-0000-0000-000000000006', NULL, 'CUPBOARD', 'Cupboard', 'Enclosed cupboard or locker', 1, 1, 1, 1, 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000');
END

-- Backfill Product UOM
INSERT INTO UnitsOfMeasure (UnitOfMeasureId, TenantId, Code, Name, Description, IsActive, CreatedAt, CreatedBy)
SELECT NEWID(), p.TenantId, UPPER(LTRIM(RTRIM(p.UnitOfMeasure))), LTRIM(RTRIM(p.UnitOfMeasure)), 'Backfilled from existing products', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'
FROM Products p
WHERE p.UnitOfMeasure IS NOT NULL AND LEN(LTRIM(RTRIM(p.UnitOfMeasure))) > 0
  AND NOT EXISTS (
      SELECT 1 FROM UnitsOfMeasure u
      WHERE u.TenantId = p.TenantId AND u.Code = UPPER(LTRIM(RTRIM(p.UnitOfMeasure)))
  )
GROUP BY p.TenantId, UPPER(LTRIM(RTRIM(p.UnitOfMeasure))), LTRIM(RTRIM(p.UnitOfMeasure));

UPDATE p
SET p.UnitOfMeasureId = u.UnitOfMeasureId
FROM Products p
INNER JOIN UnitsOfMeasure u ON u.TenantId = p.TenantId AND u.Code = UPPER(LTRIM(RTRIM(p.UnitOfMeasure)))
WHERE p.UnitOfMeasureId IS NULL;

-- Backfill Customer Addresses
INSERT INTO Addresses (AddressId, TenantId, AddressName, AddressLine1, AddressLine2, City, StateProvince, PostalCode, CountryId, UnmatchedCountryText, IsActive, CreatedAt, CreatedBy)
SELECT 
    NEWID(),
    c.TenantId,
    c.CustomerName + ' - Default Address',
    c.AddressLine1,
    c.AddressLine2,
    c.City,
    NULL,
    c.PostalCode,
    cntry.CountryId,
    CASE WHEN cntry.CountryId IS NULL AND c.Country IS NOT NULL AND LEN(LTRIM(RTRIM(c.Country))) > 0 THEN LTRIM(RTRIM(c.Country)) ELSE NULL END,
    1,
    SYSUTCDATETIME(),
    '00000000-0000-0000-0000-000000000000'
FROM Customers c
LEFT JOIN Countries cntry ON (cntry.Alpha2Code = UPPER(LTRIM(RTRIM(c.Country))) OR cntry.Alpha3Code = UPPER(LTRIM(RTRIM(c.Country))) OR UPPER(cntry.CountryName) = UPPER(LTRIM(RTRIM(c.Country))))
WHERE c.AddressLine1 IS NOT NULL AND LEN(LTRIM(RTRIM(c.AddressLine1))) > 0
  AND NOT EXISTS (SELECT 1 FROM CustomerAddresses ca WHERE ca.CustomerId = c.CustomerId AND ca.TenantId = c.TenantId);

INSERT INTO CustomerAddresses (CustomerAddressId, TenantId, CustomerId, AddressId, AddressTypeId, IsDefault, IsActive, CreatedAt, CreatedBy)
SELECT 
    NEWID(),
    c.TenantId,
    c.CustomerId,
    a.AddressId,
    '11111111-0000-0000-0000-000000000001', -- GENERAL
    1,
    1,
    SYSUTCDATETIME(),
    '00000000-0000-0000-0000-000000000000'
FROM Customers c
INNER JOIN Addresses a ON a.TenantId = c.TenantId AND a.AddressName = (c.CustomerName + ' - Default Address')
WHERE NOT EXISTS (SELECT 1 FROM CustomerAddresses ca WHERE ca.CustomerId = c.CustomerId AND ca.TenantId = c.TenantId);

-- Backfill Customer Contacts
INSERT INTO Contacts (ContactId, TenantId, ContactName, JobTitle, Email, Phone, MobileNumber, PreferredContactMethod, IsActive, CreatedAt, CreatedBy)
SELECT 
    NEWID(),
    c.TenantId,
    CASE WHEN c.ContactPerson IS NOT NULL AND LEN(LTRIM(RTRIM(c.ContactPerson))) > 0 THEN LTRIM(RTRIM(c.ContactPerson)) ELSE c.CustomerName + ' Contact' END,
    NULL,
    c.Email,
    c.Phone,
    NULL,
    NULL,
    1,
    SYSUTCDATETIME(),
    '00000000-0000-0000-0000-000000000000'
FROM Customers c
WHERE (c.ContactPerson IS NOT NULL AND LEN(LTRIM(RTRIM(c.ContactPerson))) > 0)
   OR (c.Email IS NOT NULL AND LEN(LTRIM(RTRIM(c.Email))) > 0)
   OR (c.Phone IS NOT NULL AND LEN(LTRIM(RTRIM(c.Phone))) > 0)
  AND NOT EXISTS (SELECT 1 FROM CustomerContacts cc WHERE cc.CustomerId = c.CustomerId AND cc.TenantId = c.TenantId);

INSERT INTO CustomerContacts (CustomerContactId, TenantId, CustomerId, ContactId, ContactTypeId, IsPrimary, IsActive, CreatedAt, CreatedBy)
SELECT 
    NEWID(),
    c.TenantId,
    c.CustomerId,
    ct.ContactId,
    '22222222-0000-0000-0000-000000000001', -- GENERAL
    1,
    1,
    SYSUTCDATETIME(),
    '00000000-0000-0000-0000-000000000000'
FROM Customers c
INNER JOIN Contacts ct ON ct.TenantId = c.TenantId AND (ct.ContactName = LTRIM(RTRIM(c.ContactPerson)) OR ct.ContactName = (c.CustomerName + ' Contact'))
WHERE NOT EXISTS (SELECT 1 FROM CustomerContacts cc WHERE cc.CustomerId = c.CustomerId AND cc.TenantId = c.TenantId);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_TenantId_CategoryId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_UnitsOfMeasure_TenantId_UnitOfMeasureId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseLocations_LocationTypes_LocationTypeId",
                table: "WarehouseLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseLocations_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_ParentLocationId",
                table: "WarehouseLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseLocations_WarehouseZones_TenantId_WarehouseId_WarehouseZoneId",
                table: "WarehouseLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseLocations_Warehouses_TenantId_WarehouseId",
                table: "WarehouseLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_Branches_BranchId",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_Companies_TenantId_CompanyId",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseZones_Warehouses_TenantId_WarehouseId",
                table: "WarehouseZones");

            migrationBuilder.DropTable(
                name: "BranchAddresses");

            migrationBuilder.DropTable(
                name: "CompanyAddresses");

            migrationBuilder.DropTable(
                name: "CustomerAddresses");

            migrationBuilder.DropTable(
                name: "CustomerContacts");

            migrationBuilder.DropTable(
                name: "LocationTypes");

            migrationBuilder.DropTable(
                name: "SupplierAddresses");

            migrationBuilder.DropTable(
                name: "SupplierContacts");

            migrationBuilder.DropTable(
                name: "SupplierProductPrices");

            migrationBuilder.DropTable(
                name: "UnitOfMeasureConversions");

            migrationBuilder.DropTable(
                name: "WarehouseAddresses");

            migrationBuilder.DropTable(
                name: "ContactTypes");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropTable(
                name: "UnitsOfMeasure");

            migrationBuilder.DropTable(
                name: "AddressTypes");

            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.DropTable(
                name: "Countries");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_WarehouseZones_TenantId_WarehouseId_WarehouseZoneId",
                table: "WarehouseZones");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseZones_TenantId_WarehouseId_WarehouseZoneId",
                table: "WarehouseZones");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseZones_TenantId_WarehouseZoneId",
                table: "WarehouseZones");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Warehouses_TenantId_WarehouseId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_BranchId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_TenantId_CompanyId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_TenantId_WarehouseId",
                table: "Warehouses");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_WarehouseLocationId",
                table: "WarehouseLocations");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseLocations_LocationTypeId",
                table: "WarehouseLocations");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_ParentLocationId",
                table: "WarehouseLocations");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_WarehouseLocationId",
                table: "WarehouseLocations");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseLocationId",
                table: "WarehouseLocations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Suppliers_TenantId_SupplierId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_TenantId_SupplierId",
                table: "Suppliers");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Products_TenantId_ProductId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId_CategoryId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId_ProductId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId_UnitOfMeasureId",
                table: "Products");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Customers_TenantId_CustomerId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_CustomerId",
                table: "Customers");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Companies_TenantId_Id",
                table: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Companies_TenantId_Id",
                table: "Companies");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Categories_TenantId_CategoryId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_TenantId_CategoryId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "LocationTypeId",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "ParentLocationId",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "CanConsumeInMaintenance",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CanConsumeInProduction",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsStockTracked",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TrackingMode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasureId",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseZones_WarehouseId",
                table: "WarehouseZones",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_WarehouseId",
                table: "WarehouseLocations",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_WarehouseZoneId",
                table: "WarehouseLocations",
                column: "WarehouseZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "CategoryId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseLocations_WarehouseZones_WarehouseZoneId",
                table: "WarehouseLocations",
                column: "WarehouseZoneId",
                principalTable: "WarehouseZones",
                principalColumn: "WarehouseZoneId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseLocations_Warehouses_WarehouseId",
                table: "WarehouseLocations",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "WarehouseId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseZones_Warehouses_WarehouseId",
                table: "WarehouseZones",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "WarehouseId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
