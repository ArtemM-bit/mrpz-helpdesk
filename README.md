# Support Helpdesk API

REST API для реєстрації та читання заявок служби підтримки. Проєкти використовують .NET 10 і SQL Server.

## Підготовка сервера та бази даних

Спочатку виконайте **Configuration Task**: підготуйте сервер SQL Server і створіть порожню базу `SupportDb`. Рядок підключення має бути доступним застосунку через `ConnectionStrings:SupportDatabase`. Деталі конфігурації та застосування міграцій також наведені в [docs/Database.md](docs/Database.md).

З кореня репозиторію в PowerShell задайте рядок підключення. Нижче наведено приклад для локального SQL Server Express; для іншого сервера змініть значення відповідно до Configuration Task:

```powershell
$env:ConnectionStrings__SupportDatabase = "Server=(local)\SQLEXPRESS;Database=SupportDb;Integrated Security=True;TrustServerCertificate=True;"
```

`ConnectionStrings__SupportDatabase` — формат змінної середовища для ключа конфігурації `ConnectionStrings:SupportDatabase`. Не зберігайте реальні секрети в репозиторії.

## Відновлення, міграції та запуск

Потрібні .NET 10 SDK і доступ до налаштованого SQL Server. Для застосування міграцій потрібен інструмент EF Core CLI 10.0.13 (якщо він ще не встановлений):

```powershell
dotnet tool install --global dotnet-ef --version 10.0.13
```

Відновіть залежності та застосуйте міграції до готової порожньої бази `SupportDb`:

```powershell
dotnet restore Support.slnx
dotnet ef database update --project Support.Api --startup-project Support.Api
```

API також запускає `Database.MigrateAsync()` під час старту, але явна команда вище дає змогу застосувати міграції окремо й перевірити підключення.

Запустіть API:

```powershell
dotnet run --project Support.Api
```

Профіль `http` доступний за адресою `http://localhost:5132`. У середовищі Development OpenAPI-документ доступний за адресою `http://localhost:5132/openapi/v1.json`.

## Тести

З кореня репозиторію запустіть усі тести:

```powershell
dotnet test Support.slnx
```

## Реєстрація та читання заявки

`POST /api/tickets` приймає JSON з такими полями:

| Поле | Тип JSON | Обов'язкове | Обмеження |
| --- | --- | --- | --- |
| `title` | string | так | 1–150 символів після обрізання пробілів по краях |
| `description` | string | так | 1–4000 символів після обрізання пробілів по краях |
| `categoryId` | integer | так | ID наявної категорії; початкові категорії мають ID від 1 до 5 |
| `priority` | integer | ні | `0` = `Low`, `1` = `Medium`, `2` = `High`, `3` = `Critical`; за замовчуванням `0` (`Low`) |

Приклад запиту (пріоритет `High`):

```json
{
  "title": "Не працює принтер",
  "description": "Принтер не друкує документи після заміни картриджа.",
  "categoryId": 1,
  "priority": 2
}
```

Успішна реєстрація повертає `201 Created`, заголовок `Location` з URL заявки та JSON-відповідь. `id` і `createdAt` генерує система, а `status` встановлюється в `0` (`New`):

```http
HTTP/1.1 201 Created
Location: /api/tickets/6f9619ff-8b86-d011-b42d-00cf4fc964ff
Content-Type: application/json
```

```json
{
  "id": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
  "title": "Не працює принтер",
  "description": "Принтер не друкує документи після заміни картриджа.",
  "categoryId": 1,
  "priority": 2,
  "status": 0,
  "createdAt": "2026-01-15T10:30:00+00:00"
}
```

Читання за повернутим ID через `GET /api/tickets/{id}` повертає `200 OK` і ту саму заявку:

```http
GET /api/tickets/6f9619ff-8b86-d011-b42d-00cf4fc964ff
```

Якщо вхідні поля або категорія не проходять бізнес-валідацію, `POST` повертає `400 Bad Request` — об'єкт із масивами повідомлень для полів. Наприклад, для невідомого `categoryId`:

```json
{
  "CategoryId": [
	"Вказано невідому категорію."
  ]
}
```

Для неіснуючого коректного UUID `GET` повертає `404 Not Found` без тіла:

```http
HTTP/1.1 404 Not Found
```

## Пошук заявок і статистика категорій

