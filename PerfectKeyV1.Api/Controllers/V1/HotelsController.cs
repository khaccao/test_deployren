using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PerfectKeyV1.Application.DTOs.Hotel;
using PerfectKeyV1.Application.Interfaces;
using PerfectKeyV1.Application.Services;
using PerfectKeyV1.Domain.Entities;

namespace PerfectKeyV1.Api.Controllers.V1
{
    /// <summary>
    /// Controller quản lý khách sạn
    /// </summary>
    [ApiController]
    [Route("api/v1/hotels")]
    public class HotelsController : ControllerBase
    {
        private readonly IHotelService _hotelService;

        public HotelsController(IHotelService hotelService)
        {
            _hotelService = hotelService;
        }

        /// <summary>
        /// Lấy danh sách tất cả khách sạn
        /// </summary>
        /// <returns>Danh sách khách sạn</returns>
        /// <response code="200">Lấy danh sách thành công</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<HotelDto>), 200)]
        public async Task<ActionResult<IEnumerable<HotelDto>>> GetAllHotels()
        {
            try
            {
                var hotels = await _hotelService.GetAllHotelsAsync();
                var hotelDtos = hotels.Select(h => new HotelDto
                {
                    Id = h.Id,
                    Guid = h.Guid,
                    Code = h.Code,
                    HotelName = h.HotelName,
                    Note = h.Note,
                    DBName = h.DBName,
                    IPAddress = h.IPAddress,
                    ISS_DBName = h.ISS_DBName,
                    ISS_IPAddress = h.ISS_IPAddress,
                    PKMTablet = h.PKMTablet,
                    IP_VPN_FO = h.IP_VPN_FO,
                    IP_VPN_ISS = h.IP_VPN_ISS,
                    IPLAN_Server = h.IPLAN_Server,
                    Email = h.Email,
                    IsDeleted = h.IsDeleted,
                    IsAutoUpdateOTA = h.IsAutoUpdateOTA,
                    OTATimesAuto = h.OTATimesAuto,
                    HotelAvatarUrl = h.HotelAvatarUrl,
                    StartDate = h.StartDate,
                    EndDate = h.EndDate,
                    CreateDate = h.CreateDate
                });
                return Ok(hotelDtos);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}