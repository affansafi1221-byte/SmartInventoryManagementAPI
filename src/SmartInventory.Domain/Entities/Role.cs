using System;
using System.Collections.Generic;
using System.Text;
using SmartInventory.Domain.Common;

namespace SmartInventory.Domain.Entities;

public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty; // Admin, Manager, Staff
    public ICollection<User> Users { get; set; } = new List<User>();
} 