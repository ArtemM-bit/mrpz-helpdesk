using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Support.Api;

public static class TicketOpenApiTransformer
{
    private const string TicketSearchResponseExample = """
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
        """;

    private const string CategoryStatisticsResponseExample = """
        [
          { "categoryId": 1, "categoryName": "обладнання", "ticketCount": 2 },
          { "categoryId": 2, "categoryName": "програмне забезпечення", "ticketCount": 1 },
          { "categoryId": 3, "categoryName": "мережа", "ticketCount": 1 },
          { "categoryId": 4, "categoryName": "доступ до облікового запису", "ticketCount": 0 },
          { "categoryId": 5, "categoryName": "інше", "ticketCount": 0 }
        ]
        """;

    private const string CreateRequestExample = """
        {
          "title": "Не працює принтер",
          "description": "Принтер не друкує документи після заміни картриджа.",
          "categoryId": 1,
          "priority": 2
        }
        """;

    private const string AssignedTicketResponseExample = """
        {
          "id": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
          "title": "Не працює принтер",
          "description": "Принтер не друкує документи після заміни картриджа.",
          "categoryId": 1,
          "priority": 2,
          "status": 0,
          "createdAt": "2026-01-15T10:30:00+00:00",
          "assigneeId": 2,
          "resolvedAt": null,
          "dueAt": null,
          "isOverdue": false
        }
        """;

    private const string InProgressTicketResponseExample = """
        {
          "id": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
          "title": "Не працює принтер",
          "description": "Принтер не друкує документи після заміни картриджа.",
          "categoryId": 1,
          "priority": 2,
          "status": 1,
          "createdAt": "2026-01-15T10:30:00+00:00",
          "assigneeId": 2,
          "resolvedAt": null,
          "dueAt": null,
          "isOverdue": false
        }
        """;

    private const string TicketResponseExample = """
        {
          "id": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
          "title": "Не працює принтер",
          "description": "Принтер не друкує документи після заміни картриджа.",
          "categoryId": 1,
          "priority": 2,
          "status": 0,
          "createdAt": "2026-01-15T10:30:00+00:00",
          "assigneeId": null,
          "resolvedAt": null,
          "dueAt": null,
          "isOverdue": false
        }
        """;

    private const string TicketWithDeadlineResponseExample = """
        {
          "id": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
          "title": "Не працює принтер",
          "description": "Принтер не друкує документи після заміни картриджа.",
          "categoryId": 1,
          "priority": 2,
          "status": 0,
          "createdAt": "2026-01-15T10:30:00+00:00",
          "assigneeId": null,
          "resolvedAt": null,
          "dueAt": "2026-12-01T08:00:00+00:00",
          "isOverdue": false
        }
        """;

    private const string ValidationErrorExample = """
        {
          "CategoryId": [
            "Вказано невідому категорію."
          ]
        }
        """;

    private const string DueAtValidationErrorExample = """
        {
          "DueAt": [
            "Термін виконання має бути пізніше за поточний час."
          ]
        }
        """;

    private const string AssignRequestExample = """
        {
          "assigneeId": 2
        }
        """;

    private const string UpdateDeadlineRequestExample = """
        {
          "dueAt": "2026-12-01T10:00:00+02:00"
        }
        """;

    private const string ChangeStatusRequestExample = """
        {
          "status": 1
        }
        """;

    private const string CreateCommentRequestExample = """
        {
          "text": "Після перезапуску принтер працює."
        }
        """;

    private const string CommentResponseExample = """
        {
          "id": "7f9619ff-8b86-d011-b42d-00cf4fc964ff",
          "text": "Після перезапуску принтер працює.",
          "authorId": 1,
          "createdAt": "2026-01-15T10:45:00+00:00"
        }
        """;

    private const string CommentsResponseExample = """
        [
          {
            "id": "7f9619ff-8b86-d011-b42d-00cf4fc964ff",
            "text": "Після перезапуску принтер працює.",
            "authorId": 1,
            "createdAt": "2026-01-15T10:45:00+00:00"
          }
        ]
        """;

    public static Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var path = context.Description.RelativePath?.TrimStart('/');

