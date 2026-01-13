using PerfectKeyV1.Application.DTOs.Layout;
using PerfectKeyV1.Application.Interfaces;
using PerfectKeyV1.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerfectKeyV1.Application.Services
{
    public class ElementTypeService : IElementTypeService
    {
        private readonly IElementTypeRepository _elementTypeRepo;

        public ElementTypeService(IElementTypeRepository elementTypeRepo)
        {
            _elementTypeRepo = elementTypeRepo;
        }

        public async Task<ElementTypeDto?> GetElementTypeByGuidAsync(Guid guid)
        {
            var elementType = await _elementTypeRepo.GetByGuidAsync(guid);
            return elementType != null ? MapToElementTypeDto(elementType) : null;
        }

        public async Task<IEnumerable<ElementTypeDto>> GetAllElementTypesAsync()
        {
            var elementTypes = await _elementTypeRepo.GetAllAsync();
            return elementTypes.Select(MapToElementTypeDto);
        }

        public async Task<ElementTypeDto> CreateElementTypeAsync(CreateElementTypeRequest request)
        {
            // Check if name already exists
            if (await _elementTypeRepo.ExistsByNameAsync(request.Name))
            {
                throw new Exception("Element type with this name already exists");
            }

            var elementType = new ElementType
            {
                Guid = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                Color = request.Color,
                Icon = request.Icon,
                IsActive = true,
                CreateDate = DateTime.UtcNow
            };

            var created = await _elementTypeRepo.CreateAsync(elementType);
            return MapToElementTypeDto(created);
        }

        public async Task<ElementTypeDto> UpdateElementTypeAsync(Guid guid, UpdateElementTypeRequest request)
        {
            var existingElementType = await _elementTypeRepo.GetByGuidAsync(guid);
            if (existingElementType == null)
            {
                throw new Exception("Element type not found");
            }

            // Check if name already exists (excluding current)
            if (await _elementTypeRepo.ExistsByNameAsync(request.Name, guid))
            {
                throw new Exception("Element type with this name already exists");
            }

            existingElementType.Name = request.Name;
            existingElementType.Description = request.Description;
            existingElementType.Color = request.Color;
            existingElementType.Icon = request.Icon;
            existingElementType.IsActive = request.IsActive;

            var updated = await _elementTypeRepo.UpdateAsync(existingElementType);
            return MapToElementTypeDto(updated);
        }

        public async Task<bool> DeleteElementTypeAsync(Guid guid)
        {
            var elementType = await _elementTypeRepo.GetByGuidAsync(guid);
            if (elementType == null)
            {
                return false;
            }

            // Note: We use soft delete by setting IsActive = false
            return await _elementTypeRepo.DeleteAsync(elementType.Id);
        }

        private ElementTypeDto MapToElementTypeDto(ElementType elementType)
        {
            return new ElementTypeDto
            {
                Id = elementType.Id,
                Guid = elementType.Guid,
                Name = elementType.Name,
                Description = elementType.Description,
                Color = elementType.Color,
                Icon = elementType.Icon,
                IsActive = elementType.IsActive,
                CreateDate = elementType.CreateDate,
                LastModify = elementType.LastModify
            };
        }
    }
}