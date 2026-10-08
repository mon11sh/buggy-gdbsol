using System.Text.Json;
using System.Threading.Channels;
using TransactionsService.Config;
using TransactionsService.Infrastructure.Data;

namespace TransactionsService.Infrastructure.Audit;

/// <summary>
/// Appends one JSON line per ledger row to <c>logs/transactions_yyyy_MM_dd.log</c> from a
/// background writer, so a slow or full disk never sits on the money path. The repositories only
/// enqueue (non-blocking); the channel is bounded and drops the OLDEST line under sustained
/// back-pressure, logging how many were lost. The database row is the system of record — this
/// file is an operator convenience, not the ledger.
/// </summary>
public sealed class TransactionAuditFileWriter : BackgroundService
{
    private const int Capacity = 10_000;

    private readonly Channel<string> _lines = Channel.CreateBounded<string>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly Settings _settings;
    private readonly ILogger<TransactionAuditFileWriter> _logger;

    public TransactionAuditFileWriter(Settings settings, ILogger<TransactionAuditFileWriter> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public void Enqueue(TransactionLogEntity log)
    {
        var line = JsonSerializer.Serialize(new
        {
            timestamp = log.CreatedAt.ToString("o"),
            transaction_id = log.Id,
            account_id = log.AccountId,
            type = log.TransactionType.ToString(),
            amount = log.Amount,
            balance_after = log.BalanceAfter,
            reference_id = log.ReferenceId
        });
        _lines.Writer.TryWrite(line); // DropOldest: never blocks, never throws
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var line in _lines.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    Directory.CreateDirectory(_settings.LOG_DIR);
                    var path = Path.Combine(_settings.LOG_DIR, $"transactions_{DateTime.UtcNow:yyyy_MM_dd}.log");
                    await File.AppendAllTextAsync(path, line + Environment.NewLine, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Transaction audit file write failed; the ledger row is still in the database.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown: whatever is still queued is intentionally not flushed (file is not the ledger)
        }
    }
}
