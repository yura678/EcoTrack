namespace Application.Common.Settings;

public class ApplicationSettings
{
    public required ConectionStrings ConnectionStrings { get; set; }
}

public class ConectionStrings
{
    public required string DefaultConnection { get; set; }
}