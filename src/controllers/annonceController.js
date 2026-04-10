const { query } = require('../config/database');

// ─── GET /api/annonces  (public: approved only) ───────────────────
async function getApproved(req, res) {
  const { type, page = 1, limit = 20 } = req.query;
  const offset = (page - 1) * limit;
  const params = { limit: parseInt(limit), offset: parseInt(offset) };
  let where = "a.status = 'approved'";
  if (type) { where += " AND a.type = @type"; params.type = type; }

  try {
    const result = await query(
      `SELECT a.*,
              CONCAT(u.first_name, ' ', u.last_name) AS author_name,
              u.role AS author_role,
              u.shop_name
       FROM annonces a JOIN users u ON a.user_id = u.id
       WHERE ${where}
       ORDER BY a.created_at DESC
       OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY`,
      params
    );
    res.json(result.recordset);
  } catch (err) {
    console.error(err);
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── GET /api/annonces/mine  (auth) ───────────────────────────────
async function getMine(req, res) {
  try {
    const result = await query(
      `SELECT a.*, CONCAT(u.first_name, ' ', u.last_name) AS author_name
       FROM annonces a JOIN users u ON a.user_id = u.id
       WHERE a.user_id = @userId
       ORDER BY a.created_at DESC`,
      { userId: req.user.id }
    );
    res.json(result.recordset);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── POST /api/annonces  (auth) ───────────────────────────────────
async function create(req, res) {
  const { title, content, image_url, type = 'annonce' } = req.body;
  if (!title || !content)
    return res.status(400).json({ message: 'Titre et contenu requis.' });

  // Seuls les vendeurs peuvent poster des deals
  if (type === 'deal' && req.user.role !== 'vendor' && req.user.role !== 'admin')
    return res.status(403).json({ message: 'Seuls les vendeurs peuvent publier des deals.' });

  try {
    const result = await query(
      `INSERT INTO annonces (user_id, title, content, image_url, type)
       OUTPUT INSERTED.*
       VALUES (@userId, @title, @content, @imageUrl, @type)`,
      { userId: req.user.id, title, content, imageUrl: image_url || null, type }
    );
    res.status(201).json({
      ...result.recordset[0],
      message: 'Annonce soumise. Elle sera visible après validation par l\'administrateur.'
    });
  } catch (err) {
    console.error(err);
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── DELETE /api/annonces/:id  (owner or admin) ───────────────────
async function remove(req, res) {
  try {
    const check = await query(
      'SELECT user_id FROM annonces WHERE id = @id',
      { id: parseInt(req.params.id) }
    );
    if (!check.recordset.length)
      return res.status(404).json({ message: 'Annonce introuvable.' });
    if (check.recordset[0].user_id !== req.user.id && req.user.role !== 'admin')
      return res.status(403).json({ message: 'Accès refusé.' });

    await query('DELETE FROM annonces WHERE id = @id', { id: parseInt(req.params.id) });
    res.json({ message: 'Annonce supprimée.' });
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── POST /api/annonces/:id/like  (auth, toggle) ──────────────────
async function toggleLike(req, res) {
  const annonceId = parseInt(req.params.id);
  try {
    const existing = await query(
      'SELECT id FROM annonce_reactions WHERE annonce_id=@aid AND user_id=@uid',
      { aid: annonceId, uid: req.user.id }
    );
    if (existing.recordset.length) {
      await query('DELETE FROM annonce_reactions WHERE annonce_id=@aid AND user_id=@uid',
        { aid: annonceId, uid: req.user.id });
      await query('UPDATE annonces SET likes_count = likes_count - 1 WHERE id=@id', { id: annonceId });
      return res.json({ liked: false });
    } else {
      await query('INSERT INTO annonce_reactions (annonce_id, user_id) VALUES (@aid, @uid)',
        { aid: annonceId, uid: req.user.id });
      await query('UPDATE annonces SET likes_count = likes_count + 1 WHERE id=@id', { id: annonceId });
      return res.json({ liked: true });
    }
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── GET /api/annonces/:id/comments ──────────────────────────────
async function getComments(req, res) {
  try {
    const result = await query(
      `SELECT c.*, CONCAT(u.first_name, ' ', u.last_name) AS author_name, u.role AS author_role
       FROM annonce_comments c JOIN users u ON c.user_id = u.id
       WHERE c.annonce_id = @id
       ORDER BY c.created_at ASC`,
      { id: parseInt(req.params.id) }
    );
    res.json(result.recordset);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── POST /api/annonces/:id/comments  (auth) ─────────────────────
async function addComment(req, res) {
  const { content } = req.body;
  if (!content?.trim())
    return res.status(400).json({ message: 'Commentaire vide.' });

  try {
    await query(
      'INSERT INTO annonce_comments (annonce_id, user_id, content) VALUES (@aid, @uid, @content)',
      { aid: parseInt(req.params.id), uid: req.user.id, content: content.trim() }
    );
    await query(
      'UPDATE annonces SET comments_count = comments_count + 1 WHERE id=@id',
      { id: parseInt(req.params.id) }
    );
    const result = await query(
      `SELECT c.*, CONCAT(u.first_name, ' ', u.last_name) AS author_name
       FROM annonce_comments c JOIN users u ON c.user_id = u.id
       WHERE c.annonce_id = @id ORDER BY c.created_at DESC OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY`,
      { id: parseInt(req.params.id) }
    );
    res.status(201).json(result.recordset[0]);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

module.exports = { getApproved, getMine, create, remove, toggleLike, getComments, addComment };
