using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EReaderApp.Models
{
    public class UserProfileViewModel
    {
        public int IdUser { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        public string? ProfilePicture { get; set; }

        // New property for file upload
        [Display(Name = "Profile Picture")]
        public IFormFile? ProfilePictureFile { get; set; }

        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The password and confirmation password do not match.")]
        public string? ConfirmPassword { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}