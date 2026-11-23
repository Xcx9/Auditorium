# Соответствие UML и реализации

Сверка выполнена 24 ноября 2026 года для backend f737b915f9d2e7b2fb93173c28803bedcdbe781e. Целевой комплект находится в [diagrams](../UML_lab2/ЛР2_SmartCampus/diagrams/). Согласованные особенности публичных методов описаны в [контрактах backend](backend-contracts.md).

## Карта всех диаграмм

| Диаграмма | Реализация и состояние | Задачи |
| --- | --- | --- |
| [00 — use-case системы](../UML_lab2/ЛР2_SmartCampus/diagrams/00_usecase_complete.puml) | Core предоставляет операции поиска, карточки, отметок, входа и обновления. HTTP-сценарии и фоновые адаптеры относятся к Web | #33–#40, #49 |
| [01 — действия пользователя](../UML_lab2/ЛР2_SmartCampus/diagrams/01_usecase_user.puml) | CampusService и AuthService реализованы. Экраны и переходы пользователя подключаются в Web | #35, #38, #39, #44 |
| [02 — фоновые действия](../UML_lab2/ЛР2_SmartCampus/diagrams/02_usecase_background.puml) | ScheduleUpdater.Refresh и CampusService.ReleaseDueMarks доступны. Планировщики выполняются в Web | #37, #43 |
| [03 — доменные классы](../UML_lab2/ЛР2_SmartCampus/diagrams/03_class_domain.puml) | [Entities](../../Auditorium.Core/Entities/) содержат User, Room, Lesson, OccupancyMark, ScheduleSnapshot. [AppDbContext](../../Auditorium.Infrastructure/Data/AppDbContext.cs) задаёт связи и ограничения | #33, #34 |
| [04 — сервисы](../UML_lab2/ЛР2_SmartCampus/diagrams/04_class_services.puml) | [CampusService](../../Auditorium.Core/Services/CampusService.cs), [AuthService](../../Auditorium.Core/Services/AuthService.cs), [ScheduleUpdater](../../Auditorium.Core/Services/ScheduleUpdater.cs), [IScheduleParser](../../Auditorium.Core/Interfaces/IScheduleParser.cs), [IClock](../../Auditorium.Core/Interfaces/IClock.cs). Интерфейсы хранения — Core, адаптеры — Infrastructure | #32, #35–#38, #48 |
| [05 — пользовательский процесс](../UML_lab2/ЛР2_SmartCampus/diagrams/05_activity_user.puml) | Операции Core и их отказы покрыты тестами. Сквозной браузерный процесс зависит от Web | #35, #38, #39, #44, #45 |
| [06 — создание отметки](../UML_lab2/ЛР2_SmartCampus/diagrams/06_activity_create.puml) | CreateMark проверяет покрытие и занятость, назначает AutoReleaseAt. CampusStore.AddMark сохраняет отметку и обрабатывает уникальность. Согласованное отличие от общей транзакции описано ниже | #35, #40 |
| [07 — обновление](../UML_lab2/ЛР2_SmartCampus/diagrams/07_activity_sync.puml) | Парсер принимает только проверенного кандидата. ScheduleSnapshotStore сохраняет его транзакционно и фиксирует сбой без очистки копии | #36, #37 |
| [08 — регистрация и вход](../UML_lab2/ЛР2_SmartCampus/diagrams/08_sequence_auth.puml) | AuthService, UserRepository и IdentityPasswordHasher реализованы. HTTP-сессия/claims относятся к Web | #38, #44 |
| [09 — поиск и карточка](../UML_lab2/ЛР2_SmartCampus/diagrams/09_sequence_search.puml) | FindFree(building, numberPart), GetRoom(roomId), FreeRoomSearchResult и RoomDetailsResult возвращают статус/покрытие и метаданные. GetRooms дополнительно возвращает полный каталог | #35, #39 |
| [10 — создание и конфликт](../UML_lab2/ЛР2_SmartCampus/diagrams/10_sequence_create.puml) | Частичные уникальные индексы UserId/RoomId защищают активные отметки. ConcurrentConflict возвращается при конфликте вставки. Оба конкурентных сценария покрыты тестами | #34, #35, #40 |
| [11 — своя отметка и снятие](../UML_lab2/ЛР2_SmartCampus/diagrams/11_sequence_cancel.puml) | GetOwnMark возвращает OwnMarkDto? с MarkId. CancelMark(userId, markId) проверяет автора и закрывает только активную запись условным UPDATE. История сохраняется | #35, #40 |
| [12 — автоснятие](../UML_lab2/ЛР2_SmartCampus/diagrams/12_sequence_auto.puml) | ReleaseDueMarks закрывает наступившие отметки временем AutoReleaseAt. Пересчёт срока при импорте выполняет ScheduleSnapshotStore. Занятие продолжает делать аудиторию занятой | #35, #37, #40, #43 |
| [13 — загрузка и резервная копия](../UML_lab2/ЛР2_SmartCampus/diagrams/13_sequence_sync.puml) | ScheduleParser, ScheduleUpdater и ScheduleSnapshotStore реализуют валидацию, постоянную копию, ошибку обновления и пересчёт срока. Фоновый запуск — Web | #36, #37, #43 |
| [14 — Person](../UML_lab2/ЛР2_SmartCampus/diagrams/14_example_person.puml) | Учебный пример в example/. Отдельный проект, вне Auditorium.slnx | #41 |

