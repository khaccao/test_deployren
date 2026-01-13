using PerfectKeyV1.Application.DTOs.Auth;


namespace PerfectKeyV1.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> VerifyTwoFactorAsync(TwoFactorRequest request);
        Task<AuthResponse> RefreshTokenAsync(string token, string refreshToken);
        Task<bool> LogoutAsync(string refreshToken);
        Task<IEnumerable<UserHotelDto>> GetUserHotelsAsync(string username);

        Task<TwoFactorSetupResponse> EnableTwoFactorAsync(int userId);
        Task<TwoFactorSetupResponse> ConfirmEnableTwoFactorAsync(int userId, string code);
        Task<TwoFactorSetupResponse> RegenerateQRAsync(int userId);
        Task<bool> DisableTwoFactorAsync(int userId, string code);
        Task<List<string>> GetRecoveryCodesAsync(int userId);
        Task<List<string>> GenerateNewRecoveryCodesAsync(int userId);

        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request);
        Task<bool> ResetPasswordAsync(ResetPasswordRequest request);

        Task<bool> RevokeTokenAsync(int userId);

        Task<TwoFactorSetupResponse> AdminEnableTwoFactorForUserAsync(int targetUserId);
        Task<AuthResponse> AdminConfirmEnableTwoFactorForUserAsync(int targetUserId); // Thêm phương thức này
        Task<TwoFactorSetupResponse> AdminRegenerateQRForUserAsync(int targetUserId);
        Task<bool> AdminDisableTwoFactorForUserAsync(int targetUserId);
        Task<List<string>> AdminGetRecoveryCodesForUserAsync(int targetUserId);
        Task<List<string>> AdminGenerateRecoveryCodesForUserAsync(int targetUserId);

        Task<bool> ValidateSessionAsync(string token);
        Task<string> GeneratePasswordAsync();
    }
}