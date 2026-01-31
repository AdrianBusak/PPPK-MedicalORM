using CustomORM;
using Models;
using System;
using System.Linq;

namespace MedicalORMDemo
{
    class Program
    {
        private static CustomDbContext _db;
        private const string CONNECTION_STRING = "Host=localhost;Username=postgres;Password=pass;Database=medical;Port=5432";

        static async Task Main(string[] args)
        {
            _db = new CustomDbContext(CONNECTION_STRING);
            Console.WriteLine("=== CUSTOM ORM MEDICAL SYSTEM DEMO ===\n");

            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== CUSTOM ORM MEDICAL SYSTEM DEMO ===\n");
                ShowMenu();
                var choice = Console.ReadLine()?.Trim();

                try
                {
                    await HandleChoice(choice);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nError: {ex.Message}");
                }

                Console.WriteLine("\nPress Enter to continue...");
                Console.ReadLine();
            }
        }

        static void ShowMenu()
        {
            Console.WriteLine("--- MENU ---");
            Console.WriteLine("1. Initialize Database (basic tables + FKs)");
            Console.WriteLine("2. Auto Migrate (code-first)");
            Console.WriteLine("3. Show Migrations");
            Console.WriteLine("4. Rollback Last Migration");
            Console.WriteLine("5. Migrate Forward Last");
            Console.WriteLine("6. Seed Demo Data");
            Console.WriteLine("7. CRUD Operations");
            Console.WriteLine("8. Where Filtering (Expression)");
            Console.WriteLine("9. Include Navigation Properties");
            Console.WriteLine("10. Change Tracking + SaveChanges");
            Console.WriteLine("11. Fluent Query Builder");
            Console.WriteLine("0. Exit");
            Console.Write("\nChoose option: ");
        }

        static async Task HandleChoice(string choice)
        {
            var entities = new[] { typeof(Patient), typeof(Doctor), typeof(Examination), typeof(IllnessHistory), typeof(Medication) };

            switch (choice)
            {
                case "1":
                    Console.WriteLine("\n--- 1. INITIALIZE DATABASE (BASIC) ---");
                    await _db.InitializeDatabaseAsync(entities);
                    break;

                case "2":
                    Console.WriteLine("\n--- 2. AUTO MIGRATE (CODE-FIRST) ---");
                    await _db.InitializeDatabaseMigrationAsync(entities);
                    break;

                case "3":
                    Console.WriteLine("\n--- 3. SHOW MIGRATIONS ---");
                    await _db.ShowMigrationsAsync();
                    break;

                case "4":
                    Console.WriteLine("\n--- 4. ROLLBACK LAST MIGRATION ---");
                    await _db.RollbackLastMigrationAsync();
                    break;

                case "5":
                    Console.WriteLine("\n--- 5. MIGRATE FORWARD LAST ---");
                    await _db.MigrateForwardAsync();
                    break;

                case "6":
                    await SeedDemoData();
                    break;

                case "7":
                    await CrudDemo();
                    break;

                case "8":
                    await WhereDemo();
                    break;

                case "9":
                    await IncludeDemo();
                    break;

                case "10":
                    await ChangeTrackerDemo();
                    break;

                case "11":
                    await FluentQueryDemo();
                    break;

                case "0":
                    Console.WriteLine("Goodbye!");
                    Environment.Exit(0);
                    break;

                default:
                    Console.WriteLine("Invalid option.");
                    break;
            }
        }

        static async Task SeedDemoData()
        {
            Console.WriteLine("\n--- 6. SEED DEMO DATA ---");

            var doc1 = new Doctor { FirstName = "John", LastName = "Doe", Specialization = "Cardiologist" };
            var doc2 = new Doctor { FirstName = "Jane", LastName = "Smith", Specialization = "Dermatologist" };
            _db.Create(doc1);
            _db.Create(doc2);
            Console.WriteLine("Seeded 2 doctors.");

            var patient1 = new Patient
            {
                OIB = "12345678901",
                FirstName = "Ana",
                LastName = "Novak",
                BirthDate = new DateTime(1990, 5, 15),
                Gender = "F",
                ResidenceAddress = "Zagreb, Ulica 1"
            };
            var patientId = _db.Create(patient1);
            Console.WriteLine($"Created Patient ID: {patientId}");

            var exam = new Examination
            {
                PatientId = patientId,
                DoctorId = 1,
                ExaminationType = "EKG",
                ScheduledDate = DateTime.Now.AddDays(1),
                Result = "Normal"
            };
            _db.Create(exam);
            Console.WriteLine("Created examination.");

            var illness = new IllnessHistory
            {
                PatientId = patientId,
                DoctorId = 1,
                IllnessName = "Hypertension",
                StartDate = DateTime.Now.AddMonths(-6)
            };
            _db.Create(illness);
            Console.WriteLine("Created illness history.");

            var med = new Medication
            {
                PatientId = patientId,
                MedicationName = "Amlodipine",
                DoseValue = 5.0m,
                DoseUnit = "mg",
                Frequency = "1x daily",
                PrescribedDate = DateTime.Now
            };
            _db.Create(med);
            Console.WriteLine("Created medication.");
        }

