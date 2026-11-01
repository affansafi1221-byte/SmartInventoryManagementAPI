using System;
using System.Collections.Generic;
using System.Text;
namespace SmartInventory.Application.DTOs;

public record CategoryDto(int Id, string Name, string? Description);
public record CreateCategoryDto(string Name, string? Description); 
