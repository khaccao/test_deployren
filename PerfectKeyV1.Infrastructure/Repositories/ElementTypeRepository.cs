using PerfectKeyV1.Application.Interfaces;
using PerfectKeyV1.Domain.Entities;
using PerfectKeyV1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerfectKeyV1.Infrastructure.Repositories
{
    public class ElementTypeRepository : IElementTypeRepository
    {
        private readonly ApplicationDbContext _context;

        public ElementTypeRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ElementType?> GetByIdAsync(int id)
        {
            return await _context.ElementTypes.FindAsync(id);
        }

        public async Task<ElementType?> GetByGuidAsync(Guid guid)
        {
            return await _context.ElementTypes
                .FirstOrDefaultAsync(et => et.Guid == guid);
        }

        public async Task<IEnumerable<ElementType>> GetAllAsync()
        {
            return await _context.ElementTypes
                .Where(et => et.IsActive)
                .OrderBy(et => et.Name)
                .ToListAsync();
        }

        public async Task<ElementType> CreateAsync(ElementType elementType)
        {
            _context.ElementTypes.Add(elementType);
            await _context.SaveChangesAsync();
            return elementType;
        }

        public async Task<ElementType> UpdateAsync(ElementType elementType)
        {
            elementType.LastModify = DateTime.UtcNow;
            _context.ElementTypes.Update(elementType);
            await _context.SaveChangesAsync();
            return elementType;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var elementType = await GetByIdAsync(id);
            if (elementType == null)
                return false;

            elementType.IsActive = false;
            elementType.LastModify = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsByNameAsync(string name, Guid? excludeGuid = null)
        {
            var query = _context.ElementTypes.Where(et => et.Name == name);
            if (excludeGuid.HasValue)
            {
                query = query.Where(et => et.Guid != excludeGuid.Value);
            }
            return await query.AnyAsync();
        }
    }
}