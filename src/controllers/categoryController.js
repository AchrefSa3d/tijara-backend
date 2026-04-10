const { query } = require('../config/database');

async function getAll(req, res) {
  try {
    const result = await query(
      `SELECT c.*, COUNT(p.id) AS product_count
       FROM categories c
       LEFT JOIN products p ON p.category_id = c.id AND p.is_active = 1
       GROUP BY c.id, c.name, c.slug, c.icon, c.color, c.created_at
       ORDER BY c.name`
    );
    res.json(result.recordset);
  } catch (err) {
    console.error(err);
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

async function create(req, res) {
  const { name, icon, color } = req.body;
  if (!name) return res.status(400).json({ message: 'Nom requis.' });
  const slug = name.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '');
  try {
    const result = await query(
      `INSERT INTO categories (name, slug, icon, color)
       OUTPUT INSERTED.*
       VALUES (@name, @slug, @icon, @color)`,
      { name, slug, icon: icon || 'ri-tag-line', color: color || 'primary' }
    );
    res.status(201).json(result.recordset[0]);
  } catch (err) {
    if (err.number === 2627) // Violation de clé unique SQL Server
      return res.status(409).json({ message: 'Catégorie déjà existante.' });
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

module.exports = { getAll, create };
