using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hotel
{
    [Table("rating", Schema = "hotel")]
    public class Rating
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("rating")]
        public float rating { get; set; }

        [Column("comments")]
        public string Comments { get; set; }

        [Column("id_user")]
        public int Id_user { get; set; }
    }
}
