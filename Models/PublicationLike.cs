using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EReaderApp.Models
{
    public class PublicationLike
    {
        [Key]
        public int FKIdUser { get; set; }

        [Key]
        public int FKIdPublication { get; set; }

        [ForeignKey("FKIdUser")]
        public User User { get; set; }

        [ForeignKey("FKIdPublication")]
        public Publication Publication { get; set; }
    }
}
