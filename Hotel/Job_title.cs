using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hotel
{
    [Table("job_title", Schema = "hotel")]
    public class Job_title
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("responsibilities")]
        public string Responsibilities { get; set; }

    }
}
