namespace Api.Dtos;

public record UpdateMyProfileDto(string? Name, string? FamilyName);

public record ChangeMyPasswordDto(string CurrentPassword, string NewPassword);
