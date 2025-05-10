using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hotel
{
    [Table("user", Schema = "hotel")]
    public class Users
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("password")]
        public string Password { get; set; }

        [Column("surname")]
        public string Surname { get; set; }

        [Column("firstname")]
        public string Firstname { get; set; }

        [Column("id_job")]
        public int IdJob { get; set; }

    }
}