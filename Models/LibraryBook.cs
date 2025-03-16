using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EReaderApp.Models
{
    public class LibraryBook
    {
        [Key]
        public int FKIdLibrary { get; set; }

        [Key]
        public int FKIdBook { get; set; }

        [ForeignKey("FKIdLibrary")]
        public Library Library { get; set; }

        [ForeignKey("FKIdBook")]
        public Book Book { get; set; }
    }
}
