using System.ComponentModel.DataAnnotations;

namespace ParkPalace.Application.DTOs;

public class CategoryDto
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public int MaterialCount { get; set; }
    public int ChildrenCount { get; set; }
}

public class CategoryTreeDto
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public int MaterialCount { get; set; }
    public List<CategoryTreeDto> Children { get; set; } = new();
}

public class CreateCategoryRequest
{
    public Guid? ParentId { get; set; }

    [Required(ErrorMessage = "نام دسته‌بندی الزامی است")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "کد دسته‌بندی الزامی است")]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int SortOrder { get; set; } = 0;
}

public class UpdateCategoryRequest
{
    [Required(ErrorMessage = "نام دسته‌بندی الزامی است")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}

public class MoveCategoryRequest
{
    public Guid? NewParentId { get; set; }
}
