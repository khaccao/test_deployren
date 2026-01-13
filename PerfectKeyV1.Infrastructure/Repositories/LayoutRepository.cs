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
    public class LayoutRepository : ILayoutRepository
    {
        private readonly ApplicationDbContext _context;

        public LayoutRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        // Area methods
        public async Task<Area?> GetAreaByIdAsync(int id)
        {
            return await _context.Areas.FindAsync(id);
        }

        public async Task<Area?> GetAreaByGuidAsync(Guid guid)
        {
            return await _context.Areas.FirstOrDefaultAsync(a => a.Guid == guid);
        }

        public async Task<IEnumerable<Area>> GetAreasByHotelAsync(Guid hotelGuid)
        {
            return await _context.Areas
                .Where(a => a.HotelGuid == hotelGuid && a.IsActive)
                .OrderBy(a => a.AreaName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Area>> GetAreaTreeAsync(Guid hotelGuid)
        {
            return await _context.Areas
                .Where(a => a.HotelGuid == hotelGuid && a.IsActive)
                .Include(a => a.Children)
                .OrderBy(a => a.AreaName)
                .ToListAsync();
        }

        public async Task<Area> CreateAreaAsync(Area area)
        {
            _context.Areas.Add(area);
            await _context.SaveChangesAsync();
            return area;
        }

        public async Task UpdateAreaAsync(Area area)
        {
            _context.Areas.Update(area);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAreaAsync(Area area)
        {
            _context.Areas.Remove(area);
            await _context.SaveChangesAsync();
        }

        // Element methods
        public async Task<Element?> GetElementByIdAsync(int id)
        {
            return await _context.Elements.FindAsync(id);
        }

        public async Task<Element?> GetElementByGuidAsync(Guid guid)
        {
            return await _context.Elements.FirstOrDefaultAsync(e => e.Guid == guid);
        }

        public async Task<IEnumerable<Element>> GetElementsByAreaAsync(Guid areaGuid)
        {
            return await _context.Elements
                .Where(e => e.AreaGuid == areaGuid && e.IsActive)
                .OrderBy(e => e.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Element>> GetElementsByHotelAsync(Guid hotelGuid)
        {
            return await _context.Elements
                .Where(e => e.HotelGuid == hotelGuid && e.IsActive)
                .Include(e => e.Area)
                .OrderBy(e => e.Name)
                .ToListAsync();
        }

        public async Task<Element> CreateElementAsync(Element element)
        {
            _context.Elements.Add(element);
            await _context.SaveChangesAsync();
            return element;
        }

        public async Task UpdateElementAsync(Element element)
        {
            _context.Elements.Update(element);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteElementAsync(Element element)
        {
            _context.Elements.Remove(element);
            await _context.SaveChangesAsync();
        }

        // ElementElementType methods
        public async Task CreateElementElementTypeRelationshipsAsync(int elementId, List<Guid> elementTypeGuids)
        {
            foreach (var elementTypeGuid in elementTypeGuids)
            {
                var elementType = await _context.ElementTypes.FirstOrDefaultAsync(et => et.Guid == elementTypeGuid);
                if (elementType != null)
                {
                    var relationship = new ElementElementType
                    {
                        ElementId = elementId,
                        ElementTypeId = elementType.Id,
                        CreateDate = DateTime.UtcNow
                    };
                    _context.ElementElementTypes.Add(relationship);
                }
            }
            await _context.SaveChangesAsync();
        }

        public async Task UpdateElementElementTypeRelationshipsAsync(int elementId, List<Guid> elementTypeGuids)
        {
            // Remove existing relationships
            var existingRelationships = _context.ElementElementTypes.Where(eet => eet.ElementId == elementId);
            _context.ElementElementTypes.RemoveRange(existingRelationships);
            await _context.SaveChangesAsync();

            // Add new relationships
            await CreateElementElementTypeRelationshipsAsync(elementId, elementTypeGuids);
        }

        public async Task<IEnumerable<ElementType>> GetElementTypesByElementIdAsync(int elementId)
        {
            return await _context.ElementElementTypes
                .Where(eet => eet.ElementId == elementId)
                .Include(eet => eet.ElementType)
                .Select(eet => eet.ElementType)
                .ToListAsync();
        }
    }
}