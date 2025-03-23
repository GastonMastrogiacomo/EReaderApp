using System;
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

        public string? ProfilePicture { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required]
        public string Role { get; set; } = "User"; // Default role is User, can be "Admin" for administrators
    }
}