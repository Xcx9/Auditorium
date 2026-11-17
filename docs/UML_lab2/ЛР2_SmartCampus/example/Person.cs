// Доработка сгенерированного каркаса из Generated/Person.g.cs.
// Другой namespace позволяет сохранить и показать исходные заглушки.
namespace CompletedExample;
public class Person
{
    public string Name { get; set; }
    public string Surname { get; set; }
    public string Otchestvo { get; set; }
    public Person(string name, string surname, string otchestvo)
    {
        Name = name;
        Surname = surname;
        Otchestvo = otchestvo;
    }
    public string GetFio() => Surname + " " + Name + " " + Otchestvo;
    public string FioInitials() => Surname + " " + Initial(Name) + " " + Initial(Otchestvo);
    public string ClassDescription() => "Класс Person. Хранит данные о человеке.";
    // Рефакторинг Extract Method: повторяющуюся операцию выделили в метод.
    // Поведение для непустых и пустых строк совпадает с версией «до».
    private string Initial(string text) => string.IsNullOrEmpty(text) ? "" : text.Substring(0, 1) + ".";
}
