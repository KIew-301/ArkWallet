using ArkWallet.Application.Common;
using ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Contracts.Orchestrators;

/// <summary>
/// Оркестратор для управления маркет-мейкер ботами
/// </summary>
public interface IMarketMakerOrchestrator
{
    /// <summary>
    /// Проверяет и регистрирует ботов 101 и 102, если их нет
    /// </summary>
    /// <returns>Результат операции</returns>
    /// <remarks>
    /// <para>
    /// Регистрирует двух ботов:
    /// - 101: Покупатель (Buyer) с мощностью 20
    /// - 102: Продавец (Seller) с мощностью 20
    /// </para>
    /// <para>
    /// Если бот уже существует, регистрация пропускается.
    /// </para>
    /// </remarks>
    Task<Result> EnsureBotsRegisteredAsync();

    /// <summary>
    /// Проверяет балансы ботов 101 и 102 и выдаёт деньги и токены, если их недостаточно
    /// </summary>
    /// <returns>Результат операции</returns>
    /// <remarks>
    /// <para>
    /// Проверяет и восполняет до целевых значений для каждого бота:
    /// - Баланс: 1 000 000 000
    /// - Количество токенов: 100 000 000
    /// </para>
    /// <para>
    /// Если трейдер не найден, операция пропускается с предупреждением.
    /// </para>
    /// </remarks>
    Task<Result> EnsureTraderBalancesAsync();

    /// <summary>
    /// Обновляет сетку ордеров для всех активных ботов
    /// </summary>
    /// <returns>Результат операции</returns>
    /// <remarks>
    /// Отменяет все активные ордера ботов и создаёт новые по сетке цен.
    /// Работает только для ботов с символом "ZZZ".
    /// </remarks>
    Task<Result> UpdateAllBotsGridAsync();

    /// <summary>
    /// Обрабатывает всех активных ботов (обновление мощности, сетки и рыночные ордера)
    /// </summary>
    /// <returns>Результат операции</returns>
    /// <remarks>
    /// <para>
    /// Для каждого бота выполняет:
    /// - Обновление мощности (если пришло время)
    /// - Обновление сетки ордеров (если пришло время)
    /// - Исполнение рыночного ордера
    /// </para>
    /// <para>
    /// Работает только для ботов с символом "ZZZ".
    /// </para>
    /// </remarks>
    Task<Result> ProcessBotsAsync();
}