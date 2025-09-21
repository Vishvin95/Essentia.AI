namespace secondwifeapi.Models
{
    public class CreateGroupRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int AdminUserId { get; set; }
    }

    public class CreateGroupResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? GroupId { get; set; }
    }

    public class AddUserToGroupRequest
    {
        public int GroupId { get; set; }
        public int UserId { get; set; }
    }

    public class AddUserToGroupResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class GetGroupsResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<GroupInfo> Groups { get; set; } = new List<GroupInfo>();
    }

    public class GroupInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public AdminInfo Admin { get; set; } = new AdminInfo();
        public List<GroupMemberInfo> Members { get; set; } = new List<GroupMemberInfo>();
        public DateTime CreatedAt { get; set; }
    }

    public class AdminInfo
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class GroupMemberInfo
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
    }
}