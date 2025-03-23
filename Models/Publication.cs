using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EReaderApp.Models
{
    public class Publication
    {
        [Key]
        public int IdPublication { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        public string? PubImageUrl { get; set; }

        [Required]
        public int FKIdUser { get; set; }

        [ForeignKey("FKIdUser")]
        public User? User { get; set; } // Made nullable to avoid validation error

        // Add these missing properties
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property for likes
        [NotMapped] // This ensures it's not trying to create a DB column
        public ICollection<PublicationLike> Likes { get; set; } = new List<PublicationLike>();

        [NotMapped]
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}