using PerfectKeyV1.Application.Common;
using PerfectKeyV1.Application.DTOs.Auth;
using PerfectKeyV1.Application.Interfaces;
using PerfectKeyV1.Domain.Entities;
using PerfectKeyV1.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace PerfectKeyV1.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly IJwtService _jwtService;
        private readonly ILoginSessionRepository _loginSessionRepo;
        private readonly ILoginSessionService _loginSessionService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ITwoFactorService _twoFactorService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IUserHotelRepository _userHotelRepo;

        public AuthService(
            IUserRepository userRepo,
            IJwtService jwtService,
            ILoginSessionRepository loginSessionRepo,
            ILoginSessionService loginSessionService,
            IHttpContextAccessor httpContextAccessor,
            ITwoFactorService twoFactorService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IUserHotelRepository userHotelRepo)
        {
            _userRepo = userRepo;
            _jwtService = jwtService;
            _loginSessionRepo = loginSessionRepo;
            _loginSessionService = loginSessionService;
            _httpContextAccessor = httpContextAccessor;
            _twoFactorService = twoFactorService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _userHotelRepo = userHotelRepo;
        }

        // ==================== AUTHENTICATION METHODS ====================

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    return new AuthResponse { Success = false, Message = "Username and password are required" };
                }

                Console.WriteLine($"=== LOGIN ATTEMPT FOR USER: {request.Username} ===");

                // 1. THỬ ĐĂNG NHẬP QUA GATEWAY TRƯỚC
                Console.WriteLine("Step 1: Attempting Gateway login...");
                var gatewayResponse = await CallGatewayLoginAsync(request);

                if (gatewayResponse.Success)
                {
                    Console.WriteLine("Gateway login SUCCESS!");
                    Console.WriteLine($"Gateway token received: {gatewayResponse.Token?.Substring(0, Math.Min(50, gatewayResponse.Token.Length))}...");

                    // Tìm hoặc tạo user trong local database từ thông tin Gateway
                    var user = await GetOrCreateUserFromGatewayAsync(request.Username, gatewayResponse.UserInfo);

                    if (user == null)
                    {
                        return new AuthResponse { Success = false, Message = "Failed to create local user record" };
                    }

                    // Kiểm tra trạng thái user
                    if (user.Status == UserStatus.Deleted)
                        return new AuthResponse { Success = false, Message = "Account has been deleted" };

                    if (user.Status == UserStatus.Pending)
                        return new AuthResponse { Success = false, Message = "Account is pending approval" };

                    // Tạo session với token từ Gateway
                    Console.WriteLine("Creating session with Gateway token...");
                    if (string.IsNullOrEmpty(gatewayResponse.Token))
                    {
                        return new AuthResponse { Success = false, Message = "No token received from Gateway" };
                    }
                    var session = await CreateLoginSessionAsync(
                        user,
                        gatewayResponse.Token,
                        gatewayResponse.RefreshToken,
                        false
                    );

                    // Kiểm tra 2FA
                    bool requiresTwoFactor = user.TwoFactorEnabled == 1;
                    if (requiresTwoFactor)
                    {
                        session.IsTwoFactorVerified = false;
                        await _loginSessionRepo.UpdateAsync(session);
                    }
                    else
                    {
                        session.IsTwoFactorVerified = true;
                    }

                    return new AuthResponse
                    {
                        Success = true,
                        Message = "Authentication successful (via Gateway)",
                        Token = gatewayResponse.Token, // Luôn trả token
                        RefreshToken = gatewayResponse.RefreshToken,
                        Expiration = gatewayResponse.ExpiresAt,
                        User = MapToUserDto(user),
                        SessionId = session.Id,
                        RequiresTwoFactor = requiresTwoFactor
                    };
                }

                Console.WriteLine($"Gateway login FAILED: {gatewayResponse.Message}");
                Console.WriteLine("Step 2: Falling back to LOCAL authentication...");

                // 2. FALLBACK: KIỂM TRA LOCAL DATABASE
                var localUser = await _userRepo.GetByUserNameAsync(request.Username);
                if (localUser == null)
                {
                    return new AuthResponse { Success = false, Message = "Tên đăng nhập hoặc mật khẩu không chính xác." };
                }

                // Kiểm tra mật khẩu trong local database
                if (!SecurePasswordHasher.Verify(request.Password, localUser.PasswordHash))
                {
                    return new AuthResponse { Success = false, Message = "Tên đăng nhập hoặc mật khẩu không chính xác." };
                }

                // Kiểm tra trạng thái user
                if (localUser.Status == UserStatus.Deleted)
                    return new AuthResponse { Success = false, Message = "Account has been deleted" };

                if (localUser.Status == UserStatus.Pending)
                    return new AuthResponse { Success = false, Message = "Account is pending approval" };

                // Tạo token LOCAL (chỉ khi Gateway thất bại)
                Console.WriteLine("Creating LOCAL JWT token as fallback...");
                var localToken = _jwtService.GenerateJwtToken(localUser);
                var localRefreshToken = _jwtService.GenerateRefreshToken();

                // Tạo session với token local
                var localSession = await CreateLoginSessionAsync(
                    localUser,
                    localToken,
                    localRefreshToken,
                    false
                );

                // Kiểm tra 2FA
                bool localRequiresTwoFactor = localUser.TwoFactorEnabled == 1;
                if (localRequiresTwoFactor)
                {
                    localSession.IsTwoFactorVerified = false;
                    await _loginSessionRepo.UpdateAsync(localSession);
                }
                else
                {
                    localSession.IsTwoFactorVerified = true;
                }

                return new AuthResponse
                {
                    Success = true,
                    Message = "Authentication successful (local fallback)",
                    Token = localToken, // Luôn trả token
                    RefreshToken = localRefreshToken,
                    Expiration = DateTime.UtcNow.AddMinutes(60),
                    User = MapToUserDto(localUser),
                    SessionId = localSession.Id,
                    RequiresTwoFactor = localRequiresTwoFactor
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                return new AuthResponse
                {
                    Success = false,
                    Message = $"Đăng nhập thất bại: {ex.Message}"
                };
            }
        }

        public async Task<AuthResponse> VerifyTwoFactorAsync(TwoFactorRequest request)
        {
            try
            {
                var user = await _userRepo.GetByIdAsync(request.UserId);
                if (user == null || user.TwoFactorEnabled != 1 || string.IsNullOrEmpty(user.TwoFactorSecret))
                {
                    return new AuthResponse { Success = false, Message = "2FA not enabled or user not found" };
                }

                // Validate OTP code
                if (!_twoFactorService.ValidateTwoFactorCode(user.TwoFactorSecret, request.Code))
                {
                    // Kiểm tra xem có phải là recovery code không
                    if (!await ValidateRecoveryCode(user, request.Code))
                    {
                        return new AuthResponse { Success = false, Message = "Invalid OTP or recovery code" };
                    }
                }

                // Lấy session hiện tại và đánh dấu đã xác thực 2FA
                LoginSession? session = null;
                var currentToken = GetCurrentToken();
                if (!string.IsNullOrEmpty(currentToken))
                {
                    session = await _loginSessionRepo.GetByTokenAsync(currentToken);
                }
                else if (request.SessionId.HasValue)
                {
                    session = await _loginSessionRepo.GetByIdAsync(request.SessionId.Value);
                }

                if (session == null)
                {
                    return new AuthResponse { Success = false, Message = "Session not found" };
                }

                session.IsTwoFactorVerified = true;
                session.LastActivity = DateTime.UtcNow;
                await _loginSessionRepo.UpdateAsync(session);

                return new AuthResponse
                {
                    Success = true,
                    Message = "2FA verified successfully",
                    Token = session.Token,
                    RefreshToken = session.RefreshToken,
                    Expiration = session.TokenExpiry ?? DateTime.UtcNow.AddMinutes(60),
                    User = MapToUserDto(user),
                    SessionId = session.Id,
                    RequiresTwoFactor = false
                };
            }
            catch (Exception ex)
            {
                return new AuthResponse { Success = false, Message = $"2FA verification failed: {ex.Message}" };
            }
        }

        public async Task<AuthResponse> RefreshTokenAsync(string token, string refreshToken)
        {
            try
            {
                // Lấy session hiện tại
                var oldSession = await _loginSessionRepo.GetByRefreshTokenAsync(refreshToken);
                if (oldSession == null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Session not found for refresh token"
                    };
                }

                // Gọi API Gateway để refresh token
                var hotelCode = oldSession.User?.HotelCode ?? "PERFECT.KEY";
                var gatewayResponse = await CallGatewayRefreshTokenAsync(refreshToken, hotelCode);

                if (gatewayResponse.Success)
                {
                    // Cập nhật session trong database
                    oldSession.Token = gatewayResponse.Token;
                    oldSession.RefreshToken = gatewayResponse.RefreshToken;
                    oldSession.TokenExpiry = gatewayResponse.ExpiresAt;
                    oldSession.LastActivity = DateTime.UtcNow;

                    await _loginSessionRepo.UpdateAsync(oldSession);

                    // Lấy thông tin user
                    var user = await _userRepo.GetByIdAsync(oldSession.UserId);

                    return new AuthResponse
                    {
                        Success = true,
                        Message = "Token refreshed successfully",
                        Token = gatewayResponse.Token,
                        RefreshToken = gatewayResponse.RefreshToken,
                        Expiration = gatewayResponse.ExpiresAt,
                        User = user != null ? MapToUserDto(user) : null,
                        SessionId = oldSession.Id
                    };
                }

                return new AuthResponse
                {
                    Success = false,
                    Message = gatewayResponse.Message ?? "Refresh token failed"
                };
            }
            catch (Exception ex)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = $"Refresh token failed: {ex.Message}"
                };
            }
        }

        public async Task<bool> LogoutAsync(string refreshToken)
        {
            try
            {
                bool logoutSuccess = true;

                // Lấy session trước
                var session = await _loginSessionRepo.GetByRefreshTokenAsync(refreshToken);

                // Gọi API Gateway để logout
                try
                {
                    var hotelCode = session?.User?.HotelCode ?? "PERFECT.KEY";
                    logoutSuccess = await CallGatewayLogoutAsync(refreshToken, hotelCode);
                }
                catch
                {
                    // Continue even if Gateway call fails
                }

                // Vô hiệu hóa session trong database
                if (session != null)
                {
                    session.IsActive = false;
                    session.LogoutTime = DateTime.UtcNow;
                    await _loginSessionRepo.UpdateAsync(session);
                }

                return logoutSuccess;
            }
            catch
            {
                return false;
            }
        }

        // ==================== HOTEL MANAGEMENT ====================

        public async Task<IEnumerable<UserHotelDto>> GetUserHotelsAsync(string username)
        {
            try
            {
                // Gọi API Gateway để lấy danh sách hotel
                var gatewayHotels = await CallGatewayGetUserHotelsAsync(username);
                return gatewayHotels;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user hotels: {ex.Message}");
                return new List<UserHotelDto>();
            }
        }

        // ==================== TWO-FACTOR AUTHENTICATION ====================

        public async Task<TwoFactorSetupResponse> EnableTwoFactorAsync(int userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
                return new TwoFactorSetupResponse { Success = false, Message = "User not found" };

            if (user.TwoFactorEnabled == 1)
                return new TwoFactorSetupResponse { Success = false, Message = "2FA already enabled" };

            var secretKey = _twoFactorService.GenerateSecretKey();
            var recoveryCodes = _twoFactorService.GenerateRecoveryCodes();

            user.TwoFactorSecret = secretKey;
            user.TwoFactorRecoveryCodes = string.Join(";", recoveryCodes);

            var setupResponse = _twoFactorService.GenerateSetupCode(user.Email ?? string.Empty, secretKey);
            setupResponse.RecoveryCodes = recoveryCodes;

            await _userRepo.UpdateAsync(user);
            return setupResponse;
        }

        public async Task<TwoFactorSetupResponse> ConfirmEnableTwoFactorAsync(int userId, string code)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.TwoFactorSecret))
                return new TwoFactorSetupResponse { Success = false, Message = "User not found or 2FA not setup" };

            Console.WriteLine($"Confirming 2FA for user {userId}, code: {code}, secret exists: {!string.IsNullOrEmpty(user.TwoFactorSecret)}");

            // Directly validate the OTP code
            var isValid = _twoFactorService.ValidateTwoFactorCode(user.TwoFactorSecret, code);
            Console.WriteLine($"OTP validation result: {isValid}");

            if (!isValid)
                return new TwoFactorSetupResponse { Success = false, Message = "Invalid OTP code" };

            user.TwoFactorEnabled = 1;
            await _userRepo.UpdateAsync(user);

            return new TwoFactorSetupResponse { Success = true, Message = "2FA enabled successfully" };
        }

        public async Task<TwoFactorSetupResponse> RegenerateQRAsync(int userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null || user.TwoFactorEnabled != 1 || string.IsNullOrEmpty(user.TwoFactorSecret))
                return new TwoFactorSetupResponse { Success = false, Message = "User not found or 2FA not enabled" };

            var setupResponse = _twoFactorService.GenerateSetupCode(user.Email ?? string.Empty, user.TwoFactorSecret);
            setupResponse.RecoveryCodes = user.TwoFactorRecoveryCodes?.Split(';').ToList() ?? new List<string>();

            return setupResponse;
        }

        public async Task<bool> DisableTwoFactorAsync(int userId, string code)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null || user.TwoFactorEnabled != 1)
                return false;

            // Tạo TwoFactorRequest để xác thực
            var twoFactorRequest = new TwoFactorRequest
            {
                UserId = userId,
                Code = code
            };

            // Sử dụng VerifyTwoFactorAsync để xác thực code
            var verifyResult = await VerifyTwoFactorAsync(twoFactorRequest);

            if (!verifyResult.Success)
                return false;

            user.TwoFactorEnabled = 0;
            user.TwoFactorSecret = null;
            user.TwoFactorRecoveryCodes = null;
            await _userRepo.UpdateAsync(user);

            return true;
        }

        public async Task<List<string>> GetRecoveryCodesAsync(int userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.TwoFactorRecoveryCodes))
                return new List<string>();

            return user.TwoFactorRecoveryCodes.Split(';').ToList();
        }

        public async Task<List<string>> GenerateNewRecoveryCodesAsync(int userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null || user.TwoFactorEnabled != 1)
                return new List<string>();

            var newRecoveryCodes = _twoFactorService.GenerateRecoveryCodes();
            user.TwoFactorRecoveryCodes = string.Join(";", newRecoveryCodes);
            await _userRepo.UpdateAsync(user);

            return newRecoveryCodes;
        }

        // ==================== USER MANAGEMENT ====================

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            // Kiểm tra trong local database
            if (await _userRepo.UserNameExistsAsync(request.UserName))
                return new AuthResponse { Success = false, Message = "Username already exists" };

            if (await _userRepo.EmailExistsAsync(request.Email))
                return new AuthResponse { Success = false, Message = "Email already exists" };

            try
            {
                // 1. Tạo user trong local database (không đăng ký trên Gateway vì Gateway không có API đăng ký)
                var user = new User
                {
                    Guid = Guid.NewGuid(),
                    UserName = request.UserName,
                    PasswordHash = SecurePasswordHasher.Hash(request.Password),
                    FullName = request.FullName,
                    Email = request.Email,
                    Status = UserStatus.Pending,
                    CreateDate = DateTime.UtcNow
                };

                await _userRepo.AddAsync(user);

                return new AuthResponse
                {
                    Success = true,
                    Message = "Registration successful. Please wait for admin approval.",
                    User = MapToUserDto(user)
                };
            }
            catch (Exception ex)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = $"Registration failed: {ex.Message}"
                };
            }
        }

        public async Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            var user = await _userRepo.GetByEmailAsync(request.Email);
            if (user == null)
                return true; // Security: always return true

            // Tạo reset token
            var resetToken = Guid.NewGuid().ToString();
            user.ResetToken = resetToken;
            user.ResetTokenExpiry = DateTime.UtcNow.AddHours(24);
            await _userRepo.UpdateAsync(user);

            Console.WriteLine($"Reset token for {user.Email}: {resetToken}");

            return true;
        }

        public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
        {
            // Tìm user bằng reset token
            var user = await _userRepo.GetByResetTokenAsync(request.Token);
            if (user == null || user.ResetTokenExpiry < DateTime.UtcNow)
                return false;

            user.PasswordHash = SecurePasswordHasher.Hash(request.NewPassword);
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            user.LastModify = DateTime.UtcNow;

            await _userRepo.UpdateAsync(user);
            return true;
        }

        // ==================== TOKEN MANAGEMENT ====================

        public async Task<bool> RevokeTokenAsync(int userId)
        {
            var currentToken = GetCurrentToken();
            if (!string.IsNullOrEmpty(currentToken))
            {
                var session = await _loginSessionRepo.GetByTokenAsync(currentToken);
                if (session != null && session.UserId == userId)
                {
                    await _loginSessionRepo.DeactivateSessionAsync(session.Id);

                    // Cũng có thể gọi Gateway để revoke token
                    try
                    {
                        var hotelCode = session.User.HotelCode ?? "PERFECT.KEY";
                        await CallGatewayLogoutAsync(session.RefreshToken, hotelCode);
                    }
                    catch
                    {
                        // Continue even if Gateway call fails
                    }
                }
            }
            return true;
        }

        // ==================== ADMIN FUNCTIONS ====================

        public async Task<TwoFactorSetupResponse> AdminEnableTwoFactorForUserAsync(int targetUserId)
        {
            var user = await _userRepo.GetByIdAsync(targetUserId);
            if (user == null)
                return new TwoFactorSetupResponse { Success = false, Message = "User not found" };

            var secretKey = _twoFactorService.GenerateSecretKey();
            var recoveryCodes = _twoFactorService.GenerateRecoveryCodes();

            user.TwoFactorSecret = secretKey;
            user.TwoFactorRecoveryCodes = string.Join(";", recoveryCodes);
            user.TwoFactorEnabled = 1;

            await _userRepo.UpdateAsync(user);

            var setupResponse = _twoFactorService.GenerateSetupCode(user.Email ?? string.Empty, secretKey);
            setupResponse.RecoveryCodes = recoveryCodes;
            setupResponse.Message = "2FA enabled by admin";

            return setupResponse;
        }

        // Thêm phương thức này nếu interface yêu cầu
        public async Task<AuthResponse> AdminConfirmEnableTwoFactorForUserAsync(int targetUserId)
        {
            var user = await _userRepo.GetByIdAsync(targetUserId);
            if (user == null)
                return new AuthResponse { Success = false, Message = "User not found" };

            // Admin có thể enable 2FA mà không cần xác nhận code
            if (string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                // Nếu chưa có secret, tạo mới
                var secretKey = _twoFactorService.GenerateSecretKey();
                var recoveryCodes = _twoFactorService.GenerateRecoveryCodes();

                user.TwoFactorSecret = secretKey;
                user.TwoFactorRecoveryCodes = string.Join(";", recoveryCodes);
            }

            user.TwoFactorEnabled = 1;
            await _userRepo.UpdateAsync(user);

            return new AuthResponse
            {
                Success = true,
                Message = "2FA enabled by admin successfully",
                User = MapToUserDto(user)
            };
        }

        public async Task<TwoFactorSetupResponse> AdminRegenerateQRForUserAsync(int targetUserId)
        {
            return await RegenerateQRAsync(targetUserId);
        }

        // ==================== SESSION VALIDATION ====================

        public async Task<bool> ValidateSessionAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            var session = await _loginSessionRepo.GetByTokenAsync(token);
            if (session == null || !session.IsActive)
                return false;

            // Check if session is expired
            if (session.IsExpired())
            {
                session.IsActive = false;
                await _loginSessionRepo.UpdateAsync(session);
                return false;
            }

            // Check if 2FA is required and verified
            var user = await _userRepo.GetByIdAsync(session.UserId);
            if (user != null && user.TwoFactorEnabled == 1)
            {
                return session.IsTwoFactorVerified;
            }

            return true;
        }

        // ==================== PASSWORD GENERATION ====================

        public async Task<string> GeneratePasswordAsync()
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*";
            var random = new Random();
            var password = new string(Enumerable.Repeat(validChars, 12)
                .Select(s => s[random.Next(s.Length)]).ToArray());

            return await Task.FromResult(password);
        }

        // ==================== PRIVATE METHODS ====================

        #region Gateway Integration

        private async Task<User?> GetOrCreateUserFromGatewayAsync(string username, UserInfoDto? gatewayUserInfo)
        {
            // Tìm user trong local database
            var user = await _userRepo.GetByUserNameAsync(username);

            if (user == null)
            {
                // Nếu không tìm thấy và có thông tin từ Gateway, tạo mới
                if (gatewayUserInfo != null)
                {
                    Console.WriteLine($"Creating new user from Gateway info: {username}");

                    user = new User
                    {
                        Guid = gatewayUserInfo.Guid,
                        UserName = gatewayUserInfo.UserName ?? username,
                        FullName = gatewayUserInfo.FullName ?? username,
                        Email = gatewayUserInfo.Email ?? $"{username}@perfectkey.com",
                        Status = gatewayUserInfo.Status,
                        UserType = UserType.User, // Default to User - userType được quản lý locally
                        HotelCode = gatewayUserInfo.HotelCode,
                        CreateDate = DateTime.UtcNow,
                        LastModify = DateTime.UtcNow
                    };

                    await _userRepo.AddAsync(user);
                    Console.WriteLine($"User created with ID: {user.Id}");
                }
                else
                {
                    // Tạo user mặc định nếu không có thông tin từ Gateway
                    Console.WriteLine($"Creating default user for: {username}");

                    user = new User
                    {
                        Guid = Guid.NewGuid(),
                        UserName = username,
                        FullName = username,
                        Email = $"{username}@perfectkey.com",
                        Status = UserStatus.Active,
                        UserType = UserType.User, // Default user
                        CreateDate = DateTime.UtcNow,
                        LastModify = DateTime.UtcNow
                    };

                    await _userRepo.AddAsync(user);
                }
            }
            else
            {
                // Cập nhật thông tin từ Gateway nếu có
                if (gatewayUserInfo != null)
                {
                    Console.WriteLine($"Updating existing user from Gateway info: {username}");

                    user.FullName = gatewayUserInfo.FullName ?? user.FullName;
                    user.Email = gatewayUserInfo.Email ?? user.Email;
                    user.Status = gatewayUserInfo.Status;
                    // user.UserType = gatewayUserInfo.UserType; // Không update userType từ Gateway - được quản lý locally
                    user.HotelCode = gatewayUserInfo.HotelCode ?? user.HotelCode;
                    user.LastModify = DateTime.UtcNow;

                    await _userRepo.UpdateAsync(user);
                }
            }

            return user;
        }

        private async Task<GatewayLoginResponse> CallGatewayLoginAsync(LoginRequest request)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("Gateway");

                var endpoint = $"/identity/api/v1/Auth/login";

                var loginData = new
                {
                    username = request.Username,
                    password = request.Password,
                    hotelCode = request.HotelCode ?? "PERFECT.KEY"
                };

                Console.WriteLine($"Calling Gateway login: {endpoint}");
                Console.WriteLine($"With data: {JsonSerializer.Serialize(loginData)}");

                var response = await httpClient.PostAsJsonAsync(endpoint, loginData);

                Console.WriteLine($"Gateway response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Gateway response content: {content}");

                    try
                    {
                        var loginResponse = JsonSerializer.Deserialize<GatewayLoginResponseDto>(
                            content,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        // Fix: Check for Token instead of Success property, as Identity Service JSON doesn't have Success
                        if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.Token))
                        {
                            return new GatewayLoginResponse
                            {
                                Success = true,
                                Token = loginResponse.Token,
                                RefreshToken = loginResponse.RefreshToken ?? string.Empty,
                                ExpiresAt = loginResponse.ExpiresAt != default ? loginResponse.ExpiresAt : DateTime.UtcNow.AddMinutes(60),
                                UserInfo = loginResponse.User != null ? new UserInfoDto
                                {
                                    Id = loginResponse.User.Id,
                                    Guid = loginResponse.User.Guid != default ? loginResponse.User.Guid : Guid.NewGuid(),
                                    UserName = loginResponse.User.Username ?? request.Username,
                                    FullName = loginResponse.User.FullName ?? request.Username,
                                    Email = loginResponse.User.Email ?? $"{request.Username}@perfectkey.com",
                                    Status = UserStatus.Active, // Default to active since API doesn't provide
                                    UserType = UserType.User, // Default to user
                                    HotelCode = loginResponse.User.HotelCode ?? "PERFECT.KEY"
                                } : null
                            };
                        }
                        else
                        {
                            return new GatewayLoginResponse
                            {
                                Success = false,
                                Message = "Login failed: No token received"
                            };
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.WriteLine($"JSON parsing error: {jsonEx.Message}");
                        return new GatewayLoginResponse
                        {
                            Success = false,
                            Message = "Invalid response format from Gateway"
                        };
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Gateway error response: {errorContent}");

                    try
                    {
                        var error = JsonSerializer.Deserialize<GatewayErrorResponse>(errorContent,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        return new GatewayLoginResponse
                        {
                            Success = false,
                            Message = error?.Message ?? $"Login failed: {response.StatusCode}"
                        };
                    }
                    catch
                    {
                        return new GatewayLoginResponse
                        {
                            Success = false,
                            Message = response.StatusCode switch
                            {
                                System.Net.HttpStatusCode.Unauthorized => "Tên đăng nhập hoặc mật khẩu không chính xác.",
                                System.Net.HttpStatusCode.NotFound => "Người dùng không tồn tại trong hệ thống.",
                                _ => $"Đăng nhập thất bại: {response.StatusCode}"
                            }
                        };
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"HTTP request error: {httpEx.Message}");
                return new GatewayLoginResponse
                {
                    Success = false,
                    Message = "Không thể kết nối đến máy chủ xác thực. Vui lòng thử lại sau."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception calling Gateway: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                return new GatewayLoginResponse
                {
                    Success = false,
                    Message = $"Lỗi hệ thống: {ex.Message}"
                };
            }
        }

        private async Task<GatewayLoginResponse> CallGatewayRefreshTokenAsync(string refreshToken, string hotelCode)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("Gateway");

                var endpoint = $"/identity/api/v1/Auth/refresh-token";

                var response = await httpClient.PostAsJsonAsync(endpoint, new
                {
                    refreshToken = refreshToken,
                    hotelCode = hotelCode
                });

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Gateway refresh token response: {content}");

                    var refreshResponse = JsonSerializer.Deserialize<GatewayLoginResponseDto>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    // Fix: Check for Token existence
                    if (refreshResponse != null && !string.IsNullOrEmpty(refreshResponse.Token))
                    {
                        return new GatewayLoginResponse
                        {
                            Success = true,
                            Token = refreshResponse.Token,
                            RefreshToken = refreshResponse.RefreshToken ?? string.Empty,
                            ExpiresAt = refreshResponse.ExpiresAt != default ? refreshResponse.ExpiresAt : DateTime.UtcNow.AddMinutes(60)
                        };
                    }
                    else
                    {
                        return new GatewayLoginResponse
                        {
                            Success = false,
                            Message = "Refresh token failed: No token received"
                        };
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Gateway refresh token error: {errorContent}");

                    return new GatewayLoginResponse
                    {
                        Success = false,
                        Message = response.StatusCode switch
                        {
                            System.Net.HttpStatusCode.Unauthorized => "Refresh token không hợp lệ hoặc đã hết hạn.",
                            _ => $"Refresh token failed: {response.StatusCode}"
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception refreshing token: {ex.Message}");
                return new GatewayLoginResponse
                {
                    Success = false,
                    Message = $"Refresh token failed: {ex.Message}"
                };
            }
        }

        private async Task<bool> CallGatewayLogoutAsync(string refreshToken, string hotelCode)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("Gateway");

                var endpoint = $"/identity/api/v1/Auth/logout";

                var response = await httpClient.PostAsJsonAsync(endpoint, new
                {
                    refreshToken = refreshToken,
                    hotelCode = hotelCode
                });

                Console.WriteLine($"Gateway logout response: {response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception calling Gateway logout: {ex.Message}");
                return false;
            }
        }

        private async Task<List<UserHotelDto>> CallGatewayGetUserHotelsAsync(string username)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("Gateway");

                var endpoint = $"/identity/api/v1/Auth/hotels/{Uri.EscapeDataString(username)}";

                var response = await httpClient.GetAsync(endpoint);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var hotels = JsonSerializer.Deserialize<List<UserHotelDto>>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                    return hotels ?? new List<UserHotelDto>();
                }

                Console.WriteLine($"Get hotels error: {response.StatusCode}");
                return new List<UserHotelDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception getting hotels: {ex.Message}");
                return new List<UserHotelDto>();
            }
        }

        #endregion

        #region Helper Methods

        private bool IsAdminUser(User user)
        {
            return user.UserType == UserType.SuperAdmin || user.UserType == UserType.HotelAdmin || (user.UserName?.ToLower() == "admin");
        }

        private async Task<bool> ValidateRecoveryCode(User user, string code)
        {
            if (string.IsNullOrEmpty(user.TwoFactorRecoveryCodes))
                return false;

            var recoveryCodes = user.TwoFactorRecoveryCodes.Split(';');
            var isValid = recoveryCodes.Contains(code);

            if (isValid)
            {
                var updatedCodes = recoveryCodes.Where(c => c != code).ToList();
                user.TwoFactorRecoveryCodes = string.Join(";", updatedCodes);
                await _userRepo.UpdateAsync(user);
            }

            return isValid;
        }

        private async Task<LoginSession> CreateLoginSessionAsync(User user, string token, string refreshToken, bool rememberMe = false)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                throw new InvalidOperationException("HttpContext is not available");

            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString();

            var deviceInfo = await _loginSessionService.GetDeviceInfoAsync(httpContext.Request);
            var parsedDeviceInfo = await _loginSessionService.ParseDeviceInfoAsync(userAgent);
            var location = await _loginSessionService.GetLocationFromIpAsync(ipAddress);

            // Kiểm tra token và refreshToken không null
            if (string.IsNullOrEmpty(token))
                throw new ArgumentNullException(nameof(token), "Token cannot be null or empty");

            if (string.IsNullOrEmpty(refreshToken))
                throw new ArgumentNullException(nameof(refreshToken), "Refresh token cannot be null or empty");

            var session = new LoginSession
            {
                UserId = user.Id,
                Token = token,
                RefreshToken = refreshToken,
                DeviceInfo = deviceInfo,
                IpAddress = ipAddress,
                Location = location,
                Browser = parsedDeviceInfo.Browser,
                OperatingSystem = parsedDeviceInfo.OperatingSystem,
                SessionType = parsedDeviceInfo.IsMobile ? "Mobile" :
                             parsedDeviceInfo.IsTablet ? "Tablet" : "Web",
                UserAgent = userAgent,
                LoginTime = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                TokenExpiry = DateTime.UtcNow.AddMinutes(60),
                IsActive = true,
                IsTwoFactorVerified = false,
                IsRememberMe = rememberMe,
                CreateDate = DateTime.UtcNow
            };

            return await _loginSessionRepo.AddAsync(session);
        }

        private string GetCurrentToken()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return string.Empty;

            var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
            return authHeader?.Replace("Bearer ", "") ?? string.Empty;
        }

        private UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Guid = user.Guid,
                UserName = user.UserName ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Status = user.Status,
                UserType = user.UserType,
                TwoFactorEnabled = user.TwoFactorEnabled,
                CreateDate = user.CreateDate,
                LastModify = user.LastModify
            };
        }

        private class UserInfoDto
        {
            public int Id { get; set; }
            public Guid Guid { get; set; }
            public string UserName { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public UserStatus Status { get; set; }
            public UserType? UserType { get; set; }
            public string HotelCode { get; set; } = string.Empty;
        }

        private class GatewayLoginResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public string Token { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
            public UserInfoDto? UserInfo { get; set; }
        }

        private class GatewayLoginResponseDto
        {
            // Identity Service response doesn't have Success/Message at root
            public string Token { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
            public GatewayUserDto? User { get; set; }
        }

        public class GatewayUserDto
        {
            public int Id { get; set; }
            public Guid Guid { get; set; }
            public string Username { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string? AvatarUrl { get; set; }
            public string HotelCode { get; set; } = string.Empty;
            public string HotelName { get; set; } = string.Empty;
            public string? HotelAvatarUrl { get; set; }
        }

        private class GatewayErrorResponse
        {
            public string? Message { get; set; }
            public string? Type { get; set; }
            public int? Status { get; set; }
        }

        #endregion

        // ==================== HOTEL ADMIN 2FA MANAGEMENT ====================

        #region Hotel Admin 2FA Management

        public async Task<bool> AdminDisableTwoFactorForUserAsync(int targetUserId)
        {
            var targetUser = await _userRepo.GetByIdAsync(targetUserId);
            if (targetUser == null)
                return false;

            // Disable 2FA without requiring code verification
            await _userRepo.UpdateTwoFactorAsync(targetUserId, null, null, 0);
            return true;
        }

        public async Task<List<string>> AdminGetRecoveryCodesForUserAsync(int targetUserId)
        {
            var targetUser = await _userRepo.GetByIdAsync(targetUserId);
            if (targetUser == null || string.IsNullOrEmpty(targetUser.TwoFactorRecoveryCodes))
                return new List<string>();

            return targetUser.TwoFactorRecoveryCodes.Split(',').ToList();
        }

        public async Task<List<string>> AdminGenerateRecoveryCodesForUserAsync(int targetUserId)
        {
            var targetUser = await _userRepo.GetByIdAsync(targetUserId);
            if (targetUser == null)
                throw new Exception("User not found");

            var recoveryCodes = _twoFactorService.GenerateRecoveryCodes();
            var recoveryCodesString = string.Join(",", recoveryCodes);

            await _userRepo.UpdateTwoFactorAsync(targetUserId, targetUser.TwoFactorSecret, recoveryCodesString, targetUser.TwoFactorEnabled);

            return recoveryCodes;
        }

        #endregion
    }
}