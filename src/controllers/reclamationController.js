const { query } = require('../config/database');

async function getAll(req, res) {
  try {
    let result;
    if (req.user.role === 'admin') {
      result = await query(
        `SELECT r.*, CONCAT(u.first_name, ' ', u.last_name) AS client_name, u.email, u.role AS user_role
         FROM reclamations r JOIN users u ON r.user_id = u.id
         ORDER BY r.created_at DESC`
      );
    } else {
      result = await query(
        `SELECT r.* FROM reclamations r WHERE r.user_id = @userId ORDER BY r.created_at DESC`,
        { userId: req.user.id }
      );
    }
    res.json(result.recordset);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

async function create(req, res) {
  const { subject, description } = req.body;
  if (!subject || !description)
    return res.status(400).json({ message: 'Sujet et description requis.' });

  try {
    const result = await query(
      `INSERT INTO reclamations (user_id, subject, description)
       OUTPUT INSERTED.*
       VALUES (@userId, @subject, @description)`,
      { userId: req.user.id, subject, description }
    );
    res.status(201).json(result.recordset[0]);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

async function reply(req, res) {
  const { status } = req.body;
  const valid = ['open', 'in_progress', 'resolved', 'closed'];
  if (status && !valid.includes(status))
    return res.status(400).json({ message: 'Statut invalide.' });

  try {
    const result = await query(
      `UPDATE reclamations
       SET status = COALESCE(@status, status),
           updated_at = GETDATE()
       OUTPUT INSERTED.*
       WHERE id = @id`,
      { status: status || null, id: parseInt(req.params.id) }
    );
    if (!result.recordset.length)
      return res.status(404).json({ message: 'Réclamation introuvable.' });
    res.json(result.recordset[0]);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

module.exports = { getAll, create, reply };
