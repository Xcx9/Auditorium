# Архитектура и контракты backend

Документ описывает реализацию Core и Infrastructure на 24 ноября 2026 года. Исходная проверенная версия backend — коммит f737b915f9d2e7b2fb93173c28803bedcdbe781e. Полный набор из 58 тестов прошёл 23 ноября. Соответствие диаграммам и принятые отличия приведены в [карте UML](uml-code-map.md).

## Границы слоёв

| Слой | Ответственность | Основные файлы |
| --- | --- | --- |
| Core | Сущности, правила занятости и отметок, регистрация и проверка учётных данных, интерфейсы хранения и источника | [Entities](../../Auditorium.Core/Entities/), [CampusService](../../Auditorium.Core/Services/CampusService.cs), [AuthService](../../Auditorium.Core/Services/AuthService.cs), [Interfaces](../../Auditorium.Core/Interfaces/) |
| Infrastructure | EF Core, SQLite, транзакционное сохранение расписания, HTTP-парсер МИЭТ, адаптер хэширования и московские часы | [AppDbContext](../../Auditorium.Infrastructure/Data/AppDbContext.cs), [CampusStore](../../Auditorium.Infrastructure/Data/CampusStore.cs), [ScheduleSnapshotStore](../../Auditorium.Infrastructure/Data/ScheduleSnapshotStore.cs), [ScheduleParser](../../Auditorium.Infrastructure/Scheduling/ScheduleParser.cs) |
| Web | Композиция DI, HTTP, Razor, формы, cookies/claims, авторизация и CSRF, фоновые обработчики | [Program.cs](../../Auditorium.Web/Program.cs), задачи #39, #43–#49 |

Infrastructure зависит от Core. Core не содержит PackageReference на EF Core. Web выбирает реализации интерфейсов и создаёт область DI на запрос или фоновую операцию. Один DbContext используется одной последовательной операцией. Для одновременных запросов создаются независимые контексты и соединения.

В текущем Program.cs зарегистрированы часы, CampusService, парсер и обновление расписания. Для подключения входа Web-команда регистрирует AuthService, IUserRepository/UserRepository и IPasswordHasher/IdentityPasswordHasher. Проверку занятности выполняет CampusService. Web передаёт UserId аутентифицированного пользователя из claims.

## Модель и время

Room хранит Id, Number, SourceScheduleCode и ScheduleDates. Номер содержит четыре ASCII-цифры, первая из которых от 1 до 9. После цифр допустимы буквы, пробел, скобки и дефис. Длина после обрезки внешних пробелов — от 4 до 20 символов. Управляющие символы отклоняются. Поддерживаются обозначения 3102а, 3120 а (к) и 3209 МПСУ. Building вычисляется из первой цифры. Новое создание и обновление принимают цифровые обозначения, а старые именованные записи сохраняются при миграции.

Lesson активен в интервале StartsAt <= now < EndsAt. Активная OccupancyMark имеет ClosedAt == null. На пользователя и аудиторию допускается по одной активной отметке. История закрытий сохраняется. AutoReleaseAt — начало ближайшего занятия после CreatedAt, либо null. При null срок жизни отметки автоматически не ограничивается.

Сервис получает время через IClock. MoscowClock определяет календарную дату в Europe/Moscow. SQLite хранит рабочие DateTimeOffset как UTC ticks, при чтении возвращается смещение +00:00. Для показа московского времени требуется преобразование часового пояса.

ScheduleDates содержит подтверждённые даты, включая дни без пар. При отсутствии сегодняшней даты статус не публикуется, создание запрещено. При покрытии статус Busy означает занятие или активную отметку, Free — отсутствие обеих причин. Room.Status(date, isOccupied) выполняет расчёт, RoomStatusInfo.State передаёт его результат потребителю. Значение null обозначает отсутствие данных.

## Чтение через CampusService

Публичные методы находятся в [CampusService.cs](../../Auditorium.Core/Services/CampusService.cs). Перед основными чтениями и изменениями сервис обрабатывает наступившее автоснятие.

