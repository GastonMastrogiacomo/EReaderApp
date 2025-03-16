using System.ComponentModel.DataAnnotations;

namespace EReaderApp.Models
{
    public class User
    {
        [Key]
        public int IdUser { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public string? ProfilePicture { get; set; } // Optional Profile Picture
    }
}
