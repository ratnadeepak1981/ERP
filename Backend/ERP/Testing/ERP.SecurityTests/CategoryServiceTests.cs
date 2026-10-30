using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ERP.Domain.Infrastructure.Persistence;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Infrastructure.Persistence;

namespace ERP.SecurityTests
{
    public class CategoryServiceTests
    {
        private DomainDbContext _context;
        private ICategoryRepository _repository;
        private ICategoryService _service;
        private Guid _tenantId;
        private Guid _otherTenantId;

        public CategoryServiceTests()
        {
            var options = new DbContextOptionsBuilder<DomainDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new DomainDbContext(options);
            _repository = new CategoryRepository(_context);
            _service = new CategoryService(_repository);
            _tenantId = Guid.NewGuid();
            _otherTenantId = Guid.NewGuid();
        }

        private async Task SeedCategory(Guid tenantId, Guid? parentId = null, bool isActive = true)
        {
            var cat = Category.Create(tenantId, Guid.NewGuid().ToString(), "Name" + Guid.NewGuid(), null);
            cat.SetParent(parentId);
            if (!isActive) cat.Deactivate();
            await _repository.AddAsync(cat);
            await _repository.SaveChangesAsync();
        }

        [Fact]
        public async Task CreateRootCategory_Succeeds()
        {
            var request = new CreateCategoryRequest
            {
                CategoryCode = "C001",
                CategoryName = "Root",
                Description = "Root category",
                ParentCategoryId = null
            };
            var result = await _service.CreateCategoryAsync(_tenantId, request);
            Assert.NotNull(result);
            Assert.Equal(request.CategoryCode, result.CategoryCode);
            Assert.Null(result.ParentCategoryId);
        }

        [Fact]
        public async Task CreateChildCategory_ValidParent_Succeeds()
        {
            var parent = Category.Create(_tenantId, "P001", "Parent", null);
            await _repository.AddAsync(parent);
            await _repository.SaveChangesAsync();

            var request = new CreateCategoryRequest
            {
                CategoryCode = "C002",
                CategoryName = "Child",
                ParentCategoryId = parent.CategoryId
            };
            var child = await _service.CreateCategoryAsync(_tenantId, request);
            Assert.NotNull(child);
            Assert.Equal(parent.CategoryId, child.ParentCategoryId);
        }

        [Fact]
        public async Task CreateCategory_WithParentFromOtherTenant_Throws()
        {
            var parent = Category.Create(_otherTenantId, "P001", "ParentOther", null);
            _context.Categories.Add(parent);
            _context.SaveChanges();

            var request = new CreateCategoryRequest
            {
                CategoryCode = "C003",
                CategoryName = "Invalid",
                ParentCategoryId = parent.CategoryId
            };
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.CreateCategoryAsync(_tenantId, request));
            Assert.Contains("does not exist for this tenant", ex.Message);
        }

        [Fact]
        public async Task UpdateCategory_SelfParenting_Throws()
        {
            var cat = Category.Create(_tenantId, "C004", "Self", null);
            await _repository.AddAsync(cat);
            await _repository.SaveChangesAsync();

            var request = new UpdateCategoryRequest
            {
                CategoryName = "SelfUpdated",
                ParentCategoryId = cat.CategoryId
            };
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.UpdateCategoryAsync(_tenantId, cat.CategoryId, request));
            Assert.Contains("cannot be its own parent", ex.Message);
        }

        [Fact]
        public async Task UpdateCategory_CycleDetection_Throws()
        {
            var a = Category.Create(_tenantId, "A", "A", null);
            var b = Category.Create(_tenantId, "B", "B", null);
            await _repository.AddAsync(a);
            await _repository.AddAsync(b);
            await _repository.SaveChangesAsync();

            b.SetParent(a.CategoryId);
            await _repository.SaveChangesAsync();

            var request = new UpdateCategoryRequest
            {
                CategoryName = "A",
                ParentCategoryId = b.CategoryId
            };
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.UpdateCategoryAsync(_tenantId, a.CategoryId, request));
            Assert.Contains("Circular parent relationship", ex.Message);
        }

        [Fact]
        public async Task UpdateCategory_MoveToRoot_Succeeds()
        {
            var parent = Category.Create(_tenantId, "P002", "Parent2", null);
            var child = Category.Create(_tenantId, "C005", "Child2", null);
            child.SetParent(parent.CategoryId);
            await _repository.AddAsync(parent);
            await _repository.AddAsync(child);
            await _repository.SaveChangesAsync();

            var request = new UpdateCategoryRequest
            {
                CategoryName = "Child2",
                ParentCategoryId = null
            };
            var updated = await _service.UpdateCategoryAsync(_tenantId, child.CategoryId, request);
            Assert.Null(updated.ParentCategoryId);
        }
    }
}