        if (path == "api/tickets" && context.Description.HttpMethod == "GET")
        {
            operation.Summary = "Пошук заявок";
            operation.Description = """
                Повертає масив `TicketResponse`; якщо збігів немає, повертається `[]`. Усі параметри необов'язкові. Різні фільтри (`search`, `categoryId`, `priority`, `status`, `assigneeId`) комбінуються через AND. `search` шукає без урахування регістру в `Title` АБО `Description` (OR); зовнішні пробіли обрізаються, значення лише з пробілів ігнорується.

                | Параметр | Тип | Допустимі значення | Типове значення |
                | --- | --- | --- | --- |
                | `search` | string | будь-який текст; порожній/пробільний текст не фільтрує | немає фільтра |
                | `categoryId` | integer | додатний ID категорії; початкові категорії мають ID `1`–`5` | немає фільтра |
                | `priority` | integer | `0` = `Low`, `1` = `Medium`, `2` = `High`, `3` = `Critical` | немає фільтра |
                | `status` | integer | `0` = `New`, `1` = `InProgress`, `2` = `Resolved`, `3` = `Closed` | немає фільтра |
                | `assigneeId` | integer | додатний ID відповідального працівника | немає фільтра |
                | `sortDirection` | string | `asc` або `desc` (без урахування регістру; зовнішні пробіли обрізаються) | `desc` |

                Сортування застосовується за `CreatedAt`; за однакового часу заявки впорядковуються за `Id` за зростанням. За `desc` дата сортується за спаданням, за `asc` — за зростанням.

                `400 Bad Request` повертається з описом помилки для `categoryId` або `assigneeId`, що не перевищує 0, невизначеного коду `status` чи `priority`, або `sortDirection`, відмінного від `asc`/`desc`. Невідомий додатний `categoryId` або `assigneeId` сам по собі не є помилкою та дає порожній результат.
                """;

            foreach (var parameter in operation.Parameters)
            {
                var parameterName = parameter.Name?.Split('.').Last().ToLowerInvariant();
                parameter.Description = parameterName switch
                {
                    "status" => "Необов'язковий визначений integer: 0 New, 1 InProgress, 2 Resolved, 3 Closed.",
                    "priority" => "Необов'язковий визначений integer: 0 Low, 1 Medium, 2 High, 3 Critical.",
                    "categoryid" => "Необов'язковий додатний integer ID категорії; початкові ID: 1–5.",
                    "assigneeid" => "Необов'язковий додатний integer ID відповідального працівника.",
                    "search" => "Необов'язковий рядок; пошук без урахування регістру в Title або Description, пробільне значення ігнорується.",
                    "sortdirection" => "Необов'язковий рядок asc або desc; за замовчуванням desc.",
                    _ => parameter.Description
                };
            }

            SetResponseDescription(operation, "200", "Повертається масив заявок за схемою `TicketResponse`; він може бути порожнім.");
            SetResponseDescription(operation, "400", "Параметр невалідний; повертається ValidationProblemDetails із повідомленнями про помилки.");
            SetContentExample(operation, "200", JsonNode.Parse(TicketSearchResponseExample)!);
        }
        else if (path == "api/tickets" && context.Description.HttpMethod == "POST")
        {
            operation.Summary = "Створити заявку";
            operation.Description = """
                Створює заявку. Обов'язкові поля: `title` (рядок, 1–150 символів), `description` (рядок, 1–4000 символів) і `categoryId` (ціле число ID наявної категорії; початкові ID: 1–5). Текстові значення обрізаються по краях.

                `priority` — необов'язкове ціле число: `0` = `Low`, `1` = `Medium`, `2` = `High`, `3` = `Critical`; якщо поле пропущене, використовується `0` (`Low`).

                Система генерує `id` і `createdAt`, а `status` встановлює в `0` (`New`). `assigneeId` і `resolvedAt` для нової заявки дорівнюють `null`. Успішна відповідь повертає заголовок `Location` з URL створеної заявки. Для наведеного прикладу відповіді його значення: `/api/tickets/6f9619ff-8b86-d011-b42d-00cf4fc964ff`.
                """;

            if (operation.RequestBody?.Content.TryGetValue("application/json", out var requestContent) == true)
            {
                requestContent.Example = JsonNode.Parse(CreateRequestExample);
            }

            SetResponseDescription(operation, "201", "Заявку створено; відповідь містить заявку, а заголовок `Location` — її URL.");
            SetResponseDescription(operation, "400", "Помилки валідації у вигляді об'єкта, де ключ — назва поля, а значення — масив повідомлень.");
            SetContentExample(operation, "201", JsonNode.Parse(TicketResponseExample)!);
            SetContentExample(operation, "400", JsonNode.Parse(ValidationErrorExample)!);
        }
        else if (path == "api/statistics/categories" && context.Description.HttpMethod == "GET")
        {
            operation.Summary = "Статистика заявок за категоріями";
            operation.Description = """
                Повертає всі категорії у порядку `CategoryId` за зростанням. Поля: `categoryId` — integer ID категорії; `categoryName` — її назва (string); `ticketCount` — кількість заявок (integer). `ticketCount` охоплює всі збережені заявки незалежно від статусу чи query-фільтрів `GET /api/tickets`; цей маршрут не приймає фільтри. Категорії з нульовою кількістю заявок також включені.
                """;
            SetResponseDescription(operation, "200", "Усі категорії з кількістю заявок за всіма статусами; категорії без заявок мають `ticketCount: 0`.");
            SetContentExample(operation, "200", JsonNode.Parse(CategoryStatisticsResponseExample)!);
        }
        else if (path == "api/tickets/{id}" && context.Description.HttpMethod == "GET")
        {
            operation.Summary = "Отримати заявку за ID";
            operation.Description = "Повертає збережену заявку для вказаного UUID. Для невідомого ID повертається `404 Not Found` без тіла відповіді.";
            SetResponseDescription(operation, "200", "Заявку знайдено; повертається схема `TicketResponse`.");
            SetResponseDescription(operation, "404", "Заявку з таким UUID не знайдено; тіло відповіді відсутнє.");
            SetContentExample(operation, "200", JsonNode.Parse(TicketResponseExample)!);
        }
        else if (path == "api/tickets/{id}/deadline" && context.Description.HttpMethod == "PATCH")
        {
            operation.Summary = "Оновити термін виконання заявки";
            operation.Description = "Приймає обов'язкове поле `dueAt` у форматі ISO 8601 із часовим зміщенням або `Z`. Термін має бути строго пізніше за поточний час UTC; успіх повертає заявку з нормалізованим до UTC `dueAt` і обчисленим `isOverdue`.";
            SetRequestExample(operation, UpdateDeadlineRequestExample);
            SetResponseDescription(operation, "200", "Термін оновлено; повертається заявка з датами в UTC.");
            SetResponseDescription(operation, "400", "`dueAt` відсутній, має некоректний формат або є не пізнішим за поточний час; помилка повертається для поля `DueAt`.");
            SetResponseDescription(operation, "404", "Заявку з таким UUID не знайдено; тіло відповіді відсутнє.");
            SetResponseDescription(operation, "409", "Заявка має статус `Resolved` або `Closed`; термін змінювати заборонено.");
            SetContentExample(operation, "200", JsonNode.Parse(TicketWithDeadlineResponseExample)!);
            SetContentExample(operation, "400", JsonNode.Parse(DueAtValidationErrorExample)!);
        }
        else if (path == "api/tickets/{id}/assignee" && context.Description.HttpMethod == "PATCH")
        {
            operation.Summary = "Призначити працівника на заявку";
            operation.Description = "Призначає заявці працівника підтримки за його `assigneeId`. Тіло запиту відповідає DTO `AssignTicketRequest`; успіх повертає оновлену заявку за схемою `TicketResponse`.";
            SetRequestExample(operation, AssignRequestExample);
            SetResponseDescription(operation, "200", "Працівника призначено; повертається оновлена заявка.");
            SetResponseDescription(operation, "400", "Працівника з таким ID не знайдено або він не є працівником підтримки; тіло відповіді відсутнє.");
            SetResponseDescription(operation, "404", "Заявку з таким UUID не знайдено; тіло відповіді відсутнє.");
            SetResponseDescription(operation, "409", "Заявка вже має статус `Closed` і не може бути змінена; тіло відповіді відсутнє.");
            SetContentExample(operation, "200", JsonNode.Parse(AssignedTicketResponseExample)!);
        }
        else if (path == "api/tickets/{id}/status" && context.Description.HttpMethod == "PATCH")
        {
            operation.Summary = "Змінити статус заявки";
            operation.Description = """
                Приймає DTO `ChangeTicketStatusRequest` з числовим значенням статусу й повертає оновлену заявку за схемою `TicketResponse`.

                Значення статусів: `0` = `New`, `1` = `InProgress`, `2` = `Resolved`, `3` = `Closed`.

                | Поточний статус | Дозволений наступний статус | Умова |
                | --- | --- | --- |
                | `New` | `InProgress` | Заявці має бути призначено працівника підтримки. |
                | `InProgress` | `Resolved` | Без додаткових умов. |
                | `Resolved` | `Closed` | Без додаткових умов. |
                | `Closed` | — | Переходів немає. |

                Інші переходи, повторне встановлення поточного статусу та будь-яка зміна статусу `Closed` повертають `409 Conflict`. Якщо для `New → InProgress` не призначено працівника, також повертається `409 Conflict`. `ResolvedAt` встановлюється серверним поточним часом UTC лише під час переходу в `Resolved`; перехід у `Closed` зберігає цю дату, а клієнт не може передати чи змінити її.
                """;
            SetRequestExample(operation, ChangeStatusRequestExample);
            SetResponseDescription(operation, "200", "Статус змінено; повертається оновлена заявка.");
            SetResponseDescription(operation, "400", "Передано невідоме числове значення статусу; тіло відповіді відсутнє.");
            SetResponseDescription(operation, "404", "Заявку з таким UUID не знайдено; тіло відповіді відсутнє.");
            SetResponseDescription(operation, "409", "Перехід заборонений або для початку роботи заявці не призначено працівника; тіло відповіді відсутнє.");
            SetContentExample(operation, "200", JsonNode.Parse(InProgressTicketResponseExample)!);
        }
        else if (path == "api/tickets/{id}/comments" && context.Description.HttpMethod == "POST")
        {
            operation.Summary = "Додати коментар до заявки";
            operation.Description = "Приймає DTO `CreateCommentRequest` лише з полем `text` довжиною 1–2000 символів після обрізання пробілів по краях; автор не передається клієнтом. API бере ID автора з claim `ClaimTypes.NameIdentifier` поточного користувача запиту й шукає відповідного користувача в системі. Якщо claim відсутній/не є коректним ID або користувача не знайдено, повертається `404 Not Found`. Успішна відповідь `TicketCommentResponse` містить встановлений сервером `authorId` і час створення. Коментарі можна додавати до заявки в будь-якому статусі, крім `Closed`.";
            SetRequestExample(operation, CreateCommentRequestExample);
            SetResponseDescription(operation, "201", "Коментар створено; повертається `TicketCommentResponse`, а `Location` указує на ресурс коментарів заявки.");
            SetResponseDescription(operation, "400", "Текст порожній або довший за 2000 символів після обрізання пробілів; тіло відповіді відсутнє.");
            SetResponseDescription(operation, "404", "Заявку або користувача-автора не знайдено; тіло відповіді відсутнє.");
            SetResponseDescription(operation, "409", "До заявки зі статусом `Closed` не можна додати коментар; тіло відповіді відсутнє.");
            SetContentExample(operation, "201", JsonNode.Parse(CommentResponseExample)!);
        }
        else if (path == "api/tickets/{id}/comments" && context.Description.HttpMethod == "GET")
        {
            operation.Summary = "Отримати коментарі заявки";
            operation.Description = "Повертає масив `TicketCommentResponse`. Сортування: `CreatedAt` за зростанням, а для однакового часу — `Id` за зростанням. Якщо коментарів немає, повертається порожній масив; для неіснуючого ID заявки цей маршрут також повертає порожній масив.";
            SetResponseDescription(operation, "200", "Повертається масив коментарів; він може бути порожнім.");
            SetContentExample(operation, "200", JsonNode.Parse(CommentsResponseExample)!);
        }

        return Task.CompletedTask;
    }

    private static void SetContentExample(OpenApiOperation operation, string statusCode, JsonNode example)
    {
        if (operation.Responses.TryGetValue(statusCode, out var response) &&
            response.Content.TryGetValue("application/json", out var content))
        {
            content.Example = example;
        }
    }

    private static void SetRequestExample(OpenApiOperation operation, string example)
    {
        if (operation.RequestBody?.Content.TryGetValue("application/json", out var content) == true)
        {
            content.Example = JsonNode.Parse(example);
        }
    }

    private static void SetResponseDescription(OpenApiOperation operation, string statusCode, string description)
    {
        if (operation.Responses.TryGetValue(statusCode, out var response))
        {
            response.Description = description;
        }
    }
}