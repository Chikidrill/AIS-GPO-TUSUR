# AIS GPO Backend — этап 1

Только backend. Стек:

- ASP.NET Core Web API / .NET 10
- Entity Framework Core
- PostgreSQL
- Npgsql
- JWT Bearer

## Реализованный сценарий

`STUDENT -> заявка -> CREATED -> UNDER_REVIEW -> APPROVED/REJECTED -> ProjectMembership -> /me/project`

Постоянные роли: `ADMIN`, `TEACHER`, `STUDENT`. Состояние «участник» определяется активной записью `ProjectMembership`, а не отдельной ролью.

## Запуск PostgreSQL

```bash
docker compose up -d
```

## Запуск API

```bash
dotnet restore
dotnet run
```

API: `http://localhost:5080`
OpenAPI JSON в Development: `http://localhost:5080/openapi/v1.json`

## Dev-аккаунты

Администратор:

```text
admin@gpo.local
Admin123!
```

Студент:

```text
student@gpo.local
Student123!
```

## Endpoint'ы

| Method | Endpoint | Доступ |
|---|---|---|
| POST | `/api/v1/auth/login` | public |
| GET | `/api/v1/me` | authenticated |
| GET | `/api/v1/projects` | authenticated |
| GET | `/api/v1/projects/{id}` | authenticated |
| POST | `/api/v1/projects` | ADMIN |
| GET | `/api/v1/me/profile` | STUDENT |
| PATCH | `/api/v1/me/profile` | STUDENT |
| POST | `/api/v1/projects/{id}/applications` | STUDENT |
| GET | `/api/v1/me/applications` | STUDENT |
| GET | `/api/v1/admin/applications` | ADMIN |
| POST | `/api/v1/admin/applications/{id}/take-for-review` | ADMIN |
| POST | `/api/v1/admin/applications/{id}/approve` | ADMIN |
| POST | `/api/v1/admin/applications/{id}/reject` | ADMIN |
| GET | `/api/v1/me/project` | STUDENT |

## Важные ограничения

- заявка подается только в `OPEN` проект;
- повторная активная заявка в тот же проект запрещена;
- один студент может иметь только один `ACTIVE` проект;
- `APPROVED` и создание `ProjectMembership` выполняются в одной транзакции;
- остальные активные заявки студента после одобрения становятся `CANCELLED`.

## Production

Перед production обязательно:

```text
DevSeed:Enabled = false
Jwt:Secret = случайный секрет длиной не менее 32 байт
```

На первом этапе схема создается через `Database.EnsureCreatedAsync()`. После фиксации модели следующая техническая задача — перейти на обычные EF Core migrations и добавить интеграционные тесты.
