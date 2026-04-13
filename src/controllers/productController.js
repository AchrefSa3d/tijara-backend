const { getPool, query, sql } = require('../config/database');

// ─── GET /api/products ────────────────────────────────────────
async function getAll(req, res) {
  const { category, search, page = 1, limit = 12 } = req.query;
  const offset = (page - 1) * limit;

  let where = ['p.is_active = 1'];
  const params = {};

  if (category) {
    where.push('c.slug = @category');
    params.category = category;
  }
  if (search) {
    where.push('p.name LIKE @search');
    params.search = `%${search}%`;
  }

  const whereClause = where.join(' AND ');
  params.limit  = parseInt(limit);
  params.offset = parseInt(offset);

  try {
    const result = await query(
      `SELECT p.*, c.name AS category_name, c.slug AS category_slug,
              CONCAT(u.first_name, ' ', u.last_name) AS vendor_name,
              u.shop_name,
              (SELECT AVG(CAST(r.rating AS FLOAT)) FROM reviews r WHERE r.product_id = p.id) AS avg_rating,
              (SELECT COUNT(*) FROM reviews r WHERE r.product_id = p.id) AS review_count
       FROM products p
       LEFT JOIN categories c ON p.category_id = c.id
       LEFT JOIN users u ON p.vendor_id = u.id
       WHERE ${whereClause}
       ORDER BY p.created_at DESC
       OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY`,
      params
    );

    const countResult = await query(
      `SELECT COUNT(*) AS total FROM products p
       LEFT JOIN categories c ON p.category_id = c.id
       WHERE ${whereClause}`,
      params
    );

    res.json({
      data:  result.recordset,
      total: countResult.recordset[0].total,
      page:  +page,
      limit: +limit
    });
  } catch (err) {
    console.error(err);
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── GET /api/products/:id ────────────────────────────────────
async function getOne(req, res) {
  try {
    const result = await query(
      `SELECT p.*, c.name AS category_name,
              CONCAT(u.first_name, ' ', u.last_name) AS vendor_name
       FROM products p
       LEFT JOIN categories c ON p.category_id = c.id
       LEFT JOIN users u ON p.vendor_id = u.id
       WHERE p.id = @id`,
      { id: parseInt(req.params.id) }
    );
    if (!result.recordset.length)
      return res.status(404).json({ message: 'Produit introuvable.' });
    res.json(result.recordset[0]);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── POST /api/products  (vendor) ────────────────────────────
async function create(req, res) {
  const { name, description, price, stock, category_id, image_url } = req.body;
  if (!name || !price)
    return res.status(400).json({ message: 'Nom et prix requis.' });

  try {
    // Les produits des vendeurs nécessitent une validation admin
    const approvalStatus = req.user.role === 'admin' ? 'approved' : 'pending';
    const isActive       = req.user.role === 'admin' ? 1 : 0;

    const result = await query(
      `INSERT INTO products (vendor_id, category_id, name, description, price, stock, image_url, approval_status, is_active)
       OUTPUT INSERTED.*
       VALUES (@vendorId, @categoryId, @name, @description, @price, @stock, @imageUrl, @approvalStatus, @isActive)`,
      {
        vendorId:       req.user.id,
        categoryId:     category_id || null,
        name,
        description:    description || null,
        price:          parseFloat(price),
        stock:          parseInt(stock) || 0,
        imageUrl:       image_url || null,
        approvalStatus: approvalStatus,
        isActive:       isActive,
      }
    );
    res.status(201).json(result.recordset[0]);
  } catch (err) {
    console.error(err);
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── PUT /api/products/:id ────────────────────────────────────
async function update(req, res) {
  const fields = ['name', 'description', 'price', 'stock', 'category_id', 'image_url', 'is_active'];
  const updates = [];
  const params  = {};

  for (const f of fields) {
    if (req.body[f] !== undefined) {
      updates.push(`${f} = @${f}`);
      params[f] = req.body[f];
    }
  }
  if (!updates.length)
    return res.status(400).json({ message: 'Aucun champ à modifier.' });

  params.id       = parseInt(req.params.id);
  params.vendorId = req.user.id;

  try {
    const result = await query(
      `UPDATE products SET ${updates.join(', ')}
       OUTPUT INSERTED.*
       WHERE id = @id AND vendor_id = @vendorId`,
      params
    );
    if (!result.recordset.length)
      return res.status(404).json({ message: 'Produit introuvable ou accès refusé.' });
    res.json(result.recordset[0]);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── DELETE /api/products/:id ─────────────────────────────────
async function remove(req, res) {
  try {
    const pool = await getPool();
    const request = pool.request();
    request.input('id', parseInt(req.params.id));

    let whereClause = 'id = @id';
    if (req.user.role !== 'admin') {
      whereClause += ' AND vendor_id = @vendorId';
      request.input('vendorId', req.user.id);
    }

    const result = await request.query(
      `DELETE FROM products WHERE ${whereClause}`
    );
    if (!result.rowsAffected[0])
      return res.status(404).json({ message: 'Produit introuvable.' });
    res.json({ message: 'Produit supprimé.' });
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── GET /api/products/mine  (vendor) ────────────────────────
async function getMyProducts(req, res) {
  try {
    const result = await query(
      `SELECT p.*, c.name AS category_name, c.slug AS category_slug
       FROM products p
       LEFT JOIN categories c ON p.category_id = c.id
       WHERE p.vendor_id = @vendorId
       ORDER BY p.created_at DESC`,
      { vendorId: req.user.id }
    );
    res.json(result.recordset);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── GET /api/products/vendor/:vendorId  (public vendor profile) ─
async function getVendorProfile(req, res) {
  const vendorId = parseInt(req.params.vendorId);
  try {
    const vendor = await query(
      `SELECT id, first_name, last_name, email, phone, city,
              shop_name, company_number, is_approved, created_at
       FROM users WHERE id = @vendorId AND role = 'vendor' AND is_active = 1`,
      { vendorId }
    );
    if (!vendor.recordset.length)
      return res.status(404).json({ message: 'Vendeur introuvable.' });

    const products = await query(
      `SELECT p.id, p.name, p.price, p.stock, p.image_url, p.created_at,
              c.name AS category_name,
              (SELECT AVG(CAST(r.rating AS FLOAT)) FROM reviews r WHERE r.product_id = p.id) AS avg_rating,
              (SELECT COUNT(*) FROM reviews r WHERE r.product_id = p.id) AS review_count
       FROM products p
       LEFT JOIN categories c ON p.category_id = c.id
       WHERE p.vendor_id = @vendorId AND p.is_active = 1 AND p.approval_status = 'approved'
       ORDER BY p.created_at DESC`,
      { vendorId }
    );

    res.json({ vendor: vendor.recordset[0], products: products.recordset });
  } catch (err) {
    console.error(err);
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

module.exports = { getAll, getOne, create, update, remove, getMyProducts, getVendorProfile };
