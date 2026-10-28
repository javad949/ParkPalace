using System.ComponentModel.DataAnnotations;

namespace ParkPalace.Application.DTOs;

public class UnitOfMeasureDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Precision { get; set; }
    public string Dimension { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class CreateUnitRequest
{
    [Required(ErrorMessage = "کد واحد الزامی است")]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام واحد الزامی است")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 6, ErrorMessage = "دقت اعشار باید بین ۰ تا ۶ باشد")]
    public int Precision { get; set; } = 2;

    [Required(ErrorMessage = "بُعد فیزیکی الزامی است")]
    [MaxLength(50)]
    public string Dimension { get; set; } = "Mass"; // Mass, Volume, Count, Packaging
}
