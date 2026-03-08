namespace Application.Models.Identity;

public class EditRolePermissionsDto
{
    public Guid RoleId { get; set; }
    public List<string> Permissions { get; set; }
}