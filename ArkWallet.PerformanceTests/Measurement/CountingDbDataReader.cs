using System.Collections;
using System.Data;
using System.Data.Common;

namespace ArkWallet.PerformanceTests.Measurement;

internal sealed class CountingDbDataReader(DbDataReader inner, Action onRowRead) : DbDataReader
{
    public override bool Read()
    {
        var read = inner.Read();
        if (read)
            onRowRead();
        return read;
    }

    public override Task<bool> ReadAsync(CancellationToken cancellationToken)
        => CountAsync(inner.ReadAsync(cancellationToken));

    private async Task<bool> CountAsync(Task<bool> readTask)
    {
        var read = await readTask.ConfigureAwait(false);
        if (read)
            onRowRead();
        return read;
    }

    public override int Depth => inner.Depth;
    public override int FieldCount => inner.FieldCount;
    public override bool HasRows => inner.HasRows;
    public override bool IsClosed => inner.IsClosed;
    public override int RecordsAffected => inner.RecordsAffected;

    public override object this[int ordinal] => inner[ordinal];
    public override object this[string name] => inner[name];

    public override void Close() => inner.Close();
    public override Task CloseAsync() => inner.CloseAsync();

    public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);
    public override IEnumerator GetEnumerator() => inner.GetEnumerator();
    public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);
    public override string GetName(int ordinal) => inner.GetName(ordinal);
    public override int GetOrdinal(string name) => inner.GetOrdinal(name);
    public override DataTable? GetSchemaTable() => inner.GetSchemaTable();
    public override object GetValue(int ordinal) => inner.GetValue(ordinal);
    public override int GetValues(object[] values) => inner.GetValues(values);
    public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);
    public override bool NextResult() => inner.NextResult();
    public override Task<bool> NextResultAsync(CancellationToken cancellationToken)
        => inner.NextResultAsync(cancellationToken);

    public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinal);
    public override byte GetByte(int ordinal) => inner.GetByte(ordinal);
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        => inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
    public override char GetChar(int ordinal) => inner.GetChar(ordinal);
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        => inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
    public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);
    public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinal);
    public override double GetDouble(int ordinal) => inner.GetDouble(ordinal);
    public override float GetFloat(int ordinal) => inner.GetFloat(ordinal);
    public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinal);
    public override short GetInt16(int ordinal) => inner.GetInt16(ordinal);
    public override int GetInt32(int ordinal) => inner.GetInt32(ordinal);
    public override long GetInt64(int ordinal) => inner.GetInt64(ordinal);
    public override string GetString(int ordinal) => inner.GetString(ordinal);
    public override Stream GetStream(int ordinal) => inner.GetStream(ordinal);
    public override TextReader GetTextReader(int ordinal) => inner.GetTextReader(ordinal);

    public override T GetFieldValue<T>(int ordinal) => inner.GetFieldValue<T>(ordinal);
    public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken)
        => inner.GetFieldValueAsync<T>(ordinal, cancellationToken);

    public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken)
        => inner.IsDBNullAsync(ordinal, cancellationToken);

    public override Type GetProviderSpecificFieldType(int ordinal) => inner.GetProviderSpecificFieldType(ordinal);
    public override object GetProviderSpecificValue(int ordinal) => inner.GetProviderSpecificValue(ordinal);
    public override int GetProviderSpecificValues(object[] values) => inner.GetProviderSpecificValues(values);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            inner.Dispose();
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return inner.DisposeAsync();
    }
}
