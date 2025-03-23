using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EReaderApp.Models
{
    public class Comment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int FKIdPublication { get; set; }

        [Required]
        public int FKIdUser { get; set; }

        [Required]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("FKIdPublication")]
        public Publication Publication { get; set; }

        [ForeignKey("FKIdUser")]
        public User User { get; set; }
    }
}