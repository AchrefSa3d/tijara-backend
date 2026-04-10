-- ============================================================
--  TIJARA – Schéma de la base de données PostgreSQL
--  Version : 1.0.0
-- ============================================================

-- Extension uuid (optionnel, utilisation de SERIAL ici)
-- CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ─── Nettoyage (ordre inverse des dépendances) ───────────────
DROP TABLE IF EXISTS reclamations   CASCADE;
DROP TABLE IF EXISTS order_items    CASCADE;
DROP TABLE IF EXISTS orders         CASCADE;
DROP TABLE IF EXISTS products       CASCADE;
DROP TABLE IF EXISTS categories     CASCADE;
DROP TABLE IF EXISTS users          CASCADE;

-- ─── 1. UTILISATEURS ─────────────────────────────────────────
CREATE TABLE users (
  id            SERIAL PRIMARY KEY,
  email         VARCHAR(255) UNIQUE NOT NULL,
  password_hash VARCHAR(255)        NOT NULL,
  role          VARCHAR(20)         NOT NULL DEFAULT 'user'
                  CHECK (role IN ('admin','user','vendor')),
  first_name    VARCHAR(100),
  last_name     VARCHAR(100),
  phone         VARCHAR(30),
  address       TEXT,
  city          VARCHAR(100),
  country       VARCHAR(100)        DEFAULT 'Tunisie',
  is_active     BOOLEAN             NOT NULL DEFAULT TRUE,
  created_at    TIMESTAMP           NOT NULL DEFAULT NOW(),
  updated_at    TIMESTAMP           NOT NULL DEFAULT NOW()
);

-- ─── 2. CATÉGORIES ───────────────────────────────────────────
CREATE TABLE categories (
  id          SERIAL PRIMARY KEY,
  name        VARCHAR(100) UNIQUE NOT NULL,
  slug        VARCHAR(120) UNIQUE NOT NULL,
  description TEXT,
  icon        VARCHAR(80)  DEFAULT 'ri-tag-line',
  color       VARCHAR(30)  DEFAULT 'primary',
  is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
  created_at  TIMESTAMP    NOT NULL DEFAULT NOW()
);

-- ─── 3. PRODUITS ─────────────────────────────────────────────
CREATE TABLE products (
  id            SERIAL PRIMARY KEY,
  vendor_id     INTEGER      NOT NULL REFERENCES users(id)       ON DELETE CASCADE,
  category_id   INTEGER      REFERENCES categories(id)           ON DELETE SET NULL,
  name          VARCHAR(255) NOT NULL,
  slug          VARCHAR(280) UNIQUE NOT NULL,
  description   TEXT,
  price         NUMERIC(10,3) NOT NULL CHECK (price >= 0),
  stock         INTEGER       NOT NULL DEFAULT 0 CHECK (stock >= 0),
  image_url     TEXT,
  status        VARCHAR(20)   NOT NULL DEFAULT 'active'
                  CHECK (status IN ('active','inactive','pending')),
  created_at    TIMESTAMP     NOT NULL DEFAULT NOW(),
  updated_at    TIMESTAMP     NOT NULL DEFAULT NOW()
);

-- ─── 4. COMMANDES ────────────────────────────────────────────
CREATE TABLE orders (
  id              SERIAL PRIMARY KEY,
  user_id         INTEGER       NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  total_amount    NUMERIC(10,3) NOT NULL DEFAULT 0,
  status          VARCHAR(30)   NOT NULL DEFAULT 'pending'
                    CHECK (status IN ('pending','confirmed','shipped','delivered','cancelled')),
  shipping_address TEXT,
  notes           TEXT,
  created_at      TIMESTAMP     NOT NULL DEFAULT NOW(),
  updated_at      TIMESTAMP     NOT NULL DEFAULT NOW()
);

-- ─── 5. LIGNES DE COMMANDE ───────────────────────────────────
CREATE TABLE order_items (
  id          SERIAL PRIMARY KEY,
  order_id    INTEGER       NOT NULL REFERENCES orders(id)   ON DELETE CASCADE,
  product_id  INTEGER       NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
  quantity    INTEGER       NOT NULL DEFAULT 1 CHECK (quantity > 0),
  unit_price  NUMERIC(10,3) NOT NULL CHECK (unit_price >= 0),
  subtotal    NUMERIC(10,3) GENERATED ALWAYS AS (quantity * unit_price) STORED
);

-- ─── 6. RÉCLAMATIONS ─────────────────────────────────────────
CREATE TABLE reclamations (
  id          SERIAL PRIMARY KEY,
  user_id     INTEGER      NOT NULL REFERENCES users(id)   ON DELETE CASCADE,
  order_id    INTEGER      REFERENCES orders(id)           ON DELETE SET NULL,
  subject     VARCHAR(255) NOT NULL,
  message     TEXT         NOT NULL,
  status      VARCHAR(30)  NOT NULL DEFAULT 'open'
                CHECK (status IN ('open','in_progress','resolved','closed')),
  admin_reply TEXT,
  created_at  TIMESTAMP    NOT NULL DEFAULT NOW(),
  updated_at  TIMESTAMP    NOT NULL DEFAULT NOW()
);

-- ─── INDEX ───────────────────────────────────────────────────
CREATE INDEX idx_products_vendor   ON products(vendor_id);
CREATE INDEX idx_products_category ON products(category_id);
CREATE INDEX idx_orders_user       ON orders(user_id);
CREATE INDEX idx_order_items_order ON order_items(order_id);
CREATE INDEX idx_reclamations_user ON reclamations(user_id);

-- ─── FONCTION updated_at automatique ─────────────────────────
CREATE OR REPLACE FUNCTION update_updated_at()
RETURNS TRIGGER AS $$
BEGIN
  NEW.updated_at = NOW();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_users_updated_at
  BEFORE UPDATE ON users
  FOR EACH ROW EXECUTE FUNCTION update_updated_at();

CREATE TRIGGER trg_products_updated_at
  BEFORE UPDATE ON products
  FOR EACH ROW EXECUTE FUNCTION update_updated_at();

CREATE TRIGGER trg_orders_updated_at
  BEFORE UPDATE ON orders
  FOR EACH ROW EXECUTE FUNCTION update_updated_at();

CREATE TRIGGER trg_reclamations_updated_at
  BEFORE UPDATE ON reclamations
  FOR EACH ROW EXECUTE FUNCTION update_updated_at();
