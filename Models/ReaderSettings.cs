using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EReaderApp.Models
{
    public class ReaderSettings
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int FontSize { get; set; }

        [Required]
        public string FontFamily { get; set; }

        [Required]
        public string Theme { get; set; }
    }
}