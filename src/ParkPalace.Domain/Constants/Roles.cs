namespace ParkPalace.Domain.Constants;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Procurement = "Procurement";
    public const string Manager = "Manager";
    public const string Warehouse = "Warehouse";
    public const string Finance = "Finance";
    public const string OrganizationManager = "OrganizationManager";

    public static readonly string[] All = { Admin, Procurement, Manager, Warehouse, Finance, OrganizationManager };
}
