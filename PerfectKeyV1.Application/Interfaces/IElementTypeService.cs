using PerfectKeyV1.Application.DTOs.Layout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerfectKeyV1.Application.Interfaces
{
    public interface IElementTypeService
    {
        Task<ElementTypeDto?> GetElementTypeByGuidAsync(Guid guid);
        Task<IEnumerable<ElementTypeDto>> GetAllElementTypesAsync();
        Task<ElementTypeDto> CreateElementTypeAsync(CreateElementTypeRequest request);
        Task<ElementTypeDto> UpdateElementTypeAsync(Guid guid, UpdateElementTypeRequest request);
        Task<bool> DeleteElementTypeAsync(Guid guid);
    }
}