| Метод | Результат и смысл |
| --- | --- |
| GetRooms() | List<RoomStatusInfo> со всем каталогом из БД. State может быть null. Включает сохранённые старые именованные записи |
| FindFree(int building, string? numberPart = null) | FreeRoomSearchResult. Корпус обязателен. Часть номера обрезается по краям и сравнивается без учёта регистра |
| GetRoom(int roomId) | RoomDetailsResult? — null, если аудитория отсутствует. Найденная аудитория без покрытия сохраняется в RoomInfo со State == null |
| GetOwnMark(int userId) | OwnMarkDto? — данные активной отметки, либо null. Содержит MarkId, RoomId, RoomNumber, CreatedAt и AutoReleaseAt |
| GetStatusNow(Room room) | Free или Busy. При отсутствии покрытия выбрасывает InvalidOperationException. Для карточки с отсутствующим расписанием используется GetRoom |
| FindNearOfficialLesson(int roomId) | DateTimeOffset? — первое начало занятия строго после текущего времени, либо null |

FreeRoomSearchResult передаёт Rooms, MatchingRoomCount, RoomsWithoutScheduleCount, HasScheduleCoverage и ScheduleState. Rooms содержит только подтверждённо свободные аудитории. MatchingRoomCount считается после фильтра по корпусу/номеру до отбора свободных. RoomsWithoutScheduleCount считает совпадения без покрытия даты. HasScheduleCoverage означает, что покрытие есть хотя бы у одной совпавшей аудитории.

При MatchingRoomCount == 0 фильтрам не соответствует каталог. Если все совпадения учтены в RoomsWithoutScheduleCount, расписания для них нет. Если часть совпадений без покрытия, Web может показать предупреждение о неполных данных вместе со свободными аудиториями.

RoomDetailsResult содержит RoomInfo и ScheduleState. ScheduleSnapshotState содержит LastSuccessfulUpdate и UpdateFailed. Последняя попытка обновления может завершиться ошибкой при наличии покрытой старой копии. Показывать предупреждение и время обновления решает Web. LastSuccessfulUpdate == null означает отсутствие зафиксированного успешного импорта. Статус аудитории определяется покрытием даты, а не возрастом копии.

## Изменение отметок

CreateMark(userId, roomId) и CancelMark(userId, markId) возвращают CampusOperationResult с Code, Message и вычисляемым IsSuccess. Web может использовать готовое Message или локализовать текст по Code.

| Код | Значение |
| --- | --- |
| Success | Изменение выполнено |
| UserNotFound, RoomNotFound | Переданного пользователя или аудитории нет |
| ScheduleNotCovered | Создание запрещено из-за отсутствия расписания на сегодня |
| UserAlreadyHasActiveMark | До вставки найдена активная отметка пользователя |
| RoomAlreadyOccupied | До вставки установлена занятость аудитории |
| ConcurrentConflict | Вставка отклонена уникальным ограничением после предварительных проверок |
| MarkNotFound | Указанная отметка отсутствует |
| NotOwner | Указанная отметка принадлежит другому пользователю |
| MarkAlreadyClosed | Отметка уже закрыта, в том числе другим запросом |

CreateMark берёт серверное время в начале вызова, обрабатывает автоснятие, проверяет пользователя, аудиторию, покрытие, активные отметки и пары, вычисляет AutoReleaseAt и пытается сохранить запись. Два частичных уникальных индекса SQLite защищают активные UserId и RoomId. Нарушение уникальности преобразуется Infrastructure в управляемый отказ.

С пользователем согласовано создание без общей транзакции чтения и вставки. Расписание загружается до начала использования, решение принимается по времени начала запроса. Одновременные создания следуют правилу первого успешного сохранения. Это отличается от транзакции на весь CreateMark в диаграммах 06/10.

Web передаёт в CancelMark тот MarkId, который был возвращён GetOwnMark и показан пользователю. Сервис проверяет владельца и закрытие, затем условный UPDATE проверяет Id, UserId и ClosedAt IS NULL в момент записи. Старая вкладка не снимает новую отметку. Закрытие не требует покрытия расписанием. При серверном времени раньше CreatedAt выбрасывается InvalidOperationException.

