using CustomORM;
using System;

namespace Models
{
    [Table(Name = "medications")]
    public class Medication
    {
        [Id(AutoIncrement = true)]
        public int Id { get; set; }

        [Column(Name = "patient_id", NotNull = true)]
        [ForeignKey("patients")]
        public int PatientId { get; set; }

        [Column(Name = "medication_name", NotNull = true)]
        public string MedicationName { get; set; } = "";

        [Column(Name = "dose_value", NotNull = true)]
        public decimal DoseValue { get; set; }

        [Column(Name = "dose_unit", NotNull = true)]
        public string DoseUnit { get; set; } = "";

        [Column(Name = "frequency", NotNull = true)]
        public string Frequency { get; set; } = "";

        [Column(Name = "prescribed_date", NotNull = true)]
        public DateTime PrescribedDate { get; set; }

        [Column(Name = "end_date")]
        public DateTime? EndDate { get; set; }

        [Column(Name = "notes")]
        public string Notes { get; set; } = "";

        public virtual Patient Patient { get; set; }
    }
}
