using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using WebApplication.Models;

namespace MyWebApplication.Services
{
    public class PasswordGeneratorService : IPasswordGeneratorService
    {
        private readonly UserManager<User> _userManager;

        public PasswordGeneratorService(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        public string GeneratePassword()
        {
            var options = _userManager.Options.Password;

            int length = Math.Max(options.RequiredLength, 12);

            string lower = "abcdefghijklmnopqrstuvwxyz";
            string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string digits = "0123456789";
            string special = "!@#$%-_+";

            string allChars = lower + upper + digits + special;

            while (true)
            {
                byte[] bytes = RandomNumberGenerator.GetBytes(length);

                var pwdChars = bytes.Select(b => allChars[b % allChars.Length]).ToArray();
                var password = new string(pwdChars);

                var result = _userManager.PasswordValidators
                    .Select(v => v.ValidateAsync(_userManager, null, password).Result)
                    .All(r => r.Succeeded);

                if (result)
                    return password;
            }
        }
    }
}
