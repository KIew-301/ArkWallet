# Удаление зависших процессов тестовых харнессов миграций и скретч-папки.
# Запускать в PowerShell от имени администратора.

taskkill /F /IM seed.exe 2>$null
taskkill /F /IM pgtest.exe 2>$null

if (Test-Path -LiteralPath "$env:TEMP\opencode\migtest") {
    Remove-Item -Recurse -Force -LiteralPath "$env:TEMP\opencode\migtest"
    Write-Host "Migtest scratch folder removed."
}
else {
    Write-Host "Migtest scratch folder not found."
}
