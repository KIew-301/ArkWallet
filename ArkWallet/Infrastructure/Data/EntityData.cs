namespace ArkWallet.Infrastructure.Data;

/// <summary>
/// База для данных-сущностей EF Core. Разрешены только операции создания (Create),
/// обновления (Update) и копирования (Copy). Вся бизнес-логика живёт в доменных
/// агрегатах, а не в сущностях Data (правило 18).
/// </summary>
public abstract class EntityData
{
}