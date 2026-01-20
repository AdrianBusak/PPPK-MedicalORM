using CustomORM;
using System;

namespace Models
{
    [Table(Name = "examinations")]
    public class Examination
    {
        [Id(AutoIncrement = true)]
        public int Id { get; set; }

        [Column(Name = "patient_id", NotNull = true)]
        [ForeignKey("patients")]
        public int PatientId { get; set; }

        [Column(Name = "doctor_id", NotNull = true)]
        [ForeignKey("doctors")]
        public int DoctorId { get; set; }

        [Column(Name = "examination_type", NotNull = true)]
        public string ExaminationType { get; set; } = "";

        [Column(Name = "scheduled_date", NotNull = true)]
        public DateTime ScheduledDate { get; set; }

        [Column(Name = "completed_date")]
        public DateTime? CompletedDate { get; set; }

        [Column(Name = "result")]
        public string Result { get; set; } = "";

        [Column(Name = "notes")]
        public string Notes { get; set; } = "";

        public virtual Patient Patient { get; set; }
        public virtual Doctor Doctor { get; set; }
    }
}