        static async Task CrudDemo()
        {
            Console.WriteLine("\n--- 7. CRUD OPERATIONS ---");

            var newPatient = new Patient
            {
                OIB = "98765432109",
                FirstName = "Marko",
                LastName = "Horvat",
                BirthDate = new DateTime(1985, 3, 20),
                Gender = "M",
                ResidenceAddress = "Split, Ulica 2"
            };
            var newId = _db.Create(newPatient);
            Console.WriteLine($"CREATE: New Patient ID = {newId}");

            var patient = _db.GetById<Patient>(newId);
            Console.WriteLine($"READ: Patient '{patient?.FirstName} {patient?.LastName}'");

            if (patient != null)
            {
                patient.ResidenceAddress = "Zagreb, New Address";
                var updated = _db.Update(patient, newId);
                Console.WriteLine($"UPDATE: {(updated ? "Success" : "Failed")}");
            }

            // DELETE
            //var deleted = _db.Delete<Patient>(newId);
            //Console.WriteLine($"DELETE: {(deleted ? "Success" : "Failed")}");
        }

        static async Task WhereDemo()
        {
            Console.WriteLine("\n--- 8. WHERE FILTERING (EXPRESSIONS) ---");

            var patients = _db.Where<Patient>(p => p.FirstName.Contains("Marko"));
            Console.WriteLine($"Patients with 'Marko' in name: {patients.Count}");

            var exams = _db.Where<Examination>(e =>
                e.ExaminationType == "EKG" &&
                e.CompletedDate != null);
            Console.WriteLine($"Completed EKG exams: {exams.Count}");
        }

        static async Task IncludeDemo()
        {
            Console.WriteLine("\n--- 9. INCLUDE NAVIGATION PROPERTIES ---");

            var patientWithData = _db.Query<Patient>()
                .Include(p => p.Examinations)
                .Include(p => p.Medications)
                .Include(p => p.IllnessHistories)
                .FirstOrDefault(p => p.OIB == "12345678901");

            if (patientWithData != null)
            {
                Console.WriteLine($"Patient: {patientWithData.FirstName} {patientWithData.LastName}");
                Console.WriteLine($"Exams: {patientWithData.Examinations.Count}");
                patientWithData.Examinations.ForEach(p => Console.WriteLine($"\n {p.ExaminationType}"));
                Console.WriteLine($"Meds: {patientWithData.Medications.Count}");
                Console.WriteLine($"Illnesses: {patientWithData.IllnessHistories.Count}");
            }
        }

        static async Task ChangeTrackerDemo()
        {
            Console.WriteLine("\n--- 10. CHANGE TRACKER + SAVETCHANGES ---");

            var patient = _db.GetById<Patient>(1);
            if (patient == null)
            {
                Console.WriteLine("No patient found with ID 1.");
                return;
            }

            Console.WriteLine($"Original: {patient.FirstName} {patient.LastName}");

            patient.FirstName = "Modified Marko";
            patient.LastName = "Modified Novak";

            var changesSaved = _db.SaveChanges();
            Console.WriteLine($"SaveChanges: {changesSaved} rows updated");
        }

        static async Task FluentQueryDemo()
        {
            Console.WriteLine("\n--- 11. FLUENT QUERY BUILDER ---");

            var activeMeds = _db.Query<Medication>()
                .Include(m => m.Patient)
                .ToList();

            Console.WriteLine($"Active medications: {activeMeds.Count}");
            foreach (var med in activeMeds)
            {
                Console.WriteLine($"  {med?.MedicationName} for {med?.Patient.FirstName} {med?.Patient.LastName}");
            }
        }
    }
}
