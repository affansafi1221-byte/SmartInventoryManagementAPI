using System;
using System.Collections.Generic;
using System.Text;
using SmartInventory.Domain.Common;

namespace SmartInventory.Domain.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
} 