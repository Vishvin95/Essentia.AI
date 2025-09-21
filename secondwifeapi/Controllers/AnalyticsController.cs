using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using secondwifeapi.Data;
using secondwifeapi.Models;

namespace secondwifeapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(ApplicationDbContext context, ILogger<AnalyticsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get user expense summary including total expenses, group count, and expense count
        /// </summary>
        /// <param name="userId">The ID of the user</param>
        /// <returns>User expense summary</returns>
        [HttpGet("user-summary/{userId}")]
        public async Task<ActionResult<UserExpenseSummaryResponse>> GetUserExpenseSummary(int userId)
        {
            try
            {
                _logger.LogInformation($"Getting expense summary for user {userId}");

                // Check if user exists
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                if (user == null)
                {
                    return NotFound($"User with ID {userId} not found");
                }

                // Get user's expenses
                var userExpenses = await _context.Expenses
                    .Where(e => e.UserId == userId && e.IsActive)
                    .ToListAsync();

                // Get groups user is part of
                var userGroups = await _context.GroupMembers
                    .Where(gm => gm.UserId == userId && gm.IsActive)
                    .CountAsync();

                // Calculate totals
                var totalAmount = userExpenses.Sum(e => e.TotalAmount);
                var totalExpenseCount = userExpenses.Count;
                var lastExpenseDate = userExpenses.Any() ? 
                    userExpenses.Max(e => e.ExpenseDate) : 
                    DateTime.MinValue;

                var response = new UserExpenseSummaryResponse
                {
                    UserId = userId,
                    Username = user.Username,
                    TotalExpenseAmount = totalAmount,
                    Currency = user.DefaultCurrency,
                    GroupCount = userGroups,
                    TotalExpenseCount = totalExpenseCount,
                    LastExpenseDate = lastExpenseDate
                };

                _logger.LogInformation($"Successfully retrieved expense summary for user {userId}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting expense summary for user {userId}");
                return StatusCode(500, "An error occurred while retrieving user expense summary");
            }
        }

        /// <summary>
        /// Get group expense details including individual user expenses within the group
        /// </summary>
        /// <param name="groupId">The ID of the group</param>
        /// <param name="limit">Optional limit for recent expenses (default: 20)</param>
        /// <returns>Group expense details</returns>
        [HttpGet("group-details/{groupId}")]
        public async Task<ActionResult<GroupExpenseDetailsResponse>> GetGroupExpenseDetails(int groupId, int limit = 20)
        {
            try
            {
                _logger.LogInformation($"Getting expense details for group {groupId}");

                // Check if group exists
                var group = await _context.Groups
                    .FirstOrDefaultAsync(g => g.Id == groupId && g.IsActive);

                if (group == null)
                {
                    return NotFound($"Group with ID {groupId} not found");
                }

                // Get group expenses with user information
                var groupExpenses = await _context.Expenses
                    .Include(e => e.User)
                    .Include(e => e.ExpenseItems)
                    .Where(e => e.GroupId == groupId && e.IsActive)
                    .OrderByDescending(e => e.ExpenseDate)
                    .ToListAsync();

                // Calculate group totals
                var totalGroupExpenses = groupExpenses.Sum(e => e.TotalAmount);
                var totalExpenseCount = groupExpenses.Count;

                // Group expenses by user
                var userExpenseGroups = groupExpenses
                    .GroupBy(e => new { e.UserId, e.User.Username, e.User.DisplayName })
                    .Select(g => new UserExpenseInGroup
                    {
                        UserId = g.Key.UserId,
                        Username = g.Key.Username,
                        DisplayName = g.Key.DisplayName,
                        TotalAmount = g.Sum(e => e.TotalAmount),
                        Currency = g.First().Currency,
                        ExpenseCount = g.Count(),
                        LastExpenseDate = g.Max(e => e.ExpenseDate)
                    })
                    .OrderByDescending(u => u.TotalAmount)
                    .ToList();

                // Get recent expenses for the group
                var recentExpenses = groupExpenses
                    .Take(limit)
                    .Select(e => new ExpenseDetail
                    {
                        ExpenseId = e.ExpenseId,
                        UserId = e.UserId,
                        Username = e.User.Username,
                        DisplayName = e.User.DisplayName,
                        ExpenseDate = e.ExpenseDate,
                        VendorName = e.VendorName,
                        TotalAmount = e.TotalAmount,
                        Currency = e.Currency,
                        Items = e.ExpenseItems.Where(ei => ei.IsActive).Select(ei => new ExpenseItemDetail
                        {
                            ExpenseItemId = ei.ExpenseItemId,
                            Description = ei.Description,
                            Amount = ei.Amount,
                            Currency = ei.Currency,
                            Quantity = ei.Quantity
                        }).ToList()
                    })
                    .ToList();

                var response = new GroupExpenseDetailsResponse
                {
                    GroupId = groupId,
                    GroupName = group.Name,
                    TotalGroupExpenses = totalGroupExpenses,
                    Currency = group.AdminUser?.DefaultCurrency ?? "USD", // Use admin's default currency or USD
                    TotalExpenseCount = totalExpenseCount,
                    UserExpenses = userExpenseGroups,
                    RecentExpenses = recentExpenses
                };

                _logger.LogInformation($"Successfully retrieved expense details for group {groupId}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting expense details for group {groupId}");
                return StatusCode(500, "An error occurred while retrieving group expense details");
            }
        }

        /// <summary>
        /// Get user expenses within a specific group
        /// </summary>
        /// <param name="groupId">The ID of the group</param>
        /// <param name="userId">The ID of the user</param>
        /// <returns>User expenses within the specified group</returns>
        [HttpGet("group/{groupId}/user/{userId}")]
        public async Task<ActionResult<List<ExpenseDetail>>> GetUserExpensesInGroup(int groupId, int userId)
        {
            try
            {
                _logger.LogInformation($"Getting expenses for user {userId} in group {groupId}");

                // Verify user is a member of the group
                var groupMember = await _context.GroupMembers
                    .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId && gm.IsActive);

                if (groupMember == null)
                {
                    return NotFound($"User {userId} is not a member of group {groupId}");
                }

                // Get user expenses in the group
                var userExpenses = await _context.Expenses
                    .Include(e => e.User)
                    .Include(e => e.ExpenseItems)
                    .Where(e => e.GroupId == groupId && e.UserId == userId && e.IsActive)
                    .OrderByDescending(e => e.ExpenseDate)
                    .Select(e => new ExpenseDetail
                    {
                        ExpenseId = e.ExpenseId,
                        UserId = e.UserId,
                        Username = e.User.Username,
                        DisplayName = e.User.DisplayName,
                        ExpenseDate = e.ExpenseDate,
                        VendorName = e.VendorName,
                        TotalAmount = e.TotalAmount,
                        Currency = e.Currency,
                        Items = e.ExpenseItems.Where(ei => ei.IsActive).Select(ei => new ExpenseItemDetail
                        {
                            ExpenseItemId = ei.ExpenseItemId,
                            Description = ei.Description,
                            Amount = ei.Amount,
                            Currency = ei.Currency,
                            Quantity = ei.Quantity
                        }).ToList()
                    })
                    .ToListAsync();

                _logger.LogInformation($"Successfully retrieved {userExpenses.Count} expenses for user {userId} in group {groupId}");
                return Ok(userExpenses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting expenses for user {userId} in group {groupId}");
                return StatusCode(500, "An error occurred while retrieving user expenses in group");
            }
        }

        /// <summary>
        /// Get comprehensive summary statistics for a specific group
        /// </summary>
        /// <param name="groupId">The ID of the group to get summary for</param>
        /// <returns>Group-level summary statistics</returns>
        [HttpGet("group-summary/{groupId}")]
        public async Task<ActionResult<GroupSummaryResponse>> GetGroupSummary(int groupId)
        {
            try
            {
                _logger.LogInformation($"Getting group summary for group {groupId}");

                // Get the group with its members and admin
                var group = await _context.Groups
                    .Include(g => g.AdminUser)
                    .Include(g => g.GroupMembers.Where(gm => gm.IsActive))
                    .ThenInclude(gm => gm.User)
                    .FirstOrDefaultAsync(g => g.Id == groupId && g.IsActive);

                if (group == null)
                {
                    return NotFound($"Group with ID {groupId} not found");
                }

                // Get all expenses for this group
                var groupExpenses = await _context.Expenses
                    .Where(e => e.GroupId == groupId && e.IsActive)
                    .ToListAsync();

                // Calculate basic statistics
                var totalExpenses = groupExpenses.Sum(e => e.TotalAmount);
                var totalExpenseCount = groupExpenses.Count;
                var memberCount = group.GroupMembers.Count;
                var lastExpenseDate = groupExpenses.Any() ? 
                    groupExpenses.Max(e => e.ExpenseDate) : 
                    (DateTime?)null;

                // Calculate average expense amount
                var averageExpenseAmount = totalExpenseCount > 0 ? totalExpenses / totalExpenseCount : 0;

                // Find most frequent vendor
                var mostFrequentVendor = groupExpenses
                    .Where(e => !string.IsNullOrEmpty(e.VendorName))
                    .GroupBy(e => e.VendorName)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key;

                // Count unique vendors
                var vendorCount = groupExpenses
                    .Where(e => !string.IsNullOrEmpty(e.VendorName))
                    .Select(e => e.VendorName)
                    .Distinct()
                    .Count();

                // Calculate expense frequency
                var now = DateTime.UtcNow;
                var last7Days = now.AddDays(-7);
                var last30Days = now.AddDays(-30);

                var expensesLast7Days = groupExpenses.Count(e => e.ExpenseDate >= last7Days);
                var expensesLast30Days = groupExpenses.Count(e => e.ExpenseDate >= last30Days);

                // Calculate average expenses per week/month based on group age
                var groupAgeInDays = (now - group.CreatedAt).TotalDays;
                var averageExpensesPerWeek = groupAgeInDays > 7 ? 
                    (totalExpenseCount / groupAgeInDays) * 7 : 0;
                var averageExpensesPerMonth = groupAgeInDays > 30 ? 
                    (totalExpenseCount / groupAgeInDays) * 30 : 0;

                var expenseFrequency = new ExpenseFrequency
                {
                    ExpensesLast7Days = expensesLast7Days,
                    ExpensesLast30Days = expensesLast30Days,
                    AverageExpensesPerWeek = (decimal)averageExpensesPerWeek,
                    AverageExpensesPerMonth = (decimal)averageExpensesPerMonth
                };

                // Get member information (not expense data)
                var members = group.GroupMembers
                    .Select(gm => new GroupMemberSummary
                    {
                        UserId = gm.UserId,
                        Username = gm.User.Username,
                        DisplayName = gm.User.DisplayName,
                        JoinedAt = gm.JoinedAt,
                        IsAdmin = gm.UserId == group.AdminUserId
                    })
                    .OrderBy(m => m.JoinedAt)
                    .ToList();

                var response = new GroupSummaryResponse
                {
                    GroupId = group.Id,
                    GroupName = group.Name,
                    Description = group.Description,
                    AdminUserId = group.AdminUserId,
                    AdminUsername = group.AdminUser.Username,
                    AdminDisplayName = group.AdminUser.DisplayName,
                    TotalExpenses = totalExpenses,
                    Currency = group.AdminUser.DefaultCurrency,
                    TotalExpenseCount = totalExpenseCount,
                    MemberCount = memberCount,
                    CreatedAt = group.CreatedAt,
                    LastExpenseDate = lastExpenseDate,
                    AverageExpenseAmount = averageExpenseAmount,
                    MostFrequentVendor = mostFrequentVendor,
                    VendorCount = vendorCount,
                    ExpenseFrequency = expenseFrequency,
                    Members = members
                };

                _logger.LogInformation($"Successfully retrieved summary for group {groupId}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting group summary for group {groupId}");
                return StatusCode(500, "An error occurred while retrieving group summary");
            }
        }
    }
}