ReleaseDueMarks() использует часы сервиса. Перегрузка ReleaseDueMarks(now) предназначена для серверного обработчика. Условный UPDATE закрывает только активные записи с AutoReleaseAt <= now, устанавливая ClosedAt = AutoReleaseAt. Ручное снятие и этот обработчик не перезаписывают уже заполненный ClosedAt.

Ошибки подключения, тайм-ауты блокировок и другие ошибки БД могут распространяться как исключения. ConcurrentConflict обрабатывает нарушение уникальности вставки. Общая обработка сбоев HTTP и повторов относится к Web. Методы CampusService/ICampusStore пока не принимают CancellationToken.

## Авторизация

[AuthService](../../Auditorium.Core/Services/AuthService.cs) предоставляет RegisterAsync(login, password, cancellationToken) и LoginAsync(login, password, cancellationToken). AuthResult содержит User, Error и Succeeded. AuthUser содержит только Id и Login. AuthError принимает None, InvalidLogin, InvalidPassword, LoginTaken или InvalidCredentials.

Логин нормализуется через Trim и нижний регистр. Допускаются 3–100 ASCII-символов из букв, цифр, точки, дефиса и подчёркивания. Регистрация принимает пароль длиной 8–128 символов, не состоящий только из пробелов, без управляющих символов. Пароль не обрезается. LoginAsync проверяет старые пароли независимо от новой минимальной длины. Полные правила находятся в [AuthRules](../../Auditorium.Core/Authentication/AuthRules.cs).

[IdentityPasswordHasher](../../Auditorium.Infrastructure/Security/IdentityPasswordHasher.cs) использует стандартный Microsoft PasswordHasher с IdentityV3. Репозиторий преобразует конфликт логина в LoginTaken. Неверный пароль и отсутствие пользователя возвращают InvalidCredentials. Web создаёт сессию по AuthUser.Id после успешного ответа. Формы, cookies и CSRF ведутся в #44.

## Источник и обновление расписания

IScheduleParser.LoadAndValidate принимает ScheduleLoadRequest с Semester, From и Through. Обе даты включительны, допустим период до 28 дней. Парсер получает каталог МИЭТ и расписание цифровых аудиторий 3-го и 4-го корпусов. Частичный/некорректный ответ отклоняется. Сетевые ответы в постоянных тестах заменены сохранёнными примерами.

ScheduleLoadResult.Loaded содержит проверенные аудитории и занятия. Failed содержит InvalidPeriod, SourceUnavailable либо InvalidResponse и сообщение. Отмена через CancellationToken распространяется вызывающему коду.

ScheduleUpdater.Refresh(request, cancellationToken) возвращает ScheduleRefreshResult со State и Error. Succeeded означает Error == null. При ошибке сохраняются прежняя копия и время последнего успеха, фиксируется UpdateFailed. При ошибке хранения метаданных State может быть null. GetSnapshotAsync возвращает Rooms, Lessons и метаданные из постоянного хранилища.

ScheduleSnapshotStore заменяет каталог/занятия/покрытие и метаданные в одной транзакции, сохраняет идентификаторы аудиторий по SourceScheduleCode и пересчитывает AutoReleaseAt. Аудитории, исчезнувшие из нового каталога, сохраняются с историей и пустым покрытием.

Запуск обновления при старте и затем с интервалом не более часа, scopes DbContext и отдельный обработчик автоснятия реализует Web-команда в #43. Сейчас имеется разовая команда refresh-schedule. Команды подготовки базы и импорта приведены в [инструкции миграции](../database-migration.md).

## Проверки и передача Web

Команда dotnet test Auditorium.slnx запускает 58 проверок. CampusServiceTests проверяет операции, отсутствие/частичное покрытие, старую вкладку и гонки независимых SQLite-соединений. RoomTests проверяет формат и московскую дату. ScheduleParserTests, ScheduleUpdaterTests, ScheduleMigrationTests и Authentication покрывают импорт, восстановление, миграции и учётные данные.

Web-интеграция и её проверки ведутся в #39/#43–#49. Для передачи контрактов служат этот документ и [карта диаграмм](uml-code-map.md). Изменения публичных DTO требуют согласования с потребителями.
