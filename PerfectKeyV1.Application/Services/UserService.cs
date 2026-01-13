using Microsoft.Extensions.Caching.Distributed;
using PerfectKeyV1.Application.Common;
using PerfectKeyV1.Application.DTOs.Hotel;
using PerfectKeyV1.Application.DTOs.Users;
using PerfectKeyV1.Application.Interfaces;
using PerfectKeyV1.Domain.Entities;
using PerfectKeyV1.Domain.Enums;

namespace PerfectKeyV1.Application.Services
{
    public interface IUserService
    {
        Task<UserDto> CreateUserAsync(CreateUserRequest request);
        Task<UserDto?> GetUserByIdAsync(int id);
        Task<UserDto?> GetUserByGuidAsync(Guid guid);
        Task<IEnumerable<UserDto>> GetAllUsersAsync();
        Task<UserDto> UpdateUserAsync(int userId, UpdateUserRequest request);
        Task DeleteUserAsync(int userId);
        Task ApproveUserAsync(int userId);
        Task AssignHotelsAsync(int userId, List<string> hotelCodes);
        Task<IEnumerable<HotelDto>> GetUserHotelsAsync(int userId);
        Task<IEnumerable<UserDto>> GetUsersByHotelCodeAsync(string hotelCode);
        Task<IEnumerable<UserDto>> GetMyHotelUsersAsync(int currentUserId);
        Task<IEnumerable<UserDto>> GetAllHotelAdminsAsync();
        Task<IEnumerable<UserDto>> GetHotelUsersForAdminAsync(int adminUserId);
    }

    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepo;
        private readonly IHotelRepository _hotelRepo;
        private readonly IUserHotelRepository _userHotelRepo;
        private readonly IDistributedCache _cache;

        public UserService(
            IUserRepository userRepo,
            IHotelRepository hotelRepo,
            IUserHotelRepository userHotelRepo,
            IDistributedCache cache)
        {
            _userRepo = userRepo;
            _hotelRepo = hotelRepo;
            _userHotelRepo = userHotelRepo;
            _cache = cache;
        }

        public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
        {
            if (await _userRepo.UserNameExistsAsync(request.UserName))
                throw new Exception("Username already exists");

            var user = new User
            {
                Guid = Guid.NewGuid(),
                UserName = request.UserName,
                PasswordHash = SecurePasswordHasher.Hash(request.Password),
                FullName = request.FullName,
                Email = request.Email,
                UserType = request.UserType,
                Status = UserStatus.Pending,
                CreateDate = DateTime.UtcNow
            };

            await _userRepo.AddAsync(user);

            // Clear cache
            try
            {
                var cacheKey = $"users_all";
                await _cache.RemoveAsync(cacheKey);
            }
            catch (Exception ex)
            {
                // Cache is unavailable, continue
                Console.WriteLine($"Failed to clear cache: {ex.Message}");
            }

            return MapToUserDto(user);
        }

