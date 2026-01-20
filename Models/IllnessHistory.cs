using CustomORM;
using System;

namespace Models
{
    [Table(Name = "illness_histories")]
    public class IllnessHistory
    {
        [Id(AutoIncrement = true)]
        public int Id { get; set; }

        [Column(Name = "patient_id", NotNull = true)]
        [ForeignKey("patients")]
        public int PatientId { get; set; }

        [Column(Name = "doctor_id")]
        [ForeignKey("doctors")]
        public int? DoctorId { get; set; }

        [Column(Name = "illness_name", NotNull = true)]
        public string IllnessName { get; set; } = "";

        [Column(Name = "start_date", NotNull = true)]
        public DateTime StartDate { get; set; }

        [Column(Name = "end_date")]
        public DateTime? EndDate { get; set; }

        [Column(Name = "notes")]
        public string Notes { get; set; } = "";

        public virtual Patient Patient { get; set; }
        public virtual Doctor Doctor { get; set; }
    }
}
