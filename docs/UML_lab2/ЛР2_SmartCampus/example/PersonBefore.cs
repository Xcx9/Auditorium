namespace BeforeRefactoring;
// Сохраненная версия до Extract Method. Она нужна для сравнения результатов.
public class PersonBefore
{
    public string Name { get; set; }
    public string Surname { get; set; }
    public string Otchestvo { get; set; }
    public PersonBefore(string name, string surname, string otchestvo)
    { Name = name; Surname = surname; Otchestvo = otchestvo; }
    public string GetFio() => Surname + " " + Name + " " + Otchestvo;
    public string FioInitials() => Surname + " "
        + (string.IsNullOrEmpty(Name) ? "" : Name.Substring(0, 1) + ".") + " "
        + (string.IsNullOrEmpty(Otchestvo) ? "" : Otchestvo.Substring(0, 1) + ".");
}
