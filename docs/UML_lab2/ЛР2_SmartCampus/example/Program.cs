using CompletedExample;
using BeforeRefactoring;
using System.Text;
Console.OutputEncoding = Encoding.UTF8;
// В методичке аргументы имени и фамилии в вызовах перепутаны.
// Здесь порядок согласован с конструктором: имя, фамилия, отчество.
var people = new[] {
    new Person("Александр", "Пушкин", "Сергеевич"),
    new Person("Наталья", "Гончарова", "Николаевна"),
    new Person("", "БезИмени", "")
};
foreach (var person in people)
{
    Console.WriteLine(person.GetFio());
    Console.WriteLine(person.FioInitials());
    var before = new PersonBefore(person.Name, person.Surname, person.Otchestvo);
    if (before.GetFio() != person.GetFio() || before.FioInitials() != person.FioInitials())
        throw new Exception("Рефакторинг изменил результат");
}
Console.WriteLine(people[0].ClassDescription());
Console.WriteLine("PASS: refactoring preserves results for 3 input sets");
