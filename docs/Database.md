# Схема бази даних

Для створення таблиць `Categories` і `Tickets` використовується міграція EF Core `CreateTicketAndCategory`.

Перед застосуванням міграції задайте рядок підключення `ConnectionStrings:SupportDatabase` у конфігурації застосунку або через змінну середовища:

```powershell
$env:ConnectionStrings__SupportDatabase = "Server=(local)\SQLEXPRESS;Database=SupportDb;Integrated Security=True;TrustServerCertificate=True;"
```

З кореня репозиторію виконайте:

```powershell
dotnet ef database update --project Support.Api --startup-project Support.Api
```

Міграція створює таблиці `Categories` і `Tickets`, первинні ключі, індекс і зовнішній ключ `Tickets.CategoryId`. Видалення категорії, яка містить заявки, заборонене обмеженням `Restrict`.
