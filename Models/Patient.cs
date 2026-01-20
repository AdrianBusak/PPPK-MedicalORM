using CustomORM;
using System;
using System.Collections.Generic;

namespace Models
{
    [Table(Name = "patients")]
    public class Patient
    {
        [Id(AutoIncrement = true)]
        public int Id { get; set; }

        [Column(Name = "oib", NotNull = true)]
        public string OIB { get; set; } = "";

        [Column(Name = "first_name", NotNull = true)]
        public string FirstName { get; set; } = "";

        [Column(Name = "last_name", NotNull = true)]
        public string LastName { get; set; } = "";

        [Column(Name = "birth_date", NotNull = true)]
        public DateTime BirthDate { get; set; }

        [Column(Name = "gender", NotNull = true)]
        public string Gender { get; set; } = "";

        [Column(Name = "residence_address", NotNull = true)]
        public string ResidenceAddress { get; set; } = "";

        [Column(Name = "home_address")]
        public string HomeAddress { get; set; } = "";

        public virtual List<IllnessHistory> IllnessHistories { get; set; } = new();
        public virtual List<Medication> Medications { get; set; } = new();
        public virtual List<Examination> Examinations { get; set; } = new();
    }
}
