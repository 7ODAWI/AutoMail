using System.ComponentModel.DataAnnotations;

namespace AutoMail.Users.Dto;

public class ChangeUserLanguageDto
{
    [Required]
    public string LanguageName { get; set; }
}