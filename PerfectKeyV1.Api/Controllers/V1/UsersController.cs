using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PerfectKeyV1.Application.DTOs.Auth;
using PerfectKeyV1.Application.DTOs.Users;
using PerfectKeyV1.Application.Interfaces;
using PerfectKeyV1.Application.Services;
using PerfectKeyV1.Domain.Entities;
using System.Security.Claims;

namespace PerfectKeyV1.Api.Controllers.V1
{
    /// <summary>
    /// Controller quản lý người dùng
    /// </summary>
    [ApiController]
    [Route("api/v1/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;

        public UsersController(IUserService userService, IAuthService authService)
        {
            _userService = userService;
            _authService = authService;
        }

        /// <summary>
        /// Tạo mới người dùng
        /// </summary>
        /// <param name="request">Thông tin tạo người dùng</param>
        /// <returns>Thông tin người dùng đã tạo</returns>
        /// <response code="200">Tạo người dùng thành công</response>
        /// <response code="400">Dữ liệu không hợp lệ</response>
        [HttpPost]
        [ProducesResponseType(typeof(User), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<User>> CreateUser(CreateUserRequest request)
        {
            try
            {
                var user = await _userService.CreateUserAsync(request);
                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách tất cả người dùng
        /// </summary>
        /// <returns>Danh sách người dùng</returns>
        /// <response code="200">Lấy danh sách thành công</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<User>), 200)]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }

        /// <summary>
        /// Lấy thông tin người dùng theo ID
        /// </summary>
        /// <param name="id">ID người dùng</param>
        /// <returns>Thông tin chi tiết người dùng</returns>
        /// <response code="200">Lấy thông tin thành công</response>
        /// <response code="404">Không tìm thấy người dùng</response>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(User), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound();

            return Ok(user);
        }

        /// <summary>
        /// Lấy thông tin người dùng theo GUID
        /// </summary>
        /// <param name="guid">GUID người dùng</param>
        /// <returns>Thông tin chi tiết người dùng</returns>
        /// <response code="200">Lấy thông tin thành công</response>
        /// <response code="404">Không tìm thấy người dùng</response>
        [HttpGet("guid/{guid}")]
        [ProducesResponseType(typeof(User), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<User>> GetUserByGuid(Guid guid)
        {
            var user = await _userService.GetUserByGuidAsync(guid);
            if (user == null)
                return NotFound();

            return Ok(user);
        }

        /// <summary>
        /// Cập nhật thông tin người dùng
        /// </summary>
        /// <param name="id">ID người dùng cần cập nhật</param>
        /// <param name="request">Thông tin cập nhật</param>
        /// <returns>Thông tin người dùng đã cập nhật</returns>
        /// <response code="200">Cập nhật thành công</response>
        /// <response code="400">Dữ liệu không hợp lệ</response>
        /// <response code="404">Không tìm thấy người dùng</response>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(User), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<User>> UpdateUser(int id, UpdateUserRequest request)
        {
            try
            {
                var user = await _userService.UpdateUserAsync(id, request);
                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Xóa người dùng
        /// </summary>
        /// <param name="id">ID người dùng cần xóa</param>
        /// <returns>Kết quả xóa</returns>
        /// <response code="200">Xóa thành công</response>
        /// <response code="400">Không thể xóa người dùng</response>
        /// <response code="404">Không tìm thấy người dùng</response>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                await _userService.DeleteUserAsync(id);
                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Duyệt người dùng (chỉ superadmin)
        /// </summary>
        /// <param name="id">ID người dùng cần duyệt</param>
        /// <returns>Kết quả duyệt</returns>
        /// <response code="200">Duyệt thành công</response>
        /// <response code="400">Không thể duyệt người dùng</response>
        /// <response code="404">Không tìm thấy người dùng</response>
        [HttpPut("{id:int}/approve")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ApproveUser(int id)
        {
            try
            {
                await _userService.ApproveUserAsync(id);
                return Ok(new { message = "User approved successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Gán danh sách khách sạn cho người dùng
        /// </summary>
        /// <param name="id">ID người dùng</param>
        /// <param name="hotelCodes">Danh sách mã khách sạn</param>
        /// <returns>Kết quả gán khách sạn</returns>
        /// <response code="200">Gán khách sạn thành công</response>
        /// <response code="400">Dữ liệu không hợp lệ</response>
        /// <response code="404">Không tìm thấy người dùng</response>
        [HttpPost("{id:int}/hotels")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> AssignHotels(int id, [FromBody] List<string> hotelCodes)
        {
            try
            {
                await _userService.AssignHotelsAsync(id, hotelCodes);
                return Ok(new { message = "Hotels assigned successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách khách sạn của người dùng
        /// </summary>
        /// <param name="id">ID người dùng</param>
        /// <returns>Danh sách khách sạn</returns>
        /// <response code="200">Lấy danh sách thành công</response>
        /// <response code="404">Không tìm thấy người dùng</response>
        [HttpGet("{id:int}/hotels")]
        [ProducesResponseType(typeof(IEnumerable<Hotel>), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<IEnumerable<Hotel>>> GetUserHotels(int id)
        {
            var hotels = await _userService.GetUserHotelsAsync(id);
            return Ok(hotels);
        }

        /// <summary>
        /// Lấy danh sách nhân viên theo mã khách sạn
        /// </summary>
        /// <param name="hotelCode">Mã khách sạn</param>
        /// <returns>Danh sách nhân viên của khách sạn</returns>
        /// <response code="200">Lấy danh sách thành công</response>
        /// <response code="401">Không có quyền truy cập</response>
        /// <response code="404">Không tìm thấy khách sạn</response>
        [HttpGet("by-hotel")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(typeof(IEnumerable<PerfectKeyV1.Application.DTOs.Users.UserDto>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<IEnumerable<PerfectKeyV1.Application.DTOs.Users.UserDto>>> GetUsersByHotelCode([FromQuery] string hotelCode)
        {
            try
            {
                var users = await _userService.GetUsersByHotelCodeAsync(hotelCode);
                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách nhân viên của khách sạn mà Hotel Admin đang quản lý
        /// </summary>
        /// <returns>Danh sách nhân viên của khách sạn</returns>
        /// <response code="200">Lấy danh sách thành công</response>
        /// <response code="401">Không có quyền truy cập</response>
        /// <response code="403">Không phải Hotel Admin</response>
        [HttpGet("my-hotel")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(typeof(IEnumerable<PerfectKeyV1.Application.DTOs.Users.UserDto>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<IEnumerable<PerfectKeyV1.Application.DTOs.Users.UserDto>>> GetMyHotelUsers()
        {
            try
            {
                var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || string.IsNullOrEmpty(userIdClaim.Value) || !int.TryParse(userIdClaim.Value, out var currentUserId))
                    return Unauthorized(new { message = "Invalid user token" });

                var users = await _userService.GetMyHotelUsersAsync(currentUserId);
                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách tất cả Hotel Admin (chỉ Super Admin)
        /// </summary>
        /// <returns>Danh sách Hotel Admin</returns>
        /// <response code="200">Lấy danh sách thành công</response>
        /// <response code="401">Không có quyền truy cập</response>
        /// <response code="403">Không phải Super Admin</response>
        [HttpGet("hotel-admins")]
        [Authorize(Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(IEnumerable<PerfectKeyV1.Application.DTOs.Users.UserDto>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<IEnumerable<PerfectKeyV1.Application.DTOs.Users.UserDto>>> GetAllHotelAdmins()
        {
            try
            {
                var hotelAdmins = await _userService.GetAllHotelAdminsAsync();
                return Ok(hotelAdmins);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách user thuộc khách sạn mà Hotel Admin quản lý
        /// </summary>
        /// <returns>Danh sách user của khách sạn</returns>
        /// <response code="200">Lấy danh sách thành công</response>
        /// <response code="401">Không có quyền truy cập</response>
        /// <response code="403">Không phải Hotel Admin</response>
        [HttpGet("hotel-users")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(typeof(IEnumerable<PerfectKeyV1.Application.DTOs.Users.UserDto>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<IEnumerable<PerfectKeyV1.Application.DTOs.Users.UserDto>>> GetHotelUsersForAdmin()
        {
            try
            {
                var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || string.IsNullOrEmpty(userIdClaim.Value) || !int.TryParse(userIdClaim.Value, out var currentUserId))
                    return Unauthorized(new { message = "Invalid user token" });

                var users = await _userService.GetHotelUsersForAdminAsync(currentUserId);
                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Super Admin bật 2FA cho user
        /// </summary>
        /// <param name="targetUserId">ID của user cần bật 2FA</param>
        /// <returns>Kết quả setup 2FA</returns>
        /// <response code="200">Setup thành công</response>
        /// <response code="401">Không có quyền truy cập</response>
        /// <response code="403">Không phải Super Admin</response>
        [HttpPost("{targetUserId:int}/enable-2fa")]
        [Authorize(Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(TwoFactorSetupResponse), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<TwoFactorSetupResponse>> AdminEnableTwoFactorForUser(int targetUserId)
        {
            try
            {
                var result = await _authService.AdminEnableTwoFactorForUserAsync(targetUserId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Super Admin xác nhận bật 2FA cho user
        /// </summary>
        /// <param name="targetUserId">ID của user</param>
        /// <returns>Kết quả xác nhận</returns>
        /// <response code="200">Xác nhận thành công</response>
        /// <response code="401">Không có quyền truy cập</response>
        /// <response code="403">Không phải Super Admin</response>
        [HttpPost("{targetUserId:int}/confirm-2fa")]
        [Authorize(Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(AuthResponse), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<AuthResponse>> AdminConfirmTwoFactorForUser(int targetUserId)
        {
            try
            {
                var result = await _authService.AdminConfirmEnableTwoFactorForUserAsync(targetUserId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Super Admin tắt 2FA cho user
        /// </summary>
        /// <param name="targetUserId">ID của user cần tắt 2FA</param>
        /// <returns>Kết quả tắt 2FA</returns>
        /// <response code="200">Tắt 2FA thành công</response>
        /// <response code="401">Không có quyền truy cập</response>
        /// <response code="403">Không phải Super Admin</response>
        [HttpPost("{targetUserId:int}/disable-2fa")]
        [Authorize(Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(bool), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<bool>> AdminDisableTwoFactorForUser(int targetUserId)
        {
            try
            {
                var result = await _authService.AdminDisableTwoFactorForUserAsync(targetUserId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Super Admin lấy mã dự phòng của user
        /// </summary>
        /// <param name="targetUserId">ID của user</param>
        /// <returns>Danh sách mã dự phòng</returns>
        /// <response code="200">Lấy mã thành công</response>
        /// <response code="401">Không có quyền truy cập</response>
        /// <response code="403">Không phải Super Admin</response>
        [HttpGet("{targetUserId:int}/recovery-codes")]
        [Authorize(Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(List<string>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<List<string>>> AdminGetRecoveryCodesForUser(int targetUserId)
        {
            try
            {
                var recoveryCodes = await _authService.AdminGetRecoveryCodesForUserAsync(targetUserId);
                return Ok(recoveryCodes);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Super Admin tạo mã dự phòng mới cho user
        /// </summary>
        /// <param name="targetUserId">ID của user</param>
        /// <returns>Danh sách mã dự phòng mới</returns>
        /// <response code="200">Tạo mã thành công</response>
        /// <response code="401">Không có quyền truy cập</response>
        /// <response code="403">Không phải Super Admin</response>
        [HttpPost("{targetUserId:int}/generate-recovery-codes")]
        [Authorize(Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(List<string>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<List<string>>> AdminGenerateRecoveryCodesForUser(int targetUserId)
        {
            try
            {
                var recoveryCodes = await _authService.AdminGenerateRecoveryCodesForUserAsync(targetUserId);
                return Ok(recoveryCodes);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Super Admin tạo lại QR code cho user
        /// </summary>
        /// <param name="targetUserId">ID của user</param>
        /// <returns>Kết quả tạo QR code</returns>
        /// <response code="200">Tạo QR thành công</response>
        /// <response code="401">Không có quyền truy cập</response>
        /// <response code="403">Không phải Super Admin</response>
        [HttpPost("{targetUserId:int}/regenerate-qr")]
        [Authorize(Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(TwoFactorSetupResponse), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<TwoFactorSetupResponse>> AdminRegenerateQRForUser(int targetUserId)
        {
            try
            {
                var result = await _authService.AdminRegenerateQRForUserAsync(targetUserId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #region Private Helper Methods

        private async Task VerifyHotelAdminPermissionAsync(int adminUserId, int targetUserId)
        {
            var adminUser = await _userService.GetUserByIdAsync(adminUserId);
            if (adminUser == null || adminUser.UserType != Domain.Enums.UserType.HotelAdmin)
                throw new Exception("Only hotel admins can perform this action");

            // Get hotel users for this admin
            var hotelUsers = await _userService.GetHotelUsersForAdminAsync(adminUserId);
            var targetUser = hotelUsers.FirstOrDefault(u => u.Id == targetUserId);

            if (targetUser == null)
                throw new Exception("You don't have permission to manage this user");
        }

        #endregion
    }
}