        public async Task<UserDto?> GetUserByIdAsync(int id)
        {
            var cacheKey = $"user_{id}";

            // Try to get from cache
            try
            {
                var cachedUser = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedUser))
                    return System.Text.Json.JsonSerializer.Deserialize<UserDto>(cachedUser);
            }
            catch (Exception ex)
            {
                // Cache is unavailable, continue without cache
                Console.WriteLine($"Cache unavailable: {ex.Message}");
            }

            var user = await _userRepo.GetByIdAsync(id);
            if (user != null)
            {
                var userDto = MapToUserDto(user);

                // Try to set cache
                try
                {
                    await _cache.SetStringAsync(cacheKey,
                        System.Text.Json.JsonSerializer.Serialize(userDto),
                        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });
                }
                catch (Exception ex)
                {
                    // Cache is unavailable, continue without caching
                    Console.WriteLine($"Failed to set cache: {ex.Message}");
                }

                return userDto;
            }

            return null;
        }

        public async Task<UserDto?> GetUserByGuidAsync(Guid guid)
        {
            var user = await _userRepo.GetByGuidAsync(guid);
            return user != null ? MapToUserDto(user) : null;
        }

        public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
        {
            var cacheKey = "users_all";

            // Try to get from cache
            try
            {
                var cachedUsers = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedUsers))
                    return System.Text.Json.JsonSerializer.Deserialize<IEnumerable<UserDto>>(cachedUsers) ?? new List<UserDto>();
            }
            catch (Exception ex)
            {
                // Cache is unavailable, continue without cache
                Console.WriteLine($"Cache unavailable: {ex.Message}");
            }

            var users = await _userRepo.GetAllAsync();
            var userDtos = users.Select(MapToUserDto).ToList();

            // Try to set cache
            try
            {
                await _cache.SetStringAsync(cacheKey,
                    System.Text.Json.JsonSerializer.Serialize(userDtos),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
            }
            catch (Exception ex)
            {
                // Cache is unavailable, continue without caching
                Console.WriteLine($"Failed to set cache: {ex.Message}");
            }

            return userDtos;
        }

        public async Task<UserDto> UpdateUserAsync(int userId, UpdateUserRequest request)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
                throw new Exception("User not found");

            user.FullName = request.FullName;
            user.Email = request.Email;
            user.UserType = request.UserType;
            user.Status = request.Status;
            user.LastModify = DateTime.UtcNow;

            await _userRepo.UpdateAsync(user);

            // Clear cache
            try
            {
                await _cache.RemoveAsync($"user_{userId}");
                await _cache.RemoveAsync("users_all");
            }
            catch (Exception ex)
            {
                // Cache is unavailable, continue
                Console.WriteLine($"Failed to clear cache: {ex.Message}");
            }

            return MapToUserDto(user);
        }

        public async Task ApproveUserAsync(int userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
                throw new Exception("User not found");

            if (user.Status != UserStatus.Pending)
                throw new Exception("User is not pending approval");

            user.Status = UserStatus.Active;
            user.LastModify = DateTime.UtcNow;

            await _userRepo.UpdateAsync(user);

            // Clear cache
            try
            {
                await _cache.RemoveAsync($"user_{userId}");
                await _cache.RemoveAsync("users_all");
            }
            catch (Exception ex)
            {
                // Cache is unavailable, continue
                Console.WriteLine($"Failed to clear cache: {ex.Message}");
            }
        }

        public async Task DeleteUserAsync(int userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
                throw new Exception("User not found");

            // Soft delete
            user.Status = UserStatus.Deleted;
            user.LastModify = DateTime.UtcNow;

            await _userRepo.UpdateAsync(user);

            // Clear cache
            try
            {
                await _cache.RemoveAsync($"user_{userId}");
                await _cache.RemoveAsync("users_all");
            }
            catch (Exception ex)
            {
                // Cache is unavailable, continue
                Console.WriteLine($"Failed to clear cache: {ex.Message}");
            }
        }

        public async Task AssignHotelsAsync(int userId, List<string> hotelCodes)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
                throw new Exception("User not found");

            foreach (var hotelCode in hotelCodes)
            {
                var hotel = await _hotelRepo.GetByCodeAsync(hotelCode);
                if (hotel == null)
                    throw new Exception($"Hotel with code {hotelCode} not found");

                var existing = await _userHotelRepo.FindByUserAndHotelAsync(userId, hotel.Guid);
                if (existing != null) continue;

                await _userHotelRepo.AddAsync(new UserHotel
                {
                    UserId = userId,
                    UserGuid = user.Guid,
                    HotelCode = hotel.Code,
                    HotelGuid = hotel.Guid,
                    Status = 1,
                    CreateDate = DateTime.UtcNow
                });
            }
        }

        public async Task<IEnumerable<HotelDto>> GetUserHotelsAsync(int userId)
        {
            var userHotels = await _userHotelRepo.GetByUserIdAsync(userId);
            var hotels = new List<HotelDto>();

            foreach (var userHotel in userHotels)
            {
                var hotel = await _hotelRepo.GetByGuidAsync(userHotel.HotelGuid);
                if (hotel != null)
                    hotels.Add(MapToHotelDto(hotel));
            }

            return hotels;
        }

        // Helper methods để map entity sang DTO
        private UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Guid = user.Guid,
                UserName = user.UserName ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                UserType = user.UserType,
                Status = user.Status,
                TwoFactorEnabled = user.TwoFactorEnabled,
                TwoFactorSecret = user.TwoFactorSecret,
                TwoFactorRecoveryCodes = user.TwoFactorRecoveryCodes,
                CreateDate = user.CreateDate,
                LastModify = user.LastModify
            };
        }

        private HotelDto MapToHotelDto(Hotel hotel)
        {
            return new HotelDto
            {
                Id = hotel.Id,
                Guid = hotel.Guid,
                Code = hotel.Code ?? string.Empty,
                HotelName = hotel.HotelName ?? string.Empty,
                Note = hotel.Note,
                DBName = hotel.DBName,
                IPAddress = hotel.IPAddress,
                ISS_DBName = hotel.ISS_DBName,
                ISS_IPAddress = hotel.ISS_IPAddress,
                PKMTablet = hotel.PKMTablet,
                IP_VPN_FO = hotel.IP_VPN_FO,
                IP_VPN_ISS = hotel.IP_VPN_ISS,
                IPLAN_Server = hotel.IPLAN_Server,
                Email = hotel.Email,
                IsDeleted = hotel.IsDeleted,
                IsAutoUpdateOTA = hotel.IsAutoUpdateOTA,
                OTATimesAuto = hotel.OTATimesAuto,
                HotelAvatarUrl = hotel.HotelAvatarUrl,
                StartDate = hotel.StartDate,
                EndDate = hotel.EndDate,
                CreateDate = hotel.CreateDate
            };
        }

        public async Task<IEnumerable<UserDto>> GetUsersByHotelCodeAsync(string hotelCode)
        {
            var hotel = await _hotelRepo.GetByCodeAsync(hotelCode);
            if (hotel == null)
                throw new Exception("Hotel not found");

            var userHotels = await _userHotelRepo.GetByHotelGuidAsync(hotel.Guid);
            var userIds = userHotels.Select(uh => uh.UserId).ToList();

            var users = new List<UserDto>();
            foreach (var userId in userIds)
            {
                var user = await _userRepo.GetByIdAsync(userId);
                if (user != null && user.Status != UserStatus.Deleted)
                {
                    users.Add(MapToUserDto(user));
                }
            }

            return users;
        }

        public async Task<IEnumerable<UserDto>> GetMyHotelUsersAsync(int currentUserId)
        {
            var currentUser = await _userRepo.GetByIdAsync(currentUserId);
            if (currentUser == null || currentUser.UserType != UserType.HotelAdmin)
                throw new Exception("Only hotel admins can access this endpoint");

            var userHotels = await _userHotelRepo.GetByUserIdAsync(currentUserId);
            if (!userHotels.Any())
                throw new Exception("Hotel admin has no assigned hotels");

            var hotelGuid = userHotels.First().HotelGuid;
            var allUserHotels = await _userHotelRepo.GetByHotelGuidAsync(hotelGuid);
            var userIds = allUserHotels.Select(uh => uh.UserId).Distinct().ToList();

            var users = new List<UserDto>();
            foreach (var userId in userIds)
            {
                var user = await _userRepo.GetByIdAsync(userId);
                if (user != null && user.Status != UserStatus.Deleted && user.UserType == UserType.User)
                {
                    users.Add(MapToUserDto(user));
                }
            }

            return users;
        }

        public async Task<IEnumerable<UserDto>> GetAllHotelAdminsAsync()
        {
            var users = await _userRepo.GetUsersByUserTypeAsync((int)UserType.HotelAdmin);
            return users.Select(MapToUserDto);
        }

        public async Task<IEnumerable<UserDto>> GetHotelUsersForAdminAsync(int adminUserId)
        {
            var adminUser = await _userRepo.GetByIdAsync(adminUserId);
            if (adminUser == null || adminUser.UserType != UserType.HotelAdmin)
                throw new Exception("Only hotel admins can access this endpoint");

            var userHotels = await _userHotelRepo.GetByUserIdAsync(adminUserId);
            if (!userHotels.Any())
                throw new Exception("Hotel admin has no assigned hotels");

            var hotelGuid = userHotels.First().HotelGuid;
            var allUserHotels = await _userHotelRepo.GetByHotelGuidAsync(hotelGuid);
            var userIds = allUserHotels.Select(uh => uh.UserId).Distinct().ToList();

            var users = new List<UserDto>();
            foreach (var userId in userIds)
            {
                var user = await _userRepo.GetByIdAsync(userId);
                if (user != null && user.Status != UserStatus.Deleted && user.UserType == UserType.User)
                {
                    users.Add(MapToUserDto(user));
                }
            }

            return users;
        }
    }
}