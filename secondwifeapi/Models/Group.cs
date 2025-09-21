namespace secondwifeapi.Models
{
    public class Group
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int AdminUserId { get; set; }
        public User AdminUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Navigation property for group members
        public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();
    }
}