using System.Data;
using System.Windows;
using System.Windows.Controls;
using Oracle.ManagedDataAccess.Client;
using WarehouseApp.Data;

namespace WarehouseApp.Views;

public partial class BranchManagementView : UserControl
{
    // ID of the branch selected in the list (null = nothing selected)
    private int? _selectedId;

    public BranchManagementView()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadBranches();
    }

    private void LoadBranches()
    {
        try
        {
            dgBranches.ItemsSource = DatabaseHelper.ExecuteQuery(
                "SELECT branch_id, branch_name, city, address FROM branches ORDER BY branch_id"
            ).DefaultView;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not load branches: " + ex.Message);
        }
    }

    // When a row is clicked, copy its values into the form
    private void DgBranches_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dgBranches.SelectedItem is DataRowView row)
        {
            _selectedId = Convert.ToInt32(row["BRANCH_ID"]);
            txtName.Text = row["BRANCH_NAME"].ToString();
            txtCity.Text = row["CITY"].ToString();
            txtAddress.Text = row["ADDRESS"].ToString();
        }
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show("Branch name is required.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    // INSERT – the ID comes from the sequence
    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInput()) return;

        const string sql = @"
            INSERT INTO branches (branch_id, branch_name, city, address)
            VALUES (seq_branches.NEXTVAL, :name, :city, :address)";
        try
        {
            DatabaseHelper.ExecuteNonQuery(sql,
                new OracleParameter("name", txtName.Text.Trim()),
                new OracleParameter("city", txtCity.Text.Trim()),
                new OracleParameter("address", txtAddress.Text.Trim()));

            MessageBox.Show("Branch added successfully.");
            ClearForm();
            LoadBranches();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not add branch: " + ex.Message);
        }
    }

    // UPDATE
    private void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedId == null)
        {
            MessageBox.Show("Please select a branch from the list first.");
            return;
        }
        if (!ValidateInput()) return;

        const string sql = @"
            UPDATE branches
            SET branch_name = :name, city = :city, address = :address
            WHERE branch_id = :id";
        try
        {
            DatabaseHelper.ExecuteNonQuery(sql,
                new OracleParameter("name", txtName.Text.Trim()),
                new OracleParameter("city", txtCity.Text.Trim()),
                new OracleParameter("address", txtAddress.Text.Trim()),
                new OracleParameter("id", _selectedId.Value));

            MessageBox.Show("Branch updated successfully.");
            ClearForm();
            LoadBranches();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not update branch: " + ex.Message);
        }
    }

    // DELETE
    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedId == null)
        {
            MessageBox.Show("Please select a branch from the list first.");
            return;
        }

        var answer = MessageBox.Show($"Delete branch '{txtName.Text}'?", "Confirm Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            DatabaseHelper.ExecuteNonQuery(
                "DELETE FROM branches WHERE branch_id = :id",
                new OracleParameter("id", _selectedId.Value));

            MessageBox.Show("Branch deleted successfully.");
            ClearForm();
            LoadBranches();
        }
        catch (OracleException ex) when (ex.Number == 2292)
        {
            // ORA-02292: child records exist (foreign key)
            MessageBox.Show("This branch cannot be deleted because it has stock or transfer records.",
                "Delete Not Allowed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not delete branch: " + ex.Message);
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e) => ClearForm();

    private void ClearForm()
    {
        _selectedId = null;
        dgBranches.SelectedItem = null;
        txtName.Clear();
        txtCity.Clear();
        txtAddress.Clear();
    }
}