using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Oracle.ManagedDataAccess.Client;
using WarehouseApp.Data;

namespace WarehouseApp.Views;

public partial class ProductManagementView : UserControl
{
    private int? _selectedId;

    public ProductManagementView()
    {
        InitializeComponent();
        Loaded += (_, _) => { LoadSuppliers(); LoadProducts(); };
    }

    private void LoadSuppliers()
    {
        try
        {
            cmbSupplier.ItemsSource = DatabaseHelper.ExecuteQuery(
                "SELECT supplier_id, supplier_name FROM suppliers ORDER BY supplier_name"
            ).DefaultView;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not load suppliers: " + ex.Message);
        }
    }

    private void LoadProducts()
    {
        const string sql = @"
            SELECT p.product_id, p.product_name, p.category, p.unit_price,
                   p.min_stock_level, p.supplier_id, s.supplier_name
            FROM products p
            LEFT JOIN suppliers s ON s.supplier_id = p.supplier_id
            ORDER BY p.product_id";
        try
        {
            dgProducts.ItemsSource = DatabaseHelper.ExecuteQuery(sql).DefaultView;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not load products: " + ex.Message);
        }
    }

    private void DgProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dgProducts.SelectedItem is DataRowView row)
        {
            _selectedId = Convert.ToInt32(row["PRODUCT_ID"]);
            txtName.Text = row["PRODUCT_NAME"].ToString();
            txtCategory.Text = row["CATEGORY"].ToString();
            txtPrice.Text = row["UNIT_PRICE"].ToString();
            txtMinStock.Text = row["MIN_STOCK_LEVEL"].ToString();

            if (row["SUPPLIER_ID"] == DBNull.Value)
                cmbSupplier.SelectedIndex = -1;
            else
                cmbSupplier.SelectedValue = row["SUPPLIER_ID"];
        }
    }

    // Reads and validates the form. Returns false if something is wrong.
    private bool TryReadForm(out object price, out int minStock, out object supplierId)
    {
        price = DBNull.Value;
        minStock = 10;
        supplierId = cmbSupplier.SelectedValue ?? DBNull.Value;

        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show("Product name is required.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        string priceText = txtPrice.Text.Trim().Replace(',', '.');
        if (priceText.Length > 0)
        {
            if (!decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal p) || p < 0)
            {
                MessageBox.Show("Unit price must be a positive number.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            price = p;
        }

        string minText = txtMinStock.Text.Trim();
        if (minText.Length > 0 && (!int.TryParse(minText, out minStock) || minStock < 0))
        {
            MessageBox.Show("Minimum stock level must be a whole number (0 or more).", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadForm(out object price, out int minStock, out object supplierId)) return;

        const string sql = @"
            INSERT INTO products (product_id, product_name, category, unit_price, min_stock_level, supplier_id)
            VALUES (seq_products.NEXTVAL, :name, :category, :price, :minStock, :supplierId)";
        try
        {
            DatabaseHelper.ExecuteNonQuery(sql,
                new OracleParameter("name", txtName.Text.Trim()),
                new OracleParameter("category", txtCategory.Text.Trim()),
                new OracleParameter("price", price),
                new OracleParameter("minStock", minStock),
                new OracleParameter("supplierId", supplierId));

            MessageBox.Show("Product added successfully.");
            ClearForm();
            LoadProducts();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not add product: " + ex.Message);
        }
    }

    private void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedId == null)
        {
            MessageBox.Show("Please select a product from the list first.");
            return;
        }
        if (!TryReadForm(out object price, out int minStock, out object supplierId)) return;

        const string sql = @"
            UPDATE products
            SET product_name = :name, category = :category, unit_price = :price,
                min_stock_level = :minStock, supplier_id = :supplierId
            WHERE product_id = :id";
        try
        {
            DatabaseHelper.ExecuteNonQuery(sql,
                new OracleParameter("name", txtName.Text.Trim()),
                new OracleParameter("category", txtCategory.Text.Trim()),
                new OracleParameter("price", price),
                new OracleParameter("minStock", minStock),
                new OracleParameter("supplierId", supplierId),
                new OracleParameter("id", _selectedId.Value));

            MessageBox.Show("Product updated successfully.");
            ClearForm();
            LoadProducts();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not update product: " + ex.Message);
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedId == null)
        {
            MessageBox.Show("Please select a product from the list first.");
            return;
        }

        var answer = MessageBox.Show($"Delete product '{txtName.Text}'?", "Confirm Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            DatabaseHelper.ExecuteNonQuery(
                "DELETE FROM products WHERE product_id = :id",
                new OracleParameter("id", _selectedId.Value));

            MessageBox.Show("Product deleted successfully.");
            ClearForm();
            LoadProducts();
        }
        catch (OracleException ex) when (ex.Number == 2292)
        {
            MessageBox.Show("This product cannot be deleted because it has stock or transfer records.",
                "Delete Not Allowed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not delete product: " + ex.Message);
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e) => ClearForm();

    private void ClearForm()
    {
        _selectedId = null;
        dgProducts.SelectedItem = null;
        txtName.Clear();
        txtCategory.Clear();
        txtPrice.Clear();
        txtMinStock.Clear();
        cmbSupplier.SelectedIndex = -1;
    }
}