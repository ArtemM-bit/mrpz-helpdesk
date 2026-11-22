# Support Helpdesk API

REST API служби підтримки на .NET 10 і SQL Server. Через API можна створити заявку, призначити виконавця, змінити статус і переглянути коментарі.

## Локальний запуск

Для запуску потрібні .NET 10 SDK і доступ до SQL Server.

1. Підготуйте порожню базу `SupportDb` у SQL Server.
2. У PowerShell відкрийте корінь репозиторію та задайте рядок підключення. Приклад для локального SQL Server Express з автентифікацією Windows.

   ```powershell
   $env:ConnectionStrings__SupportDatabase = "Server=(local)\SQLEXPRESS;Database=SupportDb;Integrated Security=True;TrustServerCertificate=True;"
   ```

3. У тому самому вікні відновіть залежності та запустіть API в профілі `http`.

   ```powershell
   dotnet restore Support.slnx
   dotnet run --project Support.Api --launch-profile http
   ```

Під час запуску API застосує міграції та додасть початкові категорії й користувачів. Якщо потрібно застосувати міграції окремо, встановіть EF Core CLI командою `dotnet tool install --global dotnet-ef --version 10.0.13` і скористайтеся [інструкцією для бази даних](docs/Database.md).

Базова адреса API — `http://localhost:5132`. У середовищі `Development` доступний [Swagger UI](http://localhost:5132/swagger), де можна переглядати маршрути, схеми, приклади запитів і відповідей та виконувати запити через **Try it out → Execute**. Профілі `http` та `https` налаштовані відкривати сторінку `swagger` у браузері. Для профілю `https` адреса Swagger UI — `https://localhost:7230/swagger`.

Swagger UI використовує наявну [специфікацію OpenAPI](http://localhost:5132/openapi/v1.json). Swagger UI та специфікація доступні лише в `Development`.

## Маршрути API

У маршрутах нижче `{id}` — UUID заявки, який повертає `POST /api/tickets`. Для запитів із JSON вкажіть заголовок `Content-Type: application/json`.

### `POST /api/tickets`

Створює заявку зі статусом `0` (`New`). Сервер генерує `id` і `createdAt`.

| Поле | Тип JSON | Обов’язкове | Правило |
| --- | --- | --- | --- |
| `title` | Рядок | Так | Від 1 до 150 символів. |
| `description` | Рядок | Так | Від 1 до 4000 символів. |
| `categoryId` | Ціле число | Так | ID наявної категорії. Початкові ID — від `1` до `5`. |
| `priority` | Ціле число | Ні | `0` (`Low`, за замовчуванням), `1` (`Medium`), `2` (`High`), `3` (`Critical`). |

API перевіряє максимальну довжину `title` і `description` до обрізання пробілів. Перед збереженням обрізає пробіли по краях і перевіряє, що поля залишилися непорожніми.

**Приклад запиту**

```http
POST /api/tickets
Content-Type: application/json

{
  "title": "Не працює принтер",
  "description": "Принтер не друкує після заміни картриджа.",
  "categoryId": 1,
  "priority": 2
}
```

**Відповіді**

| Код | Значення |
| --- | --- |
| `201 Created` | Заявку створено. Заголовок `Location` містить `/api/tickets/{id}`, а тіло — створену заявку. |
| `400 Bad Request` | Запит або значення поля некоректні. |
| `500 Internal Server Error` | Сталася помилка під час роботи з базою даних. |

**Приклад тіла відповіді `201`**

```json
{
  "id": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
  "title": "Не працює принтер",
  "description": "Принтер не друкує після заміни картриджа.",
  "categoryId": 1,
  "priority": 2,
  "status": 0,
  "createdAt": "2026-11-18T10:30:00+00:00",
  "assigneeId": null,
  "resolvedAt": null
}
```

Помилки, які виявив сервіс, повертаються як об’єкт із повідомленнями за полями. Наприклад, для невідомої категорії відповідь `400` містить `{"CategoryId":["Вказано невідому категорію."]}`. Помилки, які ASP.NET Core виявив ще до виклику сервісу, повертаються у форматі `ValidationProblemDetails` із полем `errors`.

### `GET /api/tickets/{id}`

Повертає збережену заявку за ID. Тіло запиту не потрібне.

```http
GET /api/tickets/6f9619ff-8b86-d011-b42d-00cf4fc964ff
```

- `200 OK` — знайдена заявка у тому самому форматі, що й відповідь на `POST /api/tickets`.
- `404 Not Found` — заявки з таким ID немає.

### `GET /api/tickets`

Повертає масив заявок. Усі параметри необов’язкові; різні фільтри комбінуються через **AND**, а `Search` шукає текст у заголовку або описі (**OR**). Результат без збігів — `200 OK` з порожнім масивом `[]`.

| Параметр | Тип | Допустимі значення та поведінка |
| --- | --- | --- |
| `Search` | Рядок | Пошук без урахування регістру в `Title` або `Description`. Пробіли по краях обрізаються, порожнє чи пробільне значення ігнорується; спеціальні символи SQL `LIKE` шукаються буквально. |
| `CategoryId` | Ціле число | Додатний ID категорії; початкові ID — `1`–`5`. Невідомий додатний ID не є помилкою й не дає збігів. |
| `Priority` | Ціле число | Визначений код: `0` (`Low`), `1` (`Medium`), `2` (`High`), `3` (`Critical`). |
| `Status` | Ціле число | Визначений код: `0` (`New`), `1` (`InProgress`), `2` (`Resolved`), `3` (`Closed`). |
| `AssigneeId` | Ціле число | Додатний ID відповідального працівника. Невідомий додатний ID не є помилкою й не дає збігів. |
| `SortDirection` | Рядок | `asc` або `desc`; регістр і пробіли по краях не мають значення. За замовчуванням — `desc`. Сортує за `CreatedAt`; при однаковій даті `Id` сортується за зростанням в обох напрямках. |

Некоректні значення параметрів, недодатні `CategoryId` чи `AssigneeId`, невизначені коди статусу/пріоритету або `SortDirection`, відмінний від `asc` і `desc`, повертають `400 Bad Request` у форматі `ValidationProblemDetails` з описом помилки. Без фільтрів повертаються всі заявки, спочатку найновіші.

**Текстовий пошук** — шукає слово в заголовку або описі:

```http
GET /api/tickets?Search=принтер
```

Приклад відповіді `200 OK` (елемент містить усі поля DTO заявки):

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

**Комбіновані фільтри** — заявка має одночасно відповідати кожному переданому параметру:

```http
GET /api/tickets?CategoryId=1&Status=0&Priority=2
```

**Відповідальний** — після призначення виконавця запит може виглядати так:

```http
GET /api/tickets?CategoryId=1&AssigneeId=2
```

**Сортування** — `asc` повертає старіші заявки першими; `desc` — новіші першими:

```http
GET /api/tickets?SortDirection=asc
GET /api/tickets?SortDirection=desc
```

**Порожній результат** (наприклад, тексту немає в жодному заголовку чи описі):

```http
GET /api/tickets?Search=невідомий-текст-123
```

```json
[]
```

### `PATCH /api/tickets/{id}/assignee`

Призначає або замінює виконавця. У полі `assigneeId` передайте цілий ID працівника підтримки. Початковий працівник підтримки має ID `2`.

```http
PATCH /api/tickets/6f9619ff-8b86-d011-b42d-00cf4fc964ff/assignee
Content-Type: application/json

{"assigneeId": 2}
```

- `200 OK` — оновлена заявка з новим `assigneeId`.
- `400 Bad Request` — користувача з таким ID немає або він не є працівником підтримки.
- `404 Not Found` — заявку не знайдено.
- `409 Conflict` — заявку закрито.

### `PATCH /api/tickets/{id}/status`

Змінює статус заявки. У полі `status` передайте числовий код наступного статусу.

| Поточний статус | Наступний статус | Умова або результат |
| --- | --- | --- |
| `0` (`New`) | `1` (`InProgress`) | Заявці вже призначено виконавця. |
| `1` (`InProgress`) | `2` (`Resolved`) | Сервер записує поточний час UTC у `resolvedAt`. |
| `2` (`Resolved`) | `3` (`Closed`) | Значення `resolvedAt` зберігається. |

```http
PATCH /api/tickets/6f9619ff-8b86-d011-b42d-00cf4fc964ff/status
Content-Type: application/json

{"status": 1}
```

- `200 OK` — оновлена заявка.
- `400 Bad Request` — невідомий код статусу.
- `404 Not Found` — заявку не знайдено.
- `409 Conflict` — перехід заборонено або для переходу в `InProgress` бракує виконавця. Повторне встановлення поточного статусу також повертає `409`.

### `POST /api/tickets/{id}/comments`

Додає коментар до заявки, яка ще не має статусу `Closed`. Поле `text` має містити від 1 до 2000 символів після обрізання пробілів.

**Для локального запуску та Postman.** У середовищі `Development` можна додавати коментарі без авторизації: у Postman виберіть **Authorization → No Auth**, у **Headers** додайте `X-User-Id` зі значенням `1` (початковий користувач) або `2` (працівник підтримки), а в **Body → raw → JSON** передайте `text`. Профілі запуску `http` та `https` уже використовують `Development`. ID має належати наявному користувачу; без заголовка, з некоректним ID або невідомим користувачем API повертає `404`.

Для автентифікованого користувача API бере ID з `NameIdentifier`, і заголовок його не замінює. Поза `Development` заголовок `X-User-Id` ігнорується.

```http
POST /api/tickets/6f9619ff-8b86-d011-b42d-00cf4fc964ff/comments
Content-Type: application/json
X-User-Id: 1

{"text": "Після перезапуску принтер працює."}
```

- `201 Created` — створений коментар з полями `id`, `text`, `authorId` і `createdAt`. `Location` містить адресу списку коментарів цієї заявки.
- `400 Bad Request` — текст порожній або задовгий.
- `404 Not Found` — заявку не знайдено або API не вдалося визначити автора.
- `409 Conflict` — заявку закрито.

### `GET /api/tickets/{id}/comments`

Повертає коментарі заявки від найстарішого до найновішого. Коментарі з однаковим часом створення впорядковуються за `id`.

```http
GET /api/tickets/6f9619ff-8b86-d011-b42d-00cf4fc964ff/comments
```

`200 OK` містить масив коментарів. Якщо заявки або коментарів немає, відповідь містить `[]`.

### `GET /api/status`

Повертає `200 OK` з назвою застосунку, середовищем і поточним часом UTC. Цей маршрут не перевіряє підключення до бази даних.

```json
{
  "name": "Support.Api",
  "environment": "Development",
  "checkedAtUtc": "2026-11-18T10:30:00+00:00"
}
```

### `GET /api/statistics/categories`

Повертає всі наявні категорії за зростанням `categoryId`. Маршрут не приймає фільтрів. `ticketCount` охоплює всі збережені заявки незалежно від статусу та параметрів `GET /api/tickets`; категорії без заявок також включені з лічильником `0`.

Поля відповіді: `categoryId` (ціле число), `categoryName` (рядок) і `ticketCount` (ціле число). У початкових даних є п’ять категорій, але немає заявок; приклад нижче ілюструє відповідь для бази з чотирма заявками:

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

## Як перевірити роботу

1. Створіть заявку через `POST /api/tickets` і збережіть `id` з відповіді.
2. Призначте виконавця `2` через `PATCH /api/tickets/{id}/assignee`.
3. Через `PATCH /api/tickets/{id}/status` послідовно встановіть `1`, `2` і `3`. Переконайтеся, що `resolvedAt` з’являється після статусу `2` і залишається тим самим після статусу `3`.
4. Перезапустіть API та виконайте `GET /api/tickets/{id}` з тим самим `id`. У відповіді мають бути збережений статус `3` і той самий виконавець.

Для запуску автоматичних тестів виконайте в корені репозиторію `dotnet test Support.slnx`. Інтеграційні тести потребують доступу до SQL Server. Для запуску лише тестів, які працюють без SQL Server, використайте команду нижче.

```powershell
dotnet test Support.slnx --filter 'FullyQualifiedName!~SupportDbConnectionTests'
```
