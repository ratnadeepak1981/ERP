using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Branch
{
    public class Branch
    {
        public Guid Id { get; set; }

        public Guid CompanyId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
