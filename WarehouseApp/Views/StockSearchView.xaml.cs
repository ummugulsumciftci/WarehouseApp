using System.Data;
using System.Windows;
using System.Windows.Controls;
using Oracle.ManagedDataAccess.Client;
using WarehouseApp.Data;

namespace WarehouseApp.Views;

public partial class StockSearchView : UserControl
{
    public StockSearchView()
    {
        InitializeComponent();
        Loaded += (_, _) => { LoadBranches(); Search(); };
    }

    private void LoadBranches()
    {
        try
        {
            DataTable dt = DatabaseHelper.ExecuteQuery(
                "SELECT branch_id, branch_name FROM branches ORDER BY branch_name");

            // Add an "All Branches" option at the top (id = 0)
            DataRow all = dt.NewRow();
            all["BRANCH_ID"] = 0;
            all["BRANCH_NAME"] = "All Branches";
            dt.Rows.InsertAt(all, 0);

            cmbBranch.ItemsSource = dt.DefaultView;
            cmbBranch.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not load branches: " + ex.Message);
        }
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e) => Search();

    private void Search()
    {
        const string sql = @"
            SELECT b.branch_name, p.product_name, p.category, sp.supplier_name,
                   s.quantity, p.min_stock_level
            FROM stock s
            JOIN products  p  ON p.product_id  = s.product_id
            JOIN branches  b  ON b.branch_id   = s.branch_id
            LEFT JOIN suppliers sp ON sp.supplier_id = p.supplier_id
            WHERE UPPER(p.product_name) LIKE UPPER(:name)
              AND UPPER(NVL(p.category, ' ')) LIKE UPPER(:category)
              AND (:branchId = 0 OR s.branch_id = :branchId)
            ORDER BY b.branch_name, p.product_name";

        int branchId = cmbBranch.SelectedValue == null ? 0 : Convert.ToInt32(cmbBranch.SelectedValue);

        try
        {
            dgStock.ItemsSource = DatabaseHelper.ExecuteQuery(sql,
                new OracleParameter("name", "%" + txtProductName.Text.Trim() + "%"),
                new OracleParameter("category", "%" + txtCategory.Text.Trim() + "%"),
                new OracleParameter("branchId", branchId)
            ).DefaultView;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Search error: " + ex.Message);
        }
    }
}