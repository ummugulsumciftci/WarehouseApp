using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace WarehouseApp.Data;

public static class DatabaseHelper
{
    private const string ConnectionString =
        "User Id=warehouse;Password=warehouse123;Data Source=localhost:1521/FREEPDB1;";

    public static OracleConnection GetConnection() => new OracleConnection(ConnectionString);

    // SELECT sorguları için: sonucu DataTable olarak döner
    public static DataTable ExecuteQuery(string sql, params OracleParameter[] parameters)
    {
        using var conn = GetConnection();
        using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.AddRange(parameters);

        using var adapter = new OracleDataAdapter(cmd);
        var table = new DataTable();
        adapter.Fill(table);
        return table;
    }

    // INSERT / UPDATE / DELETE için: etkilenen satır sayısını döner
    public static int ExecuteNonQuery(string sql, params OracleParameter[] parameters)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.AddRange(parameters);
        return cmd.ExecuteNonQuery();
    }
}