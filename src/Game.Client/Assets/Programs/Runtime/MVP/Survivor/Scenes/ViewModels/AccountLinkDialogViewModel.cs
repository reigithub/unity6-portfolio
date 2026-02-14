namespace Game.MVP.Survivor.Scenes.ViewModels
{
    /// <summary>
    /// アカウントリンクダイアログのUIロジック（テスト可能な純粋C#クラス）
    /// バリデーション・データ変換・状態判定を担当
    /// </summary>
    public class AccountLinkDialogViewModel
    {
        #region Validation

        public (bool isValid, string errorMessage) ValidateLinkForm(
            string email, string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                return (false, "All fields are required.");
            }

            if (password != confirmPassword)
            {
                return (false, "Passwords do not match.");
            }

            if (password.Length < 8)
            {
                return (false, "Password must be at least 8 characters.");
            }

            return (true, null);
        }

        public (bool isValid, string errorMessage) ValidateUserIdLogin(
            string userId, string password)
        {
            var cleanUserId = CleanUserId(userId);

            if (string.IsNullOrWhiteSpace(cleanUserId) || string.IsNullOrWhiteSpace(password))
            {
                return (false, "Please enter User ID and password.");
            }

            return (true, null);
        }

        public (bool isValid, string errorMessage) ValidateForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return (false, "Please enter your email address.");
            }

            return (true, null);
        }

        public (bool isValid, string errorMessage) ValidateResetPassword(
            string token, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
            {
                return (false, "Please enter the reset token and new password.");
            }

            if (newPassword.Length < 8)
            {
                return (false, "Password must be at least 8 characters.");
            }

            return (true, null);
        }

        #endregion

        #region Data Conversion

        public static string FormatUserId(string userId)
        {
            if (string.IsNullOrEmpty(userId) || userId.Length != 12)
                return userId ?? "-";

            return $"{userId.Substring(0, 4)} {userId.Substring(4, 4)} {userId.Substring(8, 4)}";
        }

        public static string CleanUserId(string userId)
        {
            return userId?.Replace(" ", "") ?? "";
        }

        #endregion

        #region State

        public static bool IsGuest(string authType)
        {
            return string.IsNullOrEmpty(authType) || authType.ToLower() == "guest";
        }

        #endregion
    }
}
