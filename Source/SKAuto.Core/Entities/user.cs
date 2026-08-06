using SKAuto.Core.Enums;

namespace SKAuto.Core.Entities
{
    public class User : BaseEntity
    {
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public UserRole Role { get; set; } = UserRole.User;
        public bool IsActive { get; set; } = true;

        // NEW: Email for password reset (nullable)
        public string? Email { get; set; }

        // NEW: Reset token (6-digit code)
        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }
    }
}