### `GET /api/tickets`

Усі query-параметри пошуку необов'язкові. Різні фільтри комбінуються через **AND**. `searchText` шукає без урахування регістру в `Title` **або** `Description` (OR); пробіли на початку/в кінці обрізаються, а запит, що складається лише з пробілів, ігнорується.

| Параметр | Тип | Допустимі значення | Типова поведінка |
| --- | --- | --- | --- |
| `status` | integer | `0` = `New`, `1` = `InProgress`, `2` = `Resolved`, `3` = `Closed` | Без параметра статус не фільтрується. |
| `priority` | integer | `0` = `Low`, `1` = `Medium`, `2` = `High`, `3` = `Critical` | Без параметра пріоритет не фільтрується. |
| `categoryId` | integer | ID категорії; початкові категорії мають ID `1`–`5` | Без параметра категорія не фільтрується. Невідомий ID дасть `[]`, а не `400`. |
| `searchText` | string | Будь-який текст; порожнє або пробільне значення не додає фільтр | Без параметра текст не фільтрується. Пошук виконується в заголовку або описі. |
| `sortDirection` | string | `asc` або `desc`, регістр і зовнішні пробіли не мають значення | `desc`: `CreatedAt` за спаданням. За однакової дати `Id` сортується за зростанням. `asc` змінює напрямок дати на зростання; порядок за `Id` лишається за зростанням. |

Невідомий числовий код `status` чи `priority`, некоректний тип/значення параметра або `sortDirection`, відмінний від `asc` і `desc`, повертає `400 Bad Request`. Відсутність збігів — це успішна відповідь `200 OK` з порожнім масивом.

У початкових seed-даних є категорії з ID `1`–`5` та користувачі, але **немає заявок**. Наведені нижче записи заявок — приклади форми відповіді для даних, створених клієнтом; ID категорій і їхні назви відповідають seed-даним.

**Текстовий пошук** — шукає слово в заголовку або описі:

```http
GET /api/tickets?searchText=принтер
```

```json
[
  {
    "id": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
    "title": "Не працює принтер",
    "description": "Принтер не друкує документи після заміни картриджа.",
    "categoryId": 1,
    "priority": 2,
    "status": 0,
    "createdAt": "2026-01-15T10:30:00+00:00",
    "assigneeId": null,
    "resolvedAt": null
  }
]
```

**Комбінований фільтр** — заявка має одночасно відповідати категорії та статусу:

```http
GET /api/tickets?categoryId=1&status=0
```

Відповідь містить лише заявки категорії «обладнання» (`categoryId: 1`) зі статусом `New` (`status: 0`); у прикладі це та сама заявка принтера вище:

```json
[
  {
    "id": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
    "title": "Не працює принтер",
    "description": "Принтер не друкує документи після заміни картриджа.",
    "categoryId": 1,
    "priority": 2,
    "status": 0,
    "createdAt": "2026-01-15T10:30:00+00:00",
    "assigneeId": null,
    "resolvedAt": null
  }
]
```

**Відповідальний.** Фільтра `assigneeId` (або іншого фільтра за відповідальним) у `GET /api/tickets` немає. Відповідь `TicketResponse` завжди містить поле `assigneeId`: цілий ID призначеного працівника або `null`, якщо відповідального не призначено. Наприклад, після `PATCH /api/tickets/{id}/assignee` з `{"assigneeId":2}` звичайний пошук може повернути:

```http
GET /api/tickets?categoryId=1
```

```json
[
  {
    "id": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
    "title": "Не працює принтер",
    "description": "Принтер не друкує документи після заміни картриджа.",
    "categoryId": 1,
    "priority": 2,
    "status": 1,
    "createdAt": "2026-01-15T10:30:00+00:00",
    "assigneeId": 2,
    "resolvedAt": null
  }
]
```

**Сортування.** Для вибірки з двох заявок `asc` повертає старішу першою:

```http
GET /api/tickets?sortDirection=asc
```

