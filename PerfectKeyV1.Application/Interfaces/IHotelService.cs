using PerfectKeyV1.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerfectKeyV1.Application.Interfaces
{
    public interface IHotelService
    {
        Task<IEnumerable<Hotel>> GetAllHotelsAsync();
        Task<Hotel?> GetHotelByCodeAsync(string code);
        Task<Hotel?> GetHotelByIdAsync(int id);
    }
}