using PerfectKeyV1.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerfectKeyV1.Application.Interfaces
{
    public interface IElementTypeRepository
    {
        Task<ElementType?> GetByIdAsync(int id);
        Task<ElementType?> GetByGuidAsync(Guid guid);
        Task<IEnumerable<ElementType>> GetAllAsync();
        Task<ElementType> CreateAsync(ElementType elementType);
        Task<ElementType> UpdateAsync(ElementType elementType);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsByNameAsync(string name, Guid? excludeGuid = null);
    }
}