```json
[
  {
    "id": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
    "title": "Не працює принтер",
    "description": "Принтер не друкує документи після заміни картриджа.",
    "categoryId": 1,
    "priority": 2,
    "status": 0,
    "createdAt": "2026-01-15T10:30:00+00:00",
    "assigneeId": null,
    "resolvedAt": null
  },
  {
    "id": "7f9619ff-8b86-d011-b42d-00cf4fc964ff",
    "title": "Не відкривається пошта",
    "description": "Не вдається відкрити робочу пошту.",
    "categoryId": 2,
    "priority": 1,
    "status": 0,
    "createdAt": "2026-01-16T10:30:00+00:00",
    "assigneeId": null,
    "resolvedAt": null
  }
]
```

Для `desc` те саме сортування за датою віддає записи у зворотному порядку:

```http
GET /api/tickets?sortDirection=desc
```

```json
[
  {
    "id": "7f9619ff-8b86-d011-b42d-00cf4fc964ff",
    "title": "Не відкривається пошта",
    "description": "Не вдається відкрити робочу пошту.",
    "categoryId": 2,
    "priority": 1,
    "status": 0,
    "createdAt": "2026-01-16T10:30:00+00:00",
    "assigneeId": null,
    "resolvedAt": null
  },
  {
    "id": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
    "title": "Не працює принтер",
    "description": "Принтер не друкує документи після заміни картриджа.",
    "categoryId": 1,
    "priority": 2,
    "status": 0,
    "createdAt": "2026-01-15T10:30:00+00:00",
    "assigneeId": null,
    "resolvedAt": null
  }
]
```

**Порожній результат** (наприклад, текст не зустрічається в жодному заголовку чи описі):

```http
GET /api/tickets?searchText=невідомий-текст-123
```

```json
[]
```

### `GET /api/statistics/categories`

Маршрут не приймає фільтрів. `200 OK` повертає масив усіх категорій за зростанням `categoryId`. Відповідні поля DTO `CategoryStatisticsResponse` у JSON: `categoryId` (`integer`, ID), `categoryName` (`string`, назва) і `ticketCount` (`integer`, кількість заявок). `ticketCount` охоплює всі чотири статуси — `New` (0), `InProgress` (1), `Resolved` (2) і `Closed` (3). Категорії без заявок включаються з `ticketCount: 0`; статистика рахується незалежно від будь-яких фільтрів `GET /api/tickets`.

```http
GET /api/statistics/categories
```

```json
[
  { "categoryId": 1, "categoryName": "обладнання", "ticketCount": 2 },
  { "categoryId": 2, "categoryName": "програмне забезпечення", "ticketCount": 1 },
  { "categoryId": 3, "categoryName": "мережа", "ticketCount": 1 },
  { "categoryId": 4, "categoryName": "доступ до облікового запису", "ticketCount": 0 },
  { "categoryId": 5, "categoryName": "інше", "ticketCount": 0 }
]
```

У прикладі чотири заявки сумарно враховані за всіма статусами; категорії 4 і 5 повернуто з нульовим лічильником. Застосування параметрів `status`, `priority`, `categoryId` чи `searchText` до пошуку заявок не змінює цей агрегат.

## Повний сценарій опрацювання заявки

Усі статуси передаються як числа: `0` = `New`, `1` = `InProgress`, `2` = `Resolved`, `3` = `Closed`. Дозволені лише переходи `New → InProgress → Resolved → Closed`. Для початку роботи заявці потрібно призначити працівника підтримки. Під час переходу в `Resolved` API встановлює `resolvedAt` поточним часом UTC; при переході в `Closed` значення зберігається. З `Closed` не можна перейти до іншого статусу, змінити відповідального чи додати коментар.

Для призначення у прикладі використано ID `2` — початкового працівника підтримки. Створення коментаря потребує поточного користувача, ID якого API читає з claim `ClaimTypes.NameIdentifier`. Перед виконанням цього кроку середовище має надати користувача з коректним claim, що відповідає наявному ID користувача (у прикладі — `1`). У цьому проєкті зараз не налаштовано механізм автентифікації, тому без такого налаштування запит створення коментаря поверне `404 Not Found`; сам клієнт не передає `authorId`.

API має бути запущене на `http://localhost:5132`. Виконайте послідовно у PowerShell:

```powershell
$baseUrl = "http://localhost:5132"
$createBody = @{
	title = "Не працює принтер"
	description = "Принтер не друкує документи після заміни картриджа."
	categoryId = 1
	priority = 2
} | ConvertTo-Json

$ticket = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/tickets" -ContentType "application/json" -Body $createBody
$ticketId = $ticket.id
$ticket.status
```

