const { getPool, query, sql } = require('../config/database');

// ─── GET /api/orders ──────────────────────────────────────────
async function getAll(req, res) {
  try {
    let result;
    if (req.user.role === 'admin' || req.user.role === 'vendor') {
      result = await query(
        `SELECT o.*, CONCAT(u.first_name, ' ', u.last_name) AS client_name, u.email
         FROM orders o JOIN users u ON o.user_id = u.id
         ORDER BY o.created_at DESC`
      );
    } else {
      result = await query(
        `SELECT o.* FROM orders o WHERE o.user_id = @userId ORDER BY o.created_at DESC`,
        { userId: req.user.id }
      );
    }
    res.json(result.recordset);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── GET /api/orders/:id ──────────────────────────────────────
async function getOne(req, res) {
  try {
    const result = await query(
      `SELECT o.*, CONCAT(u.first_name, ' ', u.last_name) AS client_name
       FROM orders o JOIN users u ON o.user_id = u.id
       WHERE o.id = @id`,
      { id: parseInt(req.params.id) }
    );
    if (!result.recordset.length)
      return res.status(404).json({ message: 'Commande introuvable.' });

    const order = result.recordset[0];
    if (req.user.role === 'user' && order.user_id !== req.user.id)
      return res.status(403).json({ message: 'Accès refusé.' });

    const items = await query(
      `SELECT oi.*, p.name AS product_name, p.image_url
       FROM order_items oi JOIN products p ON oi.product_id = p.id
       WHERE oi.order_id = @orderId`,
      { orderId: order.id }
    );

    res.json({ ...order, items: items.recordset });
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── POST /api/orders ─────────────────────────────────────────
async function create(req, res) {
  const { items, shipping_address, notes } = req.body;
  if (!items || !items.length)
    return res.status(400).json({ message: 'Panier vide.' });

  const pool = await getPool();
  const transaction = new sql.Transaction(pool);

  try {
    await transaction.begin();

    let total = 0;
    const enriched = [];

    for (const item of items) {
      const req2 = new sql.Request(transaction);
      req2.input('pid', item.product_id);
      const pResult = await req2.query(
        'SELECT id, price, stock, name FROM products WHERE id = @pid AND is_active = 1'
      );
      if (!pResult.recordset.length)
        throw new Error(`Produit #${item.product_id} introuvable.`);
      const p = pResult.recordset[0];
      if (p.stock < item.quantity)
        throw new Error(`Stock insuffisant pour "${p.name}".`);
      total += p.price * item.quantity;
      enriched.push({ ...item, unit_price: p.price });
    }

    // Créer la commande
    const orderReq = new sql.Request(transaction);
    orderReq.input('userId',   req.user.id);
    orderReq.input('total',    total);
    orderReq.input('address',  shipping_address || null);
    orderReq.input('notes',    notes || null);
    const orderResult = await orderReq.query(
      `INSERT INTO orders (user_id, total_amount, shipping_address, notes)
       OUTPUT INSERTED.*
       VALUES (@userId, @total, @address, @notes)`
    );
    const order = orderResult.recordset[0];

    // Insérer les lignes + décrémenter stock
    for (const item of enriched) {
      const itemReq = new sql.Request(transaction);
      itemReq.input('orderId',   order.id);
      itemReq.input('productId', item.product_id);
      itemReq.input('qty',       item.quantity);
      itemReq.input('price',     item.unit_price);
      await itemReq.query(
        'INSERT INTO order_items (order_id, product_id, quantity, unit_price) VALUES (@orderId, @productId, @qty, @price)'
      );

      const stockReq = new sql.Request(transaction);
      stockReq.input('qty', item.quantity);
      stockReq.input('pid', item.product_id);
      await stockReq.query('UPDATE products SET stock = stock - @qty WHERE id = @pid');
    }

    await transaction.commit();
    res.status(201).json({ ...order, message: 'Commande créée avec succès.' });
  } catch (err) {
    await transaction.rollback();
    res.status(400).json({ message: err.message });
  }
}

// ─── PATCH /api/orders/:id/status ────────────────────────────
async function updateStatus(req, res) {
  const { status } = req.body;
  const valid = ['pending', 'confirmed', 'shipped', 'delivered', 'cancelled'];
  if (!valid.includes(status))
    return res.status(400).json({ message: 'Statut invalide.' });

  try {
    const result = await query(
      `UPDATE orders SET status = @status
       OUTPUT INSERTED.*
       WHERE id = @id`,
      { status, id: parseInt(req.params.id) }
    );
    if (!result.recordset.length)
      return res.status(404).json({ message: 'Commande introuvable.' });
    res.json(result.recordset[0]);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

module.exports = { getAll, getOne, create, updateStatus };
