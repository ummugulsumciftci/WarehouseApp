using Oracle.ManagedDataAccess.Client;

namespace WarehouseApp.Data;

public static class ShipmentService
{
    // Cancels a shipment and moves the stock back to the source branch – in ONE transaction.
    public static void CancelShipment(int shipmentId)
    {
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using OracleTransaction tx = conn.BeginTransaction();

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
            // 1) Read and lock the shipment (only Prepared / In Transit can be cancelled)
            int sourceId, destId, productId, quantity;
            using (var cmd = Cmd(@"
                SELECT r.source_branch_id, r.dest_branch_id, r.product_id, r.quantity
                FROM shipments sh
                JOIN transfer_requests r ON r.request_id = sh.request_id
                WHERE sh.shipment_id = :id AND sh.status IN ('Prepared', 'In Transit')
                FOR UPDATE OF sh.status"))
            {
                cmd.Parameters.Add(new OracleParameter("id", shipmentId));
                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    throw new InvalidOperationException("Shipment not found or it can no longer be cancelled.");

                sourceId = Convert.ToInt32(reader.GetValue(0));
                destId = Convert.ToInt32(reader.GetValue(1));
                productId = Convert.ToInt32(reader.GetValue(2));
                quantity = Convert.ToInt32(reader.GetValue(3));
            }

            // 2) Take the quantity back from the destination
            using (var cmd = Cmd(@"
                UPDATE stock SET quantity = quantity - :qty
                WHERE branch_id = :dest AND product_id = :pid AND quantity >= :qty"))
            {
                cmd.Parameters.Add(new OracleParameter("qty", quantity));
                cmd.Parameters.Add(new OracleParameter("dest", destId));
                cmd.Parameters.Add(new OracleParameter("pid", productId));
                if (cmd.ExecuteNonQuery() == 0)
                    throw new InvalidOperationException(
                        "The destination branch no longer has enough stock to return.");
            }

            // 3) Return it to the source (INSERT if the row does not exist)
            using (var cmd = Cmd(@"
                UPDATE stock SET quantity = quantity + :qty
                WHERE branch_id = :src AND product_id = :pid"))
            {
                cmd.Parameters.Add(new OracleParameter("qty", quantity));
                cmd.Parameters.Add(new OracleParameter("src", sourceId));
                cmd.Parameters.Add(new OracleParameter("pid", productId));

                if (cmd.ExecuteNonQuery() == 0)
                {
                    using var insert = Cmd(@"
                        INSERT INTO stock (branch_id, product_id, quantity)
                        VALUES (:src, :pid, :qty)");
                    insert.Parameters.Add(new OracleParameter("src", sourceId));
                    insert.Parameters.Add(new OracleParameter("pid", productId));
                    insert.Parameters.Add(new OracleParameter("qty", quantity));
                    insert.ExecuteNonQuery();
                }
            }

            // 4) Mark the shipment as cancelled
            using (var cmd = Cmd("UPDATE shipments SET status = 'Cancelled' WHERE shipment_id = :id"))
            {
                cmd.Parameters.Add(new OracleParameter("id", shipmentId));
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}