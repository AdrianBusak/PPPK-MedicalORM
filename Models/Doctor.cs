using CustomORM;
using System.Collections.Generic;

namespace Models
{
    [Table(Name = "doctors")]
    public class Doctor
    {
        [Id(AutoIncrement = true)]
        public int Id { get; set; }

        [Column(Name = "first_name", NotNull = true)]
        public string FirstName { get; set; } = "";

        [Column(Name = "last_name", NotNull = true)]
        public string LastName { get; set; } = "";

        [Column(Name = "specialization", NotNull = true)]
        public string Specialization { get; set; } = "";

        public virtual List<Examination> Examinations { get; set; } = new();
        public virtual List<IllnessHistory> IllnessHistories { get; set; } = new();
    }
}
