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

Базова адреса API — `http://localhost:5132`. У профілі `http` також доступна [специфікація OpenAPI](http://localhost:5132/openapi/v1.json).

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

**Для локального запуску.** API визначає автора за `NameIdentifier` поточного користувача. У проєкті ще не налаштовано автентифікацію, тому в стандартному профілі цей запит повертає `404`. Коли застосунок отримує `NameIdentifier`, він має містити ID наявного користувача. У тілі запиту достатньо передати `text`.

```http
POST /api/tickets/6f9619ff-8b86-d011-b42d-00cf4fc964ff/comments
Content-Type: application/json

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

## Як перевірити роботу

1. Створіть заявку через `POST /api/tickets` і збережіть `id` з відповіді.
2. Призначте виконавця `2` через `PATCH /api/tickets/{id}/assignee`.
3. Через `PATCH /api/tickets/{id}/status` послідовно встановіть `1`, `2` і `3`. Переконайтеся, що `resolvedAt` з’являється після статусу `2` і залишається тим самим після статусу `3`.
4. Перезапустіть API та виконайте `GET /api/tickets/{id}` з тим самим `id`. У відповіді мають бути збережений статус `3` і той самий виконавець.

Для запуску автоматичних тестів виконайте в корені репозиторію `dotnet test Support.slnx`. Інтеграційні тести потребують доступу до SQL Server. Для запуску лише тестів, які працюють без SQL Server, використайте команду нижче.

```powershell
dotnet test Support.slnx --filter 'FullyQualifiedName!~SupportDbConnectionTests'
```
