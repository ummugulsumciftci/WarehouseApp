using System.Data;
using System.Windows;
using System.Windows.Controls;
using Oracle.ManagedDataAccess.Client;
using WarehouseApp.Data;

namespace WarehouseApp.Views;

public partial class ShipmentTrackingView : UserControl
{
    public ShipmentTrackingView()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadShipments();
    }

    private string SelectedStatusFilter =>
        (cmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";

    // DQL: list shipments with JOINs, optionally filtered by status
    private void LoadShipments()
    {
        const string sql = @"
            SELECT sh.shipment_id, sh.request_id,
                   sb.branch_name AS source_name,
                   db.branch_name AS dest_name,
                   p.product_name, r.quantity,
                   sh.status, sh.shipped_date, sh.delivered_date
            FROM shipments sh
            JOIN transfer_requests r ON r.request_id = sh.request_id
            JOIN branches sb ON sb.branch_id = r.source_branch_id
            JOIN branches db ON db.branch_id = r.dest_branch_id
            JOIN products p  ON p.product_id = r.product_id
            WHERE (:status = 'All' OR sh.status = :status)
            ORDER BY sh.shipment_id DESC";
        try
        {
            dgShipments.ItemsSource = DatabaseHelper.ExecuteQuery(sql,
                new OracleParameter("status", SelectedStatusFilter)).DefaultView;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not load shipments: " + ex.Message);
        }
    }

    private void CmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return; // ignore the event fired while the screen is being created
        LoadShipments();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadShipments();

    private DataRowView? GetSelectedRow()
    {
        if (dgShipments.SelectedItem is DataRowView row) return row;
        MessageBox.Show("Please select a shipment from the list.");
        return null;
    }

    // UPDATE: move a shipment to the next status
    private void ChangeStatus(string fromStatus, string toStatus)
    {
        var row = GetSelectedRow();
        if (row == null) return;

        string current = row["STATUS"].ToString()!;
        if (current != fromStatus)
        {
            MessageBox.Show($"Only '{fromStatus}' shipments can be marked as '{toStatus}'.\nCurrent status: {current}");
            return;
        }

        const string sql = @"
            UPDATE shipments
            SET status = :newStatus,
                delivered_date = CASE WHEN :newStatus = 'Delivered' THEN SYSDATE ELSE delivered_date END
            WHERE shipment_id = :id AND status = :oldStatus";
        try
        {
            int rows = DatabaseHelper.ExecuteNonQuery(sql,
                new OracleParameter("newStatus", toStatus),
                new OracleParameter("id", Convert.ToInt32(row["SHIPMENT_ID"])),
                new OracleParameter("oldStatus", fromStatus));

            if (rows == 0)
                MessageBox.Show("The shipment status has already changed. Please refresh the list.");

            LoadShipments();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not update status: " + ex.Message);
        }
    }

    private void BtnInTransit_Click(object sender, RoutedEventArgs e) => ChangeStatus("Prepared", "In Transit");
    private void BtnDelivered_Click(object sender, RoutedEventArgs e) => ChangeStatus("In Transit", "Delivered");

    // Cancel = status update + stock returned to source (transaction)
    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        var row = GetSelectedRow();
        if (row == null) return;

        string current = row["STATUS"].ToString()!;
        if (current != "Prepared" && current != "In Transit")
        {
            MessageBox.Show($"A '{current}' shipment cannot be cancelled.");
            return;
        }

        int shipmentId = Convert.ToInt32(row["SHIPMENT_ID"]);
        var answer = MessageBox.Show(
            $"Cancel shipment #{shipmentId}?\n\n{row["QUANTITY"]} x {row["PRODUCT_NAME"]} will be returned to {row["SOURCE_NAME"]}.",
            "Confirm Cancel", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            ShipmentService.CancelShipment(shipmentId);
            MessageBox.Show("Shipment cancelled and stock returned to the source branch (COMMIT).",
                "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Cancel failed. All changes were rolled back (ROLLBACK).\n\nReason: " + ex.Message,
                "Cancel Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        LoadShipments();
    }

    // DELETE: remove all cancelled shipment records
    private void BtnDeleteCancelled_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show("Permanently delete ALL cancelled shipment records?",
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            int deleted = DatabaseHelper.ExecuteNonQuery(
                "DELETE FROM shipments WHERE status = 'Cancelled'");

            MessageBox.Show(deleted == 0
                ? "There are no cancelled records to delete."
                : $"{deleted} cancelled record(s) deleted.");
            LoadShipments();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not delete records: " + ex.Message);
        }
    }
}