const { query } = require('../config/database');

// ─── GET /api/admin/vendors/pending ───────────────────────────
async function getPendingVendors(req, res) {
  try {
    const result = await query(
      `SELECT id, email, first_name, last_name, phone, city, created_at
       FROM users WHERE role = 'vendor' AND is_approved = 0 AND is_active = 1
       ORDER BY created_at DESC`
    );
    res.json(result.recordset);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── GET /api/admin/vendors ───────────────────────────────────
async function getAllVendors(req, res) {
  try {
    const result = await query(
      `SELECT id, email, first_name, last_name, phone, city, is_approved, rejection_reason, created_at
       FROM users WHERE role = 'vendor' AND is_active = 1
       ORDER BY created_at DESC`
    );
    res.json(result.recordset);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── PATCH /api/admin/vendors/:id/approve ─────────────────────
async function approveVendor(req, res) {
  try {
    const result = await query(
      `UPDATE users SET is_approved = 1, rejection_reason = NULL
       OUTPUT INSERTED.id, INSERTED.email, INSERTED.first_name, INSERTED.last_name, INSERTED.is_approved
       WHERE id = @id AND role = 'vendor'`,
      { id: parseInt(req.params.id) }
    );
    if (!result.recordset.length)
      return res.status(404).json({ message: 'Vendeur introuvable.' });
    res.json({ message: 'Compte vendeur approuvé.', vendor: result.recordset[0] });
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── PATCH /api/admin/vendors/:id/reject ──────────────────────
async function rejectVendor(req, res) {
  const { reason = 'Demande refusée par l\'administrateur.' } = req.body;
  try {
    const result = await query(
      `UPDATE users SET is_approved = 0, is_active = 0, rejection_reason = @reason
       OUTPUT INSERTED.id, INSERTED.email, INSERTED.first_name, INSERTED.last_name
       WHERE id = @id AND role = 'vendor'`,
      { id: parseInt(req.params.id), reason }
    );
    if (!result.recordset.length)
      return res.status(404).json({ message: 'Vendeur introuvable.' });
    res.json({ message: 'Compte vendeur rejeté.', vendor: result.recordset[0] });
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── GET /api/admin/users ─────────────────────────────────────
async function getAllUsers(req, res) {
  try {
    const result = await query(
      `SELECT id, email, first_name, last_name, phone, city, role, is_approved, is_active, created_at
       FROM users ORDER BY created_at DESC`
    );
    res.json(result.recordset);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── GET /api/admin/stats ─────────────────────────────────────
async function getStats(req, res) {
  try {
    const users          = await query("SELECT COUNT(*) AS total FROM users WHERE role='user' AND is_active=1");
    const vendors        = await query("SELECT COUNT(*) AS total FROM users WHERE role='vendor' AND is_active=1");
    const pending        = await query("SELECT COUNT(*) AS total FROM users WHERE role='vendor' AND is_approved=0 AND is_active=1");
    const products       = await query("SELECT COUNT(*) AS total FROM products WHERE is_active=1");
    const orders         = await query("SELECT COUNT(*) AS total FROM orders");
    const reclamations   = await query("SELECT COUNT(*) AS total FROM reclamations WHERE status='open'");
    const pendingAnn     = await query("SELECT COUNT(*) AS total FROM annonces WHERE status='pending'");
    const pendingProd    = await query("SELECT COUNT(*) AS total FROM products WHERE approval_status='pending'");

    res.json({
      users:            users.recordset[0].total,
      vendors:          vendors.recordset[0].total,
      pendingVendors:   pending.recordset[0].total,
      products:         products.recordset[0].total,
      orders:           orders.recordset[0].total,
      openReclamations: reclamations.recordset[0].total,
      pendingAnnonces:  pendingAnn.recordset[0].total,
      pendingProducts:  pendingProd.recordset[0].total,
    });
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── GET /api/admin/orders ───────────────────────────────────────
async function getAllOrders(req, res) {
  try {
    const result = await query(
      `SELECT o.id, o.user_id, o.total_amount, o.status, o.shipping_address,
              o.notes, o.created_at, o.updated_at,
              CONCAT(u.first_name,' ',u.last_name) AS client_name,
              u.email AS client_email, u.phone AS client_phone
       FROM orders o
       JOIN users u ON o.user_id = u.id
       ORDER BY o.created_at DESC`
    );
    res.json(result.recordset);
  } catch (err) {
    console.error('getAllOrders:', err);
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── PATCH /api/admin/orders/:id/status ──────────────────────────
async function updateOrderStatus(req, res) {
  const { status } = req.body;
  const allowed = ['pending','confirmed','shipped','delivered','cancelled'];
  if (!allowed.includes(status))
    return res.status(400).json({ message: 'Statut invalide.' });
  try {
    const result = await query(
      `UPDATE orders SET status = @status, updated_at = GETDATE()
       OUTPUT INSERTED.id, INSERTED.status
       WHERE id = @id`,
      { id: parseInt(req.params.id), status }
    );
    if (!result.recordset.length) return res.status(404).json({ message: 'Commande introuvable.' });
    res.json({ message: 'Statut mis à jour.', order: result.recordset[0] });
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── GET /api/admin/all-products ─────────────────────────────────
async function getAllProducts(req, res) {
  const { approval_status } = req.query;
  let where = '1=1';
  const params = {};
  if (approval_status) { where = 'p.approval_status = @approval_status'; params.approval_status = approval_status; }
  try {
    const result = await query(
      `SELECT p.id, p.name, p.price, p.stock, p.approval_status, p.is_active,
              p.image_url, p.created_at,
              c.name AS category_name,
              CONCAT(u.first_name,' ',u.last_name) AS vendor_name,
              u.shop_name, u.email AS vendor_email
       FROM products p
       LEFT JOIN categories c ON p.category_id = c.id
       LEFT JOIN users u ON p.vendor_id = u.id
       WHERE ${where}
       ORDER BY p.created_at DESC`,
      params
    );
    res.json(result.recordset);
  } catch (err) {
    console.error('getAllProducts:', err);
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── MODÉRATION ANNONCES ──────────────────────────────────────────
async function getAllAnnonces(req, res) {
  const { status } = req.query;
  let where = '1=1';
  const params = {};
  if (status) { where = 'a.status = @status'; params.status = status; }
  try {
    const result = await query(
      `SELECT a.*, CONCAT(u.first_name,' ',u.last_name) AS author_name, u.role AS author_role, u.email
       FROM annonces a JOIN users u ON a.user_id = u.id
       WHERE ${where} ORDER BY a.created_at DESC`,
      params
    );
    res.json(result.recordset);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

async function approveAnnonce(req, res) {
  try {
    const result = await query(
      `UPDATE annonces SET status='approved', rejection_reason=NULL, updated_at=GETDATE()
       OUTPUT INSERTED.id, INSERTED.title, INSERTED.status
       WHERE id = @id`,
      { id: parseInt(req.params.id) }
    );
    if (!result.recordset.length) return res.status(404).json({ message: 'Annonce introuvable.' });
    res.json({ message: 'Annonce approuvée.', annonce: result.recordset[0] });
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

async function rejectAnnonce(req, res) {
  const { reason = 'Non conforme aux règles de la plateforme.' } = req.body;
  try {
    const result = await query(
      `UPDATE annonces SET status='rejected', rejection_reason=@reason, updated_at=GETDATE()
       OUTPUT INSERTED.id, INSERTED.title, INSERTED.status
       WHERE id = @id`,
      { id: parseInt(req.params.id), reason }
    );
    if (!result.recordset.length) return res.status(404).json({ message: 'Annonce introuvable.' });
    res.json({ message: 'Annonce rejetée.', annonce: result.recordset[0] });
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── MODÉRATION PRODUITS ──────────────────────────────────────────
async function getPendingProducts(req, res) {
  try {
    const result = await query(
      `SELECT p.*, c.name AS category_name,
              CONCAT(u.first_name,' ',u.last_name) AS vendor_name, u.shop_name
       FROM products p
       LEFT JOIN categories c ON p.category_id = c.id
       LEFT JOIN users u ON p.vendor_id = u.id
       WHERE p.approval_status = 'pending'
       ORDER BY p.created_at DESC`
    );
    res.json(result.recordset);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

async function approveProduct(req, res) {
  try {
    const result = await query(
      `UPDATE products SET approval_status='approved', is_active=1
       OUTPUT INSERTED.id, INSERTED.name, INSERTED.approval_status
       WHERE id = @id`,
      { id: parseInt(req.params.id) }
    );
    if (!result.recordset.length) return res.status(404).json({ message: 'Produit introuvable.' });
    res.json({ message: 'Produit approuvé.', product: result.recordset[0] });
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

async function rejectProduct(req, res) {
  const { reason = 'Produit non conforme.' } = req.body;
  try {
    const result = await query(
      `UPDATE products SET approval_status='rejected', is_active=0
       OUTPUT INSERTED.id, INSERTED.name, INSERTED.approval_status
       WHERE id = @id`,
      { id: parseInt(req.params.id) }
    );
    if (!result.recordset.length) return res.status(404).json({ message: 'Produit introuvable.' });
    res.json({ message: 'Produit rejeté.', product: result.recordset[0] });
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

module.exports = {
  getPendingVendors, getAllVendors, approveVendor, rejectVendor, getAllUsers, getStats,
  getAllOrders, updateOrderStatus,
  getAllProducts,
  getAllAnnonces, approveAnnonce, rejectAnnonce,
  getPendingProducts, approveProduct, rejectProduct,
};
