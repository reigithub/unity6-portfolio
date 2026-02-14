using Game.MVP.Survivor.Scenes.ViewModels;
using NUnit.Framework;

namespace Game.Tests.MVP
{
    [TestFixture]
    public class AccountLinkDialogViewModelTests
    {
        private AccountLinkDialogViewModel _viewModel;

        [SetUp]
        public void Setup()
        {
            _viewModel = new AccountLinkDialogViewModel();
        }

        #region ValidateLinkForm Tests

        [Test]
        public void ValidateLinkForm_WithEmptyEmail_ReturnsInvalid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateLinkForm("", "password1", "password1");
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.EqualTo("All fields are required."));
        }

        [Test]
        public void ValidateLinkForm_WithEmptyPassword_ReturnsInvalid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateLinkForm("test@example.com", "", "password1");
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.EqualTo("All fields are required."));
        }

        [Test]
        public void ValidateLinkForm_WithEmptyConfirmPassword_ReturnsInvalid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateLinkForm("test@example.com", "password1", "");
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.EqualTo("All fields are required."));
        }

        [Test]
        public void ValidateLinkForm_WithNullFields_ReturnsInvalid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateLinkForm(null, null, null);
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.EqualTo("All fields are required."));
        }

        [Test]
        public void ValidateLinkForm_WithWhitespaceOnly_ReturnsInvalid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateLinkForm("  ", "password1", "password1");
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.EqualTo("All fields are required."));
        }

        [Test]
        public void ValidateLinkForm_WithPasswordMismatch_ReturnsInvalid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateLinkForm("test@example.com", "password1", "password2");
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.EqualTo("Passwords do not match."));
        }

        [Test]
        public void ValidateLinkForm_WithShortPassword_ReturnsInvalid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateLinkForm("test@example.com", "short", "short");
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.EqualTo("Password must be at least 8 characters."));
        }

        [Test]
        public void ValidateLinkForm_WithValidInput_ReturnsValid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateLinkForm("test@example.com", "password1", "password1");
            Assert.That(isValid, Is.True);
            Assert.That(errorMessage, Is.Null);
        }

        #endregion

        #region ValidateUserIdLogin Tests

        [Test]
        public void ValidateUserIdLogin_WithEmptyUserId_ReturnsInvalid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateUserIdLogin("", "password1");
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.EqualTo("Please enter User ID and password."));
        }

        [Test]
        public void ValidateUserIdLogin_WithEmptyPassword_ReturnsInvalid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateUserIdLogin("123456789012", "");
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.EqualTo("Please enter User ID and password."));
        }

        [Test]
        public void ValidateUserIdLogin_WithSpacesOnlyUserId_ReturnsInvalid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateUserIdLogin("    ", "password1");
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.EqualTo("Please enter User ID and password."));
        }

        [Test]
        public void ValidateUserIdLogin_WithValidInput_ReturnsValid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateUserIdLogin("0000 0000 0001", "password1");
            Assert.That(isValid, Is.True);
            Assert.That(errorMessage, Is.Null);
        }

        #endregion

        #region ValidateForgotPassword Tests

        [Test]
        public void ValidateForgotPassword_WithEmptyEmail_ReturnsInvalid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateForgotPassword("");
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.EqualTo("Please enter your email address."));
        }

        [Test]
        public void ValidateForgotPassword_WithValidEmail_ReturnsValid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateForgotPassword("test@example.com");
            Assert.That(isValid, Is.True);
            Assert.That(errorMessage, Is.Null);
        }

        #endregion

        #region ValidateResetPassword Tests

        [Test]
        public void ValidateResetPassword_WithEmptyToken_ReturnsInvalid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateResetPassword("", "password1");
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.EqualTo("Please enter the reset token and new password."));
        }

        [Test]
        public void ValidateResetPassword_WithEmptyPassword_ReturnsInvalid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateResetPassword("token123", "");
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.EqualTo("Please enter the reset token and new password."));
        }

        [Test]
        public void ValidateResetPassword_WithShortPassword_ReturnsInvalid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateResetPassword("token123", "short");
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.EqualTo("Password must be at least 8 characters."));
        }

        [Test]
        public void ValidateResetPassword_WithValidInput_ReturnsValid()
        {
            var (isValid, errorMessage) = _viewModel.ValidateResetPassword("token123", "password1");
            Assert.That(isValid, Is.True);
            Assert.That(errorMessage, Is.Null);
        }

        #endregion

        #region FormatUserId Tests

        [Test]
        public void FormatUserId_WithNull_ReturnsDash()
        {
            Assert.That(AccountLinkDialogViewModel.FormatUserId(null), Is.EqualTo("-"));
        }

        [Test]
        public void FormatUserId_WithEmptyString_ReturnsEmptyString()
        {
            // 空文字列は null ではないため、そのまま返される
            Assert.That(AccountLinkDialogViewModel.FormatUserId(""), Is.EqualTo(""));
        }

        [Test]
        public void FormatUserId_With12CharString_ReturnsFormatted()
        {
            Assert.That(AccountLinkDialogViewModel.FormatUserId("123456789012"), Is.EqualTo("1234 5678 9012"));
        }

        [Test]
        public void FormatUserId_WithNon12CharString_ReturnsAsIs()
        {
            Assert.That(AccountLinkDialogViewModel.FormatUserId("12345"), Is.EqualTo("12345"));
        }

        #endregion

        #region CleanUserId Tests

        [Test]
        public void CleanUserId_RemovesSpaces()
        {
            Assert.That(AccountLinkDialogViewModel.CleanUserId("0000 0000 0001"), Is.EqualTo("000000000001"));
        }

        [Test]
        public void CleanUserId_WithNull_ReturnsEmptyString()
        {
            Assert.That(AccountLinkDialogViewModel.CleanUserId(null), Is.EqualTo(""));
        }

        #endregion

        #region IsGuest Tests

        [Test]
        public void IsGuest_WithNull_ReturnsTrue()
        {
            Assert.That(AccountLinkDialogViewModel.IsGuest(null), Is.True);
        }

        [Test]
        public void IsGuest_WithEmptyString_ReturnsTrue()
        {
            Assert.That(AccountLinkDialogViewModel.IsGuest(""), Is.True);
        }

        [Test]
        public void IsGuest_WithGuestString_ReturnsTrue()
        {
            Assert.That(AccountLinkDialogViewModel.IsGuest("guest"), Is.True);
        }

        [Test]
        public void IsGuest_WithEmailString_ReturnsFalse()
        {
            Assert.That(AccountLinkDialogViewModel.IsGuest("email"), Is.False);
        }

        #endregion
    }
}
