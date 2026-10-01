# АркВаллет — рабочий контекст

## Получение отчёта SonarQube (SonarCloud) — ИНСТРУКЦИЯ

Проект публичный, API доступен БЕЗ токена через webfetch. Основной источник — **issues текущего PR**, не ветки.

### 1. Получить свежий список issues PR
```
https://sonarcloud.io/api/issues/search?componentKeys=KIew-301_ArkWallet&pullRequest=<NUM>&ps=100&p=<PAGE>
```
- Component: `KIew-301_ArkWallet` (см. `project_branches/list` / `components/search`).
- `<NUM>` — номер PR, например `47`.
- `ps=100` предельный размер страницы: `ps=500` таймаутит. Пейджинг: `p=1`, `p=2`, ... пока не соберёшь `total`.
- Ответ — большой JSON. webfetch кладёт обрезанный вывод в `C:\Users\nikit\.local\share\opencode\tool-output\tool_*.txt`.

### 2. Слить страницы и выровнять отчёт
Парсер написан заранее (inline python в PowerShell падает на экранировании f-строк — всегда через файл-скрипт):
```
C:\Users\nikit\AppData\Local\Temp\opencode\merge_sonar.py
```
Скрипт читает оба файла страниц, достаёт `rule/severity/component/line/message`, сортирует уникальные строки с `\t`-разделителями и пишет `C:\Users\nikit\AppData\Local\Temp\opencode\sonar_pr<NUM>.txt`.

### 3. Что анализировать
- `componentKeys=KIew-301_ArkWallet` (без `pullRequest=`) — это анализ **main** (устаревший, от 21.07.2026, коммит ed9e830, ~209 smells). НЕ используй для текущей ветки.
- `...&pullRequest=<NUM>` — issues именно PR.
- ВАЖНО: отчёт SonarCloud может отставать от рабочего дерева (fix-коммиты не перегоняли анализ). Каждый issue сверяй с текущим кодом (build-варнинги + чтение файла) перед правкой — часть пунктов уже исправлена в ветке.

### Связанное
- PR: `gh pr list --repo KIew-301/ArkWallet --state open`
- `qualitygates/project_status?project=...&pullRequest=` → 400; `pull_requests/list` → 404. Рабочий эндпоинт — только `issues/search?pullRequest=`.