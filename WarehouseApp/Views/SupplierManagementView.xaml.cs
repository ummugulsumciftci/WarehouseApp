using System.Data;
using System.Windows;
using System.Windows.Controls;
using Oracle.ManagedDataAccess.Client;
using WarehouseApp.Data;

namespace WarehouseApp.Views;

public partial class SupplierManagementView : UserControl
{
    private int? _selectedId;

    public SupplierManagementView()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadSuppliers();
    }

    private void LoadSuppliers()
    {
        try
        {
            dgSuppliers.ItemsSource = DatabaseHelper.ExecuteQuery(
                "SELECT supplier_id, supplier_name, phone, email FROM suppliers ORDER BY supplier_id"
            ).DefaultView;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not load suppliers: " + ex.Message);
        }
    }

    private void DgSuppliers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dgSuppliers.SelectedItem is DataRowView row)
        {
            _selectedId = Convert.ToInt32(row["SUPPLIER_ID"]);
            txtName.Text = row["SUPPLIER_NAME"].ToString();
            txtPhone.Text = row["PHONE"].ToString();
            txtEmail.Text = row["EMAIL"].ToString();
        }
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show("Supplier name is required.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        string email = txtEmail.Text.Trim();
        if (email.Length > 0 && !email.Contains('@'))
        {
            MessageBox.Show("Please enter a valid email address.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInput()) return;

        const string sql = @"
            INSERT INTO suppliers (supplier_id, supplier_name, phone, email)
            VALUES (seq_suppliers.NEXTVAL, :name, :phone, :email)";
        try
        {
            DatabaseHelper.ExecuteNonQuery(sql,
                new OracleParameter("name", txtName.Text.Trim()),
                new OracleParameter("phone", txtPhone.Text.Trim()),
                new OracleParameter("email", txtEmail.Text.Trim()));

            MessageBox.Show("Supplier added successfully.");
            ClearForm();
            LoadSuppliers();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not add supplier: " + ex.Message);
        }
    }

    private void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedId == null)
        {
            MessageBox.Show("Please select a supplier from the list first.");
            return;
        }
        if (!ValidateInput()) return;

        const string sql = @"
            UPDATE suppliers
            SET supplier_name = :name, phone = :phone, email = :email
            WHERE supplier_id = :id";
        try
        {
            DatabaseHelper.ExecuteNonQuery(sql,
                new OracleParameter("name", txtName.Text.Trim()),
                new OracleParameter("phone", txtPhone.Text.Trim()),
                new OracleParameter("email", txtEmail.Text.Trim()),
                new OracleParameter("id", _selectedId.Value));

            MessageBox.Show("Supplier updated successfully.");
            ClearForm();
            LoadSuppliers();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not update supplier: " + ex.Message);
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedId == null)
        {
            MessageBox.Show("Please select a supplier from the list first.");
            return;
        }

        var answer = MessageBox.Show($"Delete supplier '{txtName.Text}'?", "Confirm Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            DatabaseHelper.ExecuteNonQuery(
                "DELETE FROM suppliers WHERE supplier_id = :id",
                new OracleParameter("id", _selectedId.Value));

            MessageBox.Show("Supplier deleted successfully.");
            ClearForm();
            LoadSuppliers();
        }
        catch (OracleException ex) when (ex.Number == 2292)
        {
            MessageBox.Show("This supplier cannot be deleted because some products are linked to it.",
                "Delete Not Allowed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not delete supplier: " + ex.Message);
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e) => ClearForm();

    private void ClearForm()
    {
        _selectedId = null;
        dgSuppliers.SelectedItem = null;
        txtName.Clear();
        txtPhone.Clear();
        txtEmail.Clear();
    }
}