using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Common.Scope;

namespace ERP.SecurityTests
{
    [TestFixture]
    public class CategoryServiceTests
    {
        private DomainDbContext _context;
        private ICategoryRepository _repository;
        private ICategoryService _service;
        private Guid _tenantId;
        private Guid _otherTenantId;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<DomainDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new DomainDbContext(options);
            _repository = new CategoryRepository(_context);
            _service = new CategoryService(_repository);
            _tenantId = Guid.NewGuid();
            _otherTenantId = Guid.NewGuid();
        }

        private async Task SeedCategory(Guid tenantId, Guid? parentId = null, bool isActive = true)
        {
            var cat = Category.Create(tenantId, Guid.NewGuid().ToString(), "Name" + Guid.NewGuid(), null);
            cat.SetParent(parentId);
            if (!isActive) cat.Deactivate();
            await _repository.AddAsync(cat);
            await _repository.SaveChangesAsync();
        }

        [Test]
        public async Task CreateRootCategory_Succeeds()
        {
            var request = new CreateCategoryRequest
            {
                CategoryCode = "C001",
                CategoryName = "Root",
                Description = "Root category",
                ParentCategoryId = null
            };
            var result = await _service.CreateCategoryAsync(_tenantId, request);
            Assert.NotNull(result);
            Assert.AreEqual(request.CategoryCode, result.CategoryCode);
            Assert.IsNull(result.ParentCategoryId);
        }

        [Test]
        public async Task CreateChildCategory_ValidParent_Succeeds()
        {
            // parent
            var parent = Category.Create(_tenantId, "P001", "Parent", null);
            await _repository.AddAsync(parent);
            await _repository.SaveChangesAsync();

            var request = new CreateCategoryRequest
            {
                CategoryCode = "C002",
                CategoryName = "Child",
                ParentCategoryId = parent.CategoryId
            };
            var child = await _service.CreateCategoryAsync(_tenantId, request);
            Assert.NotNull(child);
            Assert.AreEqual(parent.CategoryId, child.ParentCategoryId);
        }

        [Test]
        public void CreateCategory_WithParentFromOtherTenant_Throws()
        {
            // parent from other tenant
            var parent = Category.Create(_otherTenantId, "P001", "ParentOther", null);
            _context.Categories.Add(parent);
            _context.SaveChanges();

            var request = new CreateCategoryRequest
            {
                CategoryCode = "C003",
                CategoryName = "Invalid",
                ParentCategoryId = parent.CategoryId
            };
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.CreateCategoryAsync(_tenantId, request));
            StringAssert.Contains("does not exist for this tenant", ex.Message);
        }

        [Test]
        public async Task UpdateCategory_SelfParenting_Throws()
        {
            var cat = Category.Create(_tenantId, "C004", "Self", null);
            await _repository.AddAsync(cat);
            await _repository.SaveChangesAsync();

            var request = new UpdateCategoryRequest
            {
                CategoryName = "SelfUpdated",
                ParentCategoryId = cat.CategoryId
            };
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.UpdateCategoryAsync(_tenantId, cat.CategoryId, request));
            StringAssert.Contains("cannot be its own parent", ex.Message);
        }

        [Test]
        public async Task UpdateCategory_CycleDetection_Throws()
        {
            // A -> B
            var a = Category.Create(_tenantId, "A", "A", null);
            var b = Category.Create(_tenantId, "B", "B", null);
            await _repository.AddAsync(a);
            await _repository.AddAsync(b);
            await _repository.SaveChangesAsync();
            // set B parent to A
            b.SetParent(a.CategoryId);
            await _repository.SaveChangesAsync();

            // attempt to set A parent to B, creating a cycle
            var request = new UpdateCategoryRequest
            {
                CategoryName = "A",
                ParentCategoryId = b.CategoryId
            };
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.UpdateCategoryAsync(_tenantId, a.CategoryId, request));
            StringAssert.Contains("Circular parent relationship", ex.Message);
        }

        [Test]
        public async Task UpdateCategory_MoveToRoot_Succeeds()
        {
            var parent = Category.Create(_tenantId, "P002", "Parent2", null);
            var child = Category.Create(_tenantId, "C005", "Child2", null);
            child.SetParent(parent.CategoryId);
            await _repository.AddAsync(parent);
            await _repository.AddAsync(child);
            await _repository.SaveChangesAsync();

            var request = new UpdateCategoryRequest
            {
                CategoryName = "Child2",
                ParentCategoryId = null
            };
            var updated = await _service.UpdateCategoryAsync(_tenantId, child.CategoryId, request);
            Assert.IsNull(updated.ParentCategoryId);
        }
    }
}
