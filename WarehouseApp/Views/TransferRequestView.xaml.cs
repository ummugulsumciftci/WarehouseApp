using System.Data;
using System.Windows;
using System.Windows.Controls;
using Oracle.ManagedDataAccess.Client;
using WarehouseApp.Data;

namespace WarehouseApp.Views;

public partial class TransferRequestView : UserControl
{
    public TransferRequestView()
    {
        InitializeComponent();
        Loaded += (_, _) => { LoadBranches(); LoadRequests(); };
    }

    private void LoadBranches()
    {
        try
        {
            DataTable dt = DatabaseHelper.ExecuteQuery(
                "SELECT branch_id, branch_name FROM branches ORDER BY branch_name");

            // Two separate views so the two combo boxes work independently
            cmbSource.ItemsSource = new DataView(dt);
            cmbDest.ItemsSource = new DataView(dt);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not load branches: " + ex.Message);
        }
    }

    // When the source branch changes, list only the products that branch has in stock
    private void CmbSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        cmbProduct.ItemsSource = null;
        txtAvailable.Text = "";
        if (cmbSource.SelectedValue == null) return;

        const string sql = @"
            SELECT p.product_id, p.product_name, s.quantity
            FROM stock s
            JOIN products p ON p.product_id = s.product_id
            WHERE s.branch_id = :branchId AND s.quantity > 0
            ORDER BY p.product_name";
        try
        {
            cmbProduct.ItemsSource = DatabaseHelper.ExecuteQuery(sql,
                new OracleParameter("branchId", Convert.ToInt32(cmbSource.SelectedValue))
            ).DefaultView;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not load products: " + ex.Message);
        }
    }

    private void CmbProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        txtAvailable.Text = cmbProduct.SelectedItem is DataRowView row
            ? $"Available at source: {row["QUANTITY"]}"
            : "";
    }

    private void LoadRequests()
    {
        const string sql = @"
            SELECT r.request_id,
                   sb.branch_name AS source_name,
                   db.branch_name AS dest_name,
                   p.product_name,
                   r.quantity,
                   r.status,
                   r.request_date
            FROM transfer_requests r
            JOIN branches sb ON sb.branch_id = r.source_branch_id
            JOIN branches db ON db.branch_id = r.dest_branch_id
            JOIN products p  ON p.product_id = r.product_id
            ORDER BY r.request_id DESC";
        try
        {
            dgRequests.ItemsSource = DatabaseHelper.ExecuteQuery(sql).DefaultView;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not load requests: " + ex.Message);
        }
    }

    private void BtnCreate_Click(object sender, RoutedEventArgs e)
    {
        // ---- Validation ----
        if (cmbSource.SelectedValue == null || cmbDest.SelectedValue == null)
        {
            MessageBox.Show("Please select both source and destination branches.");
            return;
        }
        int sourceId = Convert.ToInt32(cmbSource.SelectedValue);
        int destId = Convert.ToInt32(cmbDest.SelectedValue);

        if (sourceId == destId)
        {
            MessageBox.Show("Source and destination branches must be different.");
            return;
        }
        if (cmbProduct.SelectedItem is not DataRowView productRow)
        {
            MessageBox.Show("Please select a product.");
            return;
        }
        if (!int.TryParse(txtQuantity.Text.Trim(), out int quantity) || quantity <= 0)
        {
            MessageBox.Show("Quantity must be a positive whole number.");
            return;
        }
        int available = Convert.ToInt32(productRow["QUANTITY"]);
        if (quantity > available)
        {
            MessageBox.Show($"Not enough stock at the source branch. Available: {available}");
            return;
        }

        try
        {
            // 1) Get a new ID from the sequence
            DataTable idTable = DatabaseHelper.ExecuteQuery(
                "SELECT seq_transfer_requests.NEXTVAL AS new_id FROM dual");
            int newId = Convert.ToInt32(idTable.Rows[0]["NEW_ID"]);

            // 2) Insert the request with that ID
            const string sql = @"
                INSERT INTO transfer_requests
                    (request_id, source_branch_id, dest_branch_id, product_id, quantity, status, request_date)
                VALUES
                    (:id, :sourceId, :destId, :productId, :quantity, 'Pending', SYSDATE)";

            DatabaseHelper.ExecuteNonQuery(sql,
                new OracleParameter("id", newId),
                new OracleParameter("sourceId", sourceId),
                new OracleParameter("destId", destId),
                new OracleParameter("productId", Convert.ToInt32(productRow["PRODUCT_ID"])),
                new OracleParameter("quantity", quantity));

            MessageBox.Show($"Transfer request #{newId} created successfully.");
            txtQuantity.Clear();
            LoadRequests();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not create request: " + ex.Message);
        }
    }

    // UPDATE: a pending request can be rejected
    private void BtnReject_Click(object sender, RoutedEventArgs e)
    {
        if (dgRequests.SelectedItem is not DataRowView row)
        {
            MessageBox.Show("Please select a request from the list.");
            return;
        }
        if (row["STATUS"].ToString() != "Pending")
        {
            MessageBox.Show("Only pending requests can be rejected.");
            return;
        }

        int requestId = Convert.ToInt32(row["REQUEST_ID"]);
        var answer = MessageBox.Show($"Reject transfer request #{requestId}?", "Confirm",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            DatabaseHelper.ExecuteNonQuery(
                "UPDATE transfer_requests SET status = 'Rejected' WHERE request_id = :id AND status = 'Pending'",
                new OracleParameter("id", requestId));

            LoadRequests();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not reject request: " + ex.Message);
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadRequests();
}