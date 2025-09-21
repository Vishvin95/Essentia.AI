using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using secondwifeapi.Data;
using secondwifeapi.Models;

namespace secondwifeapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GroupController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GroupController> _logger;

        public GroupController(ApplicationDbContext context, ILogger<GroupController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("create-group")]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.AdminUserId <= 0)
            {
                return BadRequest(new CreateGroupResponse
                {
                    Success = false,
                    Message = "Group name and valid admin user ID are required."
                });
            }

            try
            {
                // Check if admin user exists
                var adminUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == request.AdminUserId && u.IsActive);

                if (adminUser == null)
                {
                    return BadRequest(new CreateGroupResponse
                    {
                        Success = false,
                        Message = "Admin user not found or inactive."
                    });
                }

                // Create new group
                var group = new Group
                {
                    Name = request.Name,
                    Description = request.Description ?? string.Empty,
                    AdminUserId = request.AdminUserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Groups.Add(group);
                await _context.SaveChangesAsync();

                // Add admin as a member of the group
                var adminMembership = new GroupMember
                {
                    GroupId = group.Id,
                    UserId = request.AdminUserId,
                    JoinedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.GroupMembers.Add(adminMembership);
                await _context.SaveChangesAsync();

                var response = new CreateGroupResponse
                {
                    Success = true,
                    Message = "Group created successfully.",
                    GroupId = group.Id
                };

                _logger.LogInformation("Group created successfully: {GroupName}, ID: {GroupId}, Admin: {AdminUserId}", 
                    group.Name, group.Id, request.AdminUserId);
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group with name '{GroupName}'", request.Name);
                return StatusCode(500, new CreateGroupResponse
                {
                    Success = false,
                    Message = "Error creating group."
                });
            }
        }

        [HttpPost("add-user-to-group")]
        public async Task<IActionResult> AddUserToGroup([FromBody] AddUserToGroupRequest request)
        {
            if (request.GroupId <= 0 || request.UserId <= 0)
            {
                return BadRequest(new AddUserToGroupResponse
                {
                    Success = false,
                    Message = "Valid group ID and user ID are required."
                });
            }

            try
            {
                // Check if group exists and is active
                var group = await _context.Groups
                    .FirstOrDefaultAsync(g => g.Id == request.GroupId && g.IsActive);

                if (group == null)
                {
                    return NotFound(new AddUserToGroupResponse
                    {
                        Success = false,
                        Message = "Group not found or inactive."
                    });
                }

                // Check if user exists and is active
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive);

                if (user == null)
                {
                    return NotFound(new AddUserToGroupResponse
                    {
                        Success = false,
                        Message = "User not found or inactive."
                    });
                }

                // Check if user is already a member
                var existingMembership = await _context.GroupMembers
                    .FirstOrDefaultAsync(gm => gm.GroupId == request.GroupId && 
                                             gm.UserId == request.UserId && 
                                             gm.IsActive);

                if (existingMembership != null)
                {
                    return BadRequest(new AddUserToGroupResponse
                    {
                        Success = false,
                        Message = "User is already a member of this group."
                    });
                }

                // Add user to group
                var groupMember = new GroupMember
                {
                    GroupId = request.GroupId,
                    UserId = request.UserId,
                    JoinedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.GroupMembers.Add(groupMember);
                await _context.SaveChangesAsync();

                var response = new AddUserToGroupResponse
                {
                    Success = true,
                    Message = "User added to group successfully."
                };

                _logger.LogInformation("User {UserId} added to group {GroupId} successfully", 
                    request.UserId, request.GroupId);
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user {UserId} to group {GroupId}", 
                    request.UserId, request.GroupId);
                return StatusCode(500, new AddUserToGroupResponse
                {
                    Success = false,
                    Message = "Error adding user to group."
                });
            }
        }

        [HttpGet("get-groups/{userId}")]
        public async Task<IActionResult> GetUserGroups(int userId)
        {
            if (userId <= 0)
            {
                return BadRequest(new GetGroupsResponse
                {
                    Success = false,
                    Message = "Valid user ID is required."
                });
            }

            try
            {
                // Check if user exists
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                if (user == null)
                {
                    return NotFound(new GetGroupsResponse
                    {
                        Success = false,
                        Message = "User not found or inactive."
                    });
                }

                // Get only groups where user is a member (either as admin or regular member)
                var userGroups = await _context.Groups
                    .Include(g => g.AdminUser)
                    .Include(g => g.GroupMembers)
                        .ThenInclude(gm => gm.User)
                    .Where(g => g.IsActive && 
                               (g.AdminUserId == userId || 
                                g.GroupMembers.Any(gm => gm.UserId == userId && gm.IsActive)))
                    .Select(g => new GroupInfo
                    {
                        Id = g.Id,
                        Name = g.Name,
                        Description = g.Description,
                        IsAdmin = g.AdminUserId == userId,
                        Admin = new AdminInfo
                        {
                            Id = g.AdminUser.Id,
                            Username = g.AdminUser.Username,
                            DisplayName = g.AdminUser.DisplayName ?? ""
                        },
                        Members = g.GroupMembers
                            .Where(gm => gm.IsActive)
                            .Select(gm => new GroupMemberInfo
                            {
                                Id = gm.User.Id,
                                Username = gm.User.Username,
                                DisplayName = gm.User.DisplayName ?? "",
                                JoinedAt = gm.JoinedAt
                            })
                            .OrderBy(m => m.Username)
                            .ToList(),
                        CreatedAt = g.CreatedAt
                    })
                    .OrderBy(g => g.Name)
                    .ToListAsync();

                var response = new GetGroupsResponse
                {
                    Success = true,
                    Message = $"Retrieved {userGroups.Count} groups for user.",
                    Groups = userGroups
                };

                _logger.LogInformation("Retrieved {GroupCount} groups for user {UserId}", 
                    userGroups.Count, userId);
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving groups for user {UserId}", userId);
                return StatusCode(500, new GetGroupsResponse
                {
                    Success = false,
                    Message = "Error retrieving groups."
                });
            }
        }
    }
}