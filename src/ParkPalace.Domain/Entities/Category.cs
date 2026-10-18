using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public class Category : BaseEntity
{
    public Guid? ParentId { get; set; }
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();

    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public ICollection<Material> Materials { get; set; } = new List<Material>();
}
