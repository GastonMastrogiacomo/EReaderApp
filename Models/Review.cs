using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EReaderApp.Models
{
    public class Review
    {
        [Key]
        public int IdReview { get; set; }

        [Required(ErrorMessage = "El comentario es obligatorio")]
        [Display(Name = "Comentario")]
        public string Comment { get; set; }

        [Required(ErrorMessage = "La calificación es obligatoria")]
        [Range(1, 5, ErrorMessage = "La calificación debe ser entre 1 y 5")]
        [Display(Name = "Calificación")]
        public int Rating { get; set; }

        [Required]
        public int FKIdBook { get; set; }

        [Required]
        public int FKIdUser { get; set; }

        [Display(Name = "Fecha de creación")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("FKIdBook")]
        public Book Book { get; set; }

        [ForeignKey("FKIdUser")]
        public User User { get; set; }
    }
}