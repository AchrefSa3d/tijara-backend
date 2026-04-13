const { query } = require('../config/database');

// GET /api/messages/conversations  — list my conversations
async function getConversations(req, res) {
  const userId = req.user.id;
  const role   = req.user.role;

  const field = role === 'vendor' ? 'c.vendor_id' : 'c.client_id';

  try {
    const result = await query(
      `SELECT c.id, c.product_id, c.product_name, c.updated_at,
              CONCAT(client.first_name, ' ', client.last_name) AS client_name,
              client.email AS client_email,
              CONCAT(vendor.first_name, ' ', vendor.last_name) AS vendor_name,
              vendor.email AS vendor_email,
              (SELECT TOP 1 content FROM messages WHERE conversation_id = c.id ORDER BY created_at DESC) AS last_message,
              (SELECT COUNT(*) FROM messages WHERE conversation_id = c.id AND is_read = 0 AND sender_id <> @userId) AS unread_count
       FROM conversations c
       JOIN users client ON c.client_id = client.id
       JOIN users vendor ON c.vendor_id = vendor.id
       WHERE ${field} = @userId
       ORDER BY c.updated_at DESC`,
      { userId }
    );
    res.json(result.recordset);
  } catch (err) {
    console.error(err);
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// GET /api/messages/conversations/:id  — get messages in conversation
async function getConversationMessages(req, res) {
  const convId = parseInt(req.params.id);
  const userId = req.user.id;

  try {
    // Verify user belongs to this conversation
    const conv = await query(
      `SELECT * FROM conversations WHERE id = @id AND (vendor_id = @uid OR client_id = @uid)`,
      { id: convId, uid: userId }
    );
    if (!conv.recordset.length)
      return res.status(403).json({ message: 'Accès refusé.' });

    // Mark messages as read
    await query(
      `UPDATE messages SET is_read = 1 WHERE conversation_id = @id AND sender_id <> @uid`,
      { id: convId, uid: userId }
    );

    const msgs = await query(
      `SELECT m.id, m.content, m.is_read, m.created_at, m.sender_id,
              CONCAT(u.first_name, ' ', u.last_name) AS sender_name
       FROM messages m
       JOIN users u ON m.sender_id = u.id
       WHERE m.conversation_id = @id
       ORDER BY m.created_at ASC`,
      { id: convId }
    );

    res.json({ conversation: conv.recordset[0], messages: msgs.recordset });
  } catch (err) {
    console.error(err);
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// POST /api/messages/conversations/:id  — send a message
async function sendMessage(req, res) {
  const convId  = parseInt(req.params.id);
  const userId  = req.user.id;
  const { content } = req.body;

  if (!content?.trim())
    return res.status(400).json({ message: 'Message vide.' });

  try {
    const conv = await query(
      `SELECT * FROM conversations WHERE id = @id AND (vendor_id = @uid OR client_id = @uid)`,
      { id: convId, uid: userId }
    );
    if (!conv.recordset.length)
      return res.status(403).json({ message: 'Accès refusé.' });

    const result = await query(
      `INSERT INTO messages (conversation_id, sender_id, content)
       OUTPUT INSERTED.*
       VALUES (@convId, @senderId, @content)`,
      { convId, senderId: userId, content: content.trim() }
    );

    // Update conversation updated_at
    await query(
      `UPDATE conversations SET updated_at = GETDATE() WHERE id = @id`,
      { id: convId }
    );

    res.status(201).json(result.recordset[0]);
  } catch (err) {
    console.error(err);
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// POST /api/messages/start  — start a new conversation (client -> vendor)
async function startConversation(req, res) {
  const { vendor_id, product_id, product_name, content } = req.body;
  const clientId = req.user.id;

  if (!vendor_id || !content?.trim())
    return res.status(400).json({ message: 'vendor_id et message requis.' });

  try {
    // Check if conversation already exists
    let conv = await query(
      `SELECT id FROM conversations
       WHERE vendor_id = @vendorId AND client_id = @clientId
         AND (product_id = @productId OR (@productId IS NULL AND product_id IS NULL))`,
      { vendorId: parseInt(vendor_id), clientId, productId: product_id ? parseInt(product_id) : null }
    );

    let convId;
    if (conv.recordset.length) {
      convId = conv.recordset[0].id;
    } else {
      const newConv = await query(
        `INSERT INTO conversations (vendor_id, client_id, product_id, product_name)
         OUTPUT INSERTED.*
         VALUES (@vendorId, @clientId, @productId, @productName)`,
        {
          vendorId:    parseInt(vendor_id),
          clientId,
          productId:   product_id ? parseInt(product_id) : null,
          productName: product_name || null,
        }
      );
      convId = newConv.recordset[0].id;
    }

    // Send first message
    await query(
      `INSERT INTO messages (conversation_id, sender_id, content) VALUES (@convId, @senderId, @content)`,
      { convId, senderId: clientId, content: content.trim() }
    );

    await query(`UPDATE conversations SET updated_at = GETDATE() WHERE id = @id`, { id: convId });

    res.status(201).json({ conversation_id: convId, message: 'Conversation démarrée.' });
  } catch (err) {
    console.error(err);
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

module.exports = { getConversations, getConversationMessages, sendMessage, startConversation };
