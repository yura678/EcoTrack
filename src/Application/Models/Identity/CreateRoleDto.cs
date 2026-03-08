namespace Application.Models.Identity;

public class CreateRoleDto
{
    public string RoleName { get; set; }
    public string DisplayName { get; set; }
    public Guid EnterpriseId { get; set; }
}