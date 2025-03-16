using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EReaderApp.Models
{
    public class BookCategory
    {
        [Key]
        public int FKIdCategory { get; set; }

        [Key]
        public int FKIdBook { get; set; }

        [ForeignKey("FKIdCategory")]
        public Category Category { get; set; }

        [ForeignKey("FKIdBook")]
        public Book Book { get; set; }
    }
}
