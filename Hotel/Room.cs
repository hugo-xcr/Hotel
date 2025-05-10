using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hotel
{
    [Table("room", Schema = "hotel")]
    public class Room
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("room_nimber")]
        public int Room_number { get; set; }

        [Column("id_information")]
        public int Id_information { get; set; }
    }
}
