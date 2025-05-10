using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hotel
{
    [Table("booking", Schema = "hotel")]
    public class Booking
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int User_id { get; set; }
        [Column("room_id")]
        public int Room_id { get; set; }

        [Column("start_date")]
        public string Start_date { get; set; }
        [Column("end_date")]
        public string End_date { get; set; }

        [Column("summ")]
        public int Summ { get; set; }

    }
}
