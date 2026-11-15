using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Support.Api;

public static class TicketOpenApiTransformer
{
    private const string CreateRequestExample = """
        {
          "title": "Не працює принтер",
          "description": "Принтер не друкує документи після заміни картриджа.",
          "categoryId": 1,
          "priority": 2
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
          "resolvedAt": null
        }
        """;

    private const string ValidationErrorExample = """
        {
          "CategoryId": [
            "Вказано невідому категорію."
          ]
        }
        """;

    public static Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var path = context.Description.RelativePath?.TrimStart('/');

        if (path == "api/tickets" && context.Description.HttpMethod == "POST")
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

            SetContentExample(operation, "201", JsonNode.Parse(TicketResponseExample)!);
            SetContentExample(operation, "400", JsonNode.Parse(ValidationErrorExample)!);
        }
        else if (path == "api/tickets/{id}" && context.Description.HttpMethod == "GET")
        {
            operation.Summary = "Отримати заявку за ID";
            operation.Description = "Повертає збережену заявку для вказаного UUID. Для невідомого ID повертається `404 Not Found` без тіла відповіді.";
            SetContentExample(operation, "200", JsonNode.Parse(TicketResponseExample)!);
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
}