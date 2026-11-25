using Oracle.ManagedDataAccess.Client;

namespace WarehouseApp.Data;

// Executes a transfer request as ONE atomic database transaction.
public static class TransferService
{
    /// <returns>The ID of the created shipment.</returns>
    public static int ExecuteTransfer(int requestId, bool simulateFailure = false)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using OracleTransaction tx = conn.BeginTransaction();

        // Helper: every command runs on the same connection and transaction
        OracleCommand Cmd(string sql)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.BindByName = true;
            cmd.Transaction = tx;
            return cmd;
        }

        try
        {
            // 1) Read and LOCK the request (FOR UPDATE prevents double execution)
            int sourceId, destId, productId, quantity;
            using (var cmd = Cmd(@"
                SELECT source_branch_id, dest_branch_id, product_id, quantity
                FROM transfer_requests
                WHERE request_id = :id AND status = 'Pending'
                FOR UPDATE"))
            {
                cmd.Parameters.Add(new OracleParameter("id", requestId));
                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    throw new InvalidOperationException("Request not found or it is no longer pending.");

                sourceId = Convert.ToInt32(reader.GetValue(0));
                destId = Convert.ToInt32(reader.GetValue(1));
                productId = Convert.ToInt32(reader.GetValue(2));
                quantity = Convert.ToInt32(reader.GetValue(3));
            }

            // 2) UPDATE #1: decrease source stock (only if there is enough)
            using (var cmd = Cmd(@"
                UPDATE stock SET quantity = quantity - :qty
                WHERE branch_id = :src AND product_id = :pid AND quantity >= :qty"))
            {
                cmd.Parameters.Add(new OracleParameter("qty", quantity));
                cmd.Parameters.Add(new OracleParameter("src", sourceId));
                cmd.Parameters.Add(new OracleParameter("pid", productId));
                if (cmd.ExecuteNonQuery() == 0)
                    throw new InvalidOperationException("Insufficient stock at the source branch.");
            }

            // Demo: fail AFTER the source was decreased, to show that ROLLBACK undoes it
            if (simulateFailure)
                throw new InvalidOperationException("Simulated error after decreasing the source stock.");

            // 3) UPDATE #2: increase destination stock (INSERT if the row does not exist yet)
            using (var cmd = Cmd(@"
                UPDATE stock SET quantity = quantity + :qty
                WHERE branch_id = :dest AND product_id = :pid"))
            {
                cmd.Parameters.Add(new OracleParameter("qty", quantity));
                cmd.Parameters.Add(new OracleParameter("dest", destId));
                cmd.Parameters.Add(new OracleParameter("pid", productId));

                if (cmd.ExecuteNonQuery() == 0)
                {
                    using var insert = Cmd(@"
                        INSERT INTO stock (branch_id, product_id, quantity)
                        VALUES (:dest, :pid, :qty)");
                    insert.Parameters.Add(new OracleParameter("dest", destId));
                    insert.Parameters.Add(new OracleParameter("pid", productId));
                    insert.Parameters.Add(new OracleParameter("qty", quantity));
                    insert.ExecuteNonQuery();
                }
            }

            // 4) INSERT the shipment record (ID from sequence)
            int shipmentId;
            using (var cmd = Cmd("SELECT seq_shipments.NEXTVAL FROM dual"))
            {
                shipmentId = Convert.ToInt32(cmd.ExecuteScalar());
            }
            using (var cmd = Cmd(@"
                INSERT INTO shipments (shipment_id, request_id, status, shipped_date)
                VALUES (:sid, :rid, 'Prepared', SYSDATE)"))
            {
                cmd.Parameters.Add(new OracleParameter("sid", shipmentId));
                cmd.Parameters.Add(new OracleParameter("rid", requestId));
                cmd.ExecuteNonQuery();
            }

            // 5) Mark the request as completed
            using (var cmd = Cmd("UPDATE transfer_requests SET status = 'Completed' WHERE request_id = :id"))
            {
                cmd.Parameters.Add(new OracleParameter("id", requestId));
                cmd.ExecuteNonQuery();
            }

            // 6) Everything succeeded -> make it permanent
            tx.Commit();
            return shipmentId;
        }
        catch
        {
            // Any error -> undo ALL changes made in this transaction
            tx.Rollback();
            throw;
        }
    }
}