## Согласованные отличия и границы проверки

Room.Building вычисляется по первой цифре номера. Room.Status(date, isOccupied) является методом с переданным временем/признаком занятости. Результат передаётся через RoomStatusInfo.State. Free/Busy используются при покрытой дате, null обозначает отсутствие данных. Сохранённого изменяемого статуса Room нет. Строковые значения диаграммы представлены enum и nullable в C#.

CreateMark реализован без транзакции, охватывающей все чтения и вставку из диаграмм 06/10. Пользователь согласовал предварительную загрузку расписания до начала использования, серверное время начала вызова и правило первого успешного сохранения. Два уникальных индекса и обработка конфликта защищают активные UserId/RoomId. Атомарное согласование чтений со сменой копии во время запроса не заявляется.

Снятие и наступившее автоснятие используют условные UPDATE. При гонке они сохраняют время первого закрытия. Обновление расписания, его покрытия и метаданных остаётся транзакционным.

FindFree возвращает счётчик RoomsWithoutScheduleCount для частично покрытого каталога. Карточка найденной аудитории сохраняется без статуса при отсутствии данных. Метаданные UpdateFailed и LastSuccessfulUpdate помогают Web показать состояние источника. Тексты и формы отображения находятся в области #39/#46.

58 постоянных тестов проверяют согласованные Core/Infrastructure-сценарии, включая SQLite-конкуренцию и миграции. HTTP, cookies, CSRF и браузерные переходы должны подтверждаться Web-тестами #45. Исходники backend опубликованы в feature/core-logic, документационный выпуск переносит этот backend в dev. Готовность main проверяется по включению соответствующих коммитов.

## Изоляция учебного комплекта

[Auditorium.slnx](../../Auditorium.slnx) содержит Core, Infrastructure, Web и Tests. Учебные app/SmartCampus.Demo.csproj и example/PersonExample.csproj находятся в собственных каталогах. Их Generated/Domain.g.cs и Person*.cs не включены в рабочие проекты.

В учебном app/CampusService.cs хранение реализовано списками с lock. Его GetRoom возвращает null при отсутствии аудитории или покрытия даты. В рабочем CampusService.GetRoom отсутствие аудитории возвращает null, а отсутствие расписания сохраняет карточку с RoomInfo.State == null. Демо иллюстрирует правила и не заменяет SQLite и её проверки.

Рабочей основой служат диаграммы из docs/UML_lab2/ЛР2_SmartCampus/diagrams/. Старый корневой UML и conflict_demo.txt отсутствуют в проверенной версии. Исторические миграции и учебные проекты сохранены.

Сопоставление закрывает Core-сверку #25. Передача Web осуществляется через этот документ и [контракты](backend-contracts.md), связанные с #47/#48. Документация экранов и запуска Web ведётся в #46.
