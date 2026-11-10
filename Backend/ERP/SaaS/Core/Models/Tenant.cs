using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SaaS.Common;
namespace SaaS.Core.Models
{
    public class Tenant : Auditable
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Subdomain { get; set; } = string.Empty;

        public string? IndustryType { get; set; }

        public string CompanyStructure { get; set; } = "Single";

        public bool ECommerceEnabled { get; set; } = false;

        public string? AdminFullName { get; set; }

        public string? AdminEmail { get; set; }

        public string? AdminPhone { get; set; }

        public bool IsActive { get; set; } = true;

    }
}