`POST /api/tickets` повертає `201 Created`, заявку зі статусом `0` (`New`) і `Location: /api/tickets/{id}`. ID і `createdAt` генерує сервер.

```powershell
$assignmentBody = @{ assigneeId = 2 } | ConvertTo-Json
$ticket = Invoke-RestMethod -Method Patch -Uri "$baseUrl/api/tickets/$ticketId/assignee" -ContentType "application/json" -Body $assignmentBody
$ticket.assigneeId

$statusBody = @{ status = 1 } | ConvertTo-Json
$ticket = Invoke-RestMethod -Method Patch -Uri "$baseUrl/api/tickets/$ticketId/status" -ContentType "application/json" -Body $statusBody
$ticket.status
```

`PATCH /assignee` повертає `200 OK` з оновленою заявкою та `assigneeId: 2`; `PATCH /status` повертає `200 OK` зі статусом `1` (`InProgress`).

За наявності налаштованого контексту користувача з `NameIdentifier = "1"` додайте коментар:

```powershell
$commentBody = @{ text = "Після перезапуску принтер працює." } | ConvertTo-Json
$comment = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/tickets/$ticketId/comments" -ContentType "application/json" -Body $commentBody
$comment
```

`POST /comments` повертає `201 Created` з `TicketCommentResponse`: `id` генерується сервером, `text` обрізається по краях, `authorId` дорівнює ID з claim, `createdAt` встановлюється сервером. `GET /comments` повертає масив коментарів у порядку `createdAt` за зростанням, а для однакового часу — `id` за зростанням; якщо заявка чи коментарі відсутні, відповідь — порожній масив.

```powershell
$statusBody = @{ status = 2 } | ConvertTo-Json
$ticket = Invoke-RestMethod -Method Patch -Uri "$baseUrl/api/tickets/$ticketId/status" -ContentType "application/json" -Body $statusBody
$ticket.status
$ticket.resolvedAt

$statusBody = @{ status = 3 } | ConvertTo-Json
$ticket = Invoke-RestMethod -Method Patch -Uri "$baseUrl/api/tickets/$ticketId/status" -ContentType "application/json" -Body $statusBody
$ticket.status
$ticket.resolvedAt
```

Після переходу в `Resolved` очікується `status: 2` та заповнений `resolvedAt` у UTC. Після `Closed` очікується `status: 3` і те саме значення `resolvedAt`.

Заборонений перехід, наприклад із `Closed` назад у `Resolved`, повертає `409 Conflict` без тіла:

```http
PATCH /api/tickets/{id}/status
Content-Type: application/json

{"status":2}

HTTP/1.1 409 Conflict
```

До завершення заявки цей самий код `409` повертається, якщо спробувати перевести `New` у `InProgress` без призначеного працівника. Некоректне значення статусу повертає `400`, а коректний, але недозволений перехід — `409`.

## Перевірка збереження після перезапуску API

Виконайте команди в одному вікні PowerShell, залишивши змінну `$ticketId` між зупинкою та повторним запуском API. API має бути запущене на `http://localhost:5132`.

1. Створіть заявку та перевірте статус і заголовок `Location`:

   ```powershell
   $baseUrl = "http://localhost:5132"
   $body = '{"title":"Не працює принтер","description":"Перевірка збереження заявки.","categoryId":1,"priority":2}'
   $created = Invoke-WebRequest -Method Post -Uri "$baseUrl/api/tickets" -ContentType "application/json" -Body $body
   $created.StatusCode
   $created.Headers.Location
   $ticket = $created.Content | ConvertFrom-Json
   $ticketId = $ticket.id
   ```

   Очікується код `201`, а `$ticketId` міститиме ID з відповіді.

2. Прочитайте створену заявку:

   ```powershell
   Invoke-RestMethod -Uri "$baseUrl/api/tickets/$ticketId"
   ```

3. Зупиніть API клавішами `Ctrl+C` і запустіть його знову командою `dotnet run --project Support.Api`.

4. Повторіть GET з тим самим ID:

   ```powershell
   Invoke-RestMethod -Uri "$baseUrl/api/tickets/$ticketId"
   ```

   Заявка має повернутися з тими самими даними, оскільки збережена в SQL Server, а не в пам'яті процесу.