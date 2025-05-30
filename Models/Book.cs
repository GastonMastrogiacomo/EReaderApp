﻿using System.ComponentModel.DataAnnotations;

namespace EReaderApp.Models
{
    public class Book
    {
        [Key]
        public int IdBook { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Author { get; set; }

        public string? Description { get; set; } // Optional
        public string? ImageLink { get; set; } // Optional
        public string? ReleaseDate { get; set; } // Optional
        public int? PageCount { get; set; } // Optional
        public double? Score { get; set; } // Optional
        public string? PdfPath { get; set; } // Optional

        public string? AuthorBio { get; set; }
    }
}