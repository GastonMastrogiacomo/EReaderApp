using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EReaderApp.Models
{
    public class ReviewLike
    {
        [Key]
        public int FKIdUser { get; set; }

        [Key]
        public int FKIdReview { get; set; }

        [ForeignKey("FKIdUser")]
        public User User { get; set; }

        [ForeignKey("FKIdReview")]
        public Review Review { get; set; }
    }
}