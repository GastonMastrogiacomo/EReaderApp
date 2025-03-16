using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EReaderApp.Models
{
    public class Library
    {
        [Key]
        public int IdLibrary { get; set; }

        [Required]
        public string ListName { get; set; }

        [Required]
        public int FKIdUser { get; set; }

        [ForeignKey("FKIdUser")]
        public User User { get; set; }
    }
}
