using CustomORM;
using Models;

var connectionString = "Host=localhost;Username=postgres;Password=pass;Database=medical;Port=5432";
var db = new CustomDbContext(connectionString);

try
{
    Console.WriteLine("Kreiram tablice...\n");
    await db.InitializeDatabaseAsync(typeof(Doctor), typeof(Patient), typeof(IllnessHistory), typeof(Medication), typeof(Examination));

    Console.WriteLine("\nSeeding doktora...");
    var doc1 = new Doctor { FirstName = "Marko", LastName = "Horvat", Specialization = "Kardiolog" };
    var doc1Id = db.Create(doc1);
    Console.WriteLine($"Doktor 1 ID: {doc1Id}");

    var doc2 = new Doctor { FirstName = "Ana", LastName = "Novak", Specialization = "Neurologinja" };
    var doc2Id = db.Create(doc2);
    Console.WriteLine($"Doktor 2 ID: {doc2Id}");

    Console.WriteLine("\nKreiram pacijenta...");
    var patient = new Patient
    {
        OIB = "12345678901",
        FirstName = "Ivan",
        LastName = "Horvat",
        BirthDate = new DateTime(1990, 5, 15),
        Gender = "M",
        ResidenceAddress = "Zagreb, Glavna 1",
        HomeAddress = "Zagreb, Brana 5"
    };
    var patientId = db.Create(patient);
    Console.WriteLine($"Pacijent ID: {patientId}");

    Console.WriteLine("\nDodajem povijest bolesti...");
    var illness = new IllnessHistory
    {
        PatientId = patientId,
        DoctorId = doc1Id,
        IllnessName = "Bol u prsima",
        StartDate = DateTime.Now.AddMonths(-2),
        EndDate = DateTime.Now,
        Notes = "Tretiran s medicinama"
    };
    var illnessId = db.Create(illness);
    Console.WriteLine($"Bolest ID: {illnessId}");

    Console.WriteLine("\nDodajem lijek...");
    var medication = new Medication
    {
        PatientId = patientId,
        MedicationName = "Aspirin",
        DoseValue = 100,
        DoseUnit = "mg",
        Frequency = "2x dnevno",
        PrescribedDate = DateTime.Now.AddMonths(-2),
        Notes = "Srčana zaštita"
    };
    var medId = db.Create(medication);
    Console.WriteLine($"Lijek ID: {medId}");

    Console.WriteLine("\nZakazujem pregled...");
    var exam = new Examination
    {
        PatientId = patientId,
        DoctorId = doc1Id,
        ExaminationType = "EKG",
        ScheduledDate = DateTime.Now.AddDays(7),
        Notes = "Rutinska provjera"
    };
    var examId = db.Create(exam);
    Console.WriteLine($"Pregled ID: {examId}");

    Console.WriteLine("\n\n========== PREGLED IZ BAZE ==========");
    var allPatients = db.GetAll<Patient>();
    var allDoctors = db.GetAll<Doctor>();
    var allMeds = db.GetAll<Medication>();
    var allExams = db.GetAll<Examination>();
    var allIllnesses = db.GetAll<IllnessHistory>();

    Console.WriteLine($"\nPacijenti ({allPatients.Count}):");
    foreach (var p in allPatients)
        Console.WriteLine($"{p.FirstName} {p.LastName} | OIB: {p.OIB} | Rođen: {p.BirthDate:yyyy-MM-dd}");

    Console.WriteLine($"\nLiječnici ({allDoctors.Count}):");
    foreach (var d in allDoctors)
        Console.WriteLine($"   {d.FirstName} {d.LastName} | {d.Specialization}");

    Console.WriteLine($"\nLijekovi ({allMeds.Count}):");
    foreach (var m in allMeds)
        Console.WriteLine($"   {m.MedicationName} {m.DoseValue}{m.DoseUnit} | {m.Frequency}");

    Console.WriteLine($"\nBolesti ({allIllnesses.Count}):");
    foreach (var i in allIllnesses)
        Console.WriteLine($"   {i.IllnessName} | {i.StartDate:yyyy-MM-dd} do {i.EndDate:yyyy-MM-dd}");

    Console.WriteLine($"\nPregledi ({allExams.Count}):");
    foreach (var e in allExams)
        Console.WriteLine($"   {e.ExaminationType} zakazan za {e.ScheduledDate:yyyy-MM-dd}");

    Console.WriteLine("\nORM TEST USPJEŠAN!");
}
catch (Exception ex)
{
    Console.WriteLine($"\nGreška: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}
