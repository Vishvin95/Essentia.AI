using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using secondwifeapi.Data;
using secondwifeapi.Models;
using System.Security.Cryptography;
using System.Text;

namespace secondwifeapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserAuthenticationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserAuthenticationController> _logger;

        public UserAuthenticationController(ApplicationDbContext context, ILogger<UserAuthenticationController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("check-username")]
        public async Task<IActionResult> CheckUsernameExists([FromBody] CheckUsernameRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                return BadRequest(new CheckUsernameResponse 
                { 
                    Exists = false, 
                    Message = "Username is required." 
                });
            }

            try
            {
                var exists = await _context.Users
                    .AnyAsync(u => u.Username.ToLower() == request.Username.ToLower());

                var response = new CheckUsernameResponse
                {
                    Exists = exists,
                    Message = exists ? "Username already exists." : "Username is available."
                };

                _logger.LogInformation("Username check for '{Username}': {Exists}", request.Username, exists);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking username existence for '{Username}'", request.Username);
                return StatusCode(500, new CheckUsernameResponse 
                { 
                    Exists = false, 
                    Message = "Error checking username availability." 
                });
            }
        }

        [HttpPost("sign-up")]
        public async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new SignUpResponse
                {
                    Success = false,
                    Message = "Username and password are required."
                });
            }

            try
            {
                // Check if username already exists
                var existingUserByUsername = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower());

                if (existingUserByUsername != null)
                {
                    return BadRequest(new SignUpResponse
                    {
                        Success = false,
                        Message = "Username already exists."
                    });
                }

                // Check if email already exists (only if email is provided)
                if (!string.IsNullOrWhiteSpace(request.Email))
                {
                    var existingUserByEmail = await _context.Users
                        .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == request.Email.ToLower());

                    if (existingUserByEmail != null)
                    {
                        return BadRequest(new SignUpResponse
                        {
                            Success = false,
                            Message = "Email already exists."
                        });
                    }
                }

                // Hash the password
                var passwordHash = HashPassword(request.Password);

                // Validate currency code (basic validation)
                if (!IsValidCurrencyCode(request.DefaultCurrency))
                {
                    return BadRequest(new SignUpResponse
                    {
                        Success = false,
                        Message = "Invalid currency code. Please use a valid ISO 4217 currency code (e.g., USD, EUR, GBP)."
                    });
                }

                // Create new user
                var user = new User
                {
                    Username = request.Username,
                    Email = request.Email,
                    PasswordHash = passwordHash,
                    DisplayName = request.DisplayName,
                    DefaultCurrency = request.DefaultCurrency.ToUpper(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Generate a simple token (in production, use proper JWT)
                var token = GenerateSimpleToken(user.Id, user.Username);

                var response = new SignUpResponse
                {
                    Success = true,
                    Message = "User created successfully.",
                    UserId = user.Id.ToString(),
                    Token = token
                };

                _logger.LogInformation("User created successfully: {Username}, ID: {UserId}", user.Username, user.Id);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user with username '{Username}'", request.Username);
                return StatusCode(500, new SignUpResponse
                {
                    Success = false,
                    Message = "Error creating user."
                });
            }
        }

        [HttpPost("sign-in")]
        public async Task<IActionResult> SignIn([FromBody] SignInRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new SignInResponse
                {
                    Success = false,
                    Message = "Username and password are required."
                });
            }

            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower() && u.IsActive);

                if (user == null)
                {
                    return Unauthorized(new SignInResponse
                    {
                        Success = false,
                        Message = "Invalid username or password."
                    });
                }

                // Verify password
                var passwordHash = HashPassword(request.Password);
                if (user.PasswordHash != passwordHash)
                {
                    return Unauthorized(new SignInResponse
                    {
                        Success = false,
                        Message = "Invalid username or password."
                    });
                }

                // Update last access time
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var response = new SignInResponse
                {
                    Success = true,
                    Message = "Sign in successful.",
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = user.Email ?? "",
                        FirstName = user.DisplayName ?? "",
                        LastName = ""
                    }
                };

                _logger.LogInformation("User signed in successfully: {Username}, ID: {UserId}", user.Username, user.Id);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sign in for username '{Username}'", request.Username);
                return StatusCode(500, new SignInResponse
                {
                    Success = false,
                    Message = "Error during sign in."
                });
            }
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        private static string GenerateSimpleToken(int userId, string username)
        {
            // Simple token generation - in production use proper JWT
            var tokenData = $"{userId}:{username}:{DateTime.UtcNow.Ticks}";
            var tokenBytes = Encoding.UTF8.GetBytes(tokenData);
            return Convert.ToBase64String(tokenBytes);
        }

        private static bool IsValidCurrencyCode(string currencyCode)
        {
            if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
                return false;

            // Common currency codes - in production, you might want to use a more comprehensive list
            var validCurrencies = new HashSet<string>
            {
                "USD", "EUR", "GBP", "JPY", "AUD", "CAD", "CHF", "CNY", "SEK", "NZD",
                "MXN", "SGD", "HKD", "NOK", "TRY", "ZAR", "BRL", "INR", "KRW", "PLN",
                "DKK", "CZK", "HUF", "ILS", "CLP", "PHP", "AED", "COP", "SAR", "MYR",
                "RON", "THB", "BGN", "HRK", "RUB", "ISK", "IDR", "UAH"
            };

            return validCurrencies.Contains(currencyCode.ToUpper());
        }
    }
}