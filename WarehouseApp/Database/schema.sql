-- Multi-Branch Warehouse Inventory System - Database Schema
-- Run as user WAREHOUSE on FREEPDB1

CREATE SEQUENCE seq_branches START WITH 1 INCREMENT BY 1;
CREATE SEQUENCE seq_suppliers START WITH 1 INCREMENT BY 1;
CREATE SEQUENCE seq_products START WITH 1 INCREMENT BY 1;
CREATE SEQUENCE seq_transfer_requests START WITH 1 INCREMENT BY 1;
CREATE SEQUENCE seq_shipments START WITH 1 INCREMENT BY 1;

CREATE TABLE branches (
    branch_id    NUMBER PRIMARY KEY,
    branch_name  VARCHAR2(100) NOT NULL,
    city         VARCHAR2(50),
    address      VARCHAR2(200)
);

CREATE TABLE suppliers (
    supplier_id    NUMBER PRIMARY KEY,
    supplier_name  VARCHAR2(100) NOT NULL,
    phone          VARCHAR2(20),
    email          VARCHAR2(100)
);

CREATE TABLE products (
    product_id       NUMBER PRIMARY KEY,
    product_name     VARCHAR2(100) NOT NULL,
    category         VARCHAR2(50),
    unit_price       NUMBER(10,2),
    min_stock_level  NUMBER DEFAULT 10,
    supplier_id      NUMBER REFERENCES suppliers(supplier_id)
);

CREATE TABLE stock (
    branch_id   NUMBER REFERENCES branches(branch_id),
    product_id  NUMBER REFERENCES products(product_id),
    quantity    NUMBER DEFAULT 0 NOT NULL CHECK (quantity >= 0),
    PRIMARY KEY (branch_id, product_id)
);

CREATE TABLE transfer_requests (
    request_id        NUMBER PRIMARY KEY,
    source_branch_id  NUMBER NOT NULL REFERENCES branches(branch_id),
    dest_branch_id    NUMBER NOT NULL REFERENCES branches(branch_id),
    product_id        NUMBER NOT NULL REFERENCES products(product_id),
    quantity          NUMBER NOT NULL CHECK (quantity > 0),
    status            VARCHAR2(20) DEFAULT 'Pending' CHECK (status IN ('Pending','Completed','Rejected')),
    request_date      DATE DEFAULT SYSDATE,
    CHECK (source_branch_id <> dest_branch_id)
);

CREATE TABLE shipments (
    shipment_id    NUMBER PRIMARY KEY,
    request_id     NUMBER NOT NULL REFERENCES transfer_requests(request_id),
    status         VARCHAR2(20) DEFAULT 'Prepared' CHECK (status IN ('Prepared','In Transit','Delivered','Cancelled')),
    shipped_date   DATE DEFAULT SYSDATE,
    delivered_date DATE
);

-- Sample data
INSERT INTO branches VALUES (seq_branches.NEXTVAL, 'Central Warehouse', 'Ankara', 'Ostim');
INSERT INTO branches VALUES (seq_branches.NEXTVAL, 'Kadikoy Branch', 'Istanbul', 'Kadikoy');
INSERT INTO branches VALUES (seq_branches.NEXTVAL, 'Konak Branch', 'Izmir', 'Konak');
INSERT INTO suppliers VALUES (seq_suppliers.NEXTVAL, 'Anatolia Supply Co.', '03120000000', 'info@anatolia.com');
INSERT INTO products VALUES (seq_products.NEXTVAL, 'Laptop', 'Electronics', 25000, 5, 1);
INSERT INTO products VALUES (seq_products.NEXTVAL, 'Mouse', 'Electronics', 300, 20, 1);
INSERT INTO products VALUES (seq_products.NEXTVAL, 'A4 Paper', 'Stationery', 150, 50, 1);
INSERT INTO stock VALUES (1, 1, 40);
INSERT INTO stock VALUES (1, 2, 200);
INSERT INTO stock VALUES (1, 3, 500);
INSERT INTO stock VALUES (2, 1, 8);
INSERT INTO stock VALUES (2, 2, 15);
INSERT INTO stock VALUES (3, 3, 30);
COMMIT;