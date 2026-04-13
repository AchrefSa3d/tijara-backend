const { query } = require('../config/database');

// GET /api/products/:id/reviews
async function getProductReviews(req, res) {
  const productId = parseInt(req.params.id);
  try {
    const result = await query(
      `SELECT r.id, r.rating, r.comment, r.created_at,
              CONCAT(u.first_name, ' ', u.last_name) AS author_name,
              u.id AS user_id
       FROM reviews r
       JOIN users u ON r.user_id = u.id
       WHERE r.product_id = @productId
       ORDER BY r.created_at DESC`,
      { productId }
    );

    const stats = await query(
      `SELECT COUNT(*) AS total,
              AVG(CAST(rating AS FLOAT)) AS avg_rating,
              SUM(CASE WHEN rating=5 THEN 1 ELSE 0 END) AS r5,
              SUM(CASE WHEN rating=4 THEN 1 ELSE 0 END) AS r4,
              SUM(CASE WHEN rating=3 THEN 1 ELSE 0 END) AS r3,
              SUM(CASE WHEN rating=2 THEN 1 ELSE 0 END) AS r2,
              SUM(CASE WHEN rating=1 THEN 1 ELSE 0 END) AS r1
       FROM reviews WHERE product_id = @productId`,
      { productId }
    );

    res.json({
      reviews: result.recordset,
      stats: stats.recordset[0]
    });
  } catch (err) {
    console.error(err);
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// POST /api/products/:id/reviews
async function addReview(req, res) {
  const productId = parseInt(req.params.id);
  const { rating, comment } = req.body;

  if (!rating || rating < 1 || rating > 5)
    return res.status(400).json({ message: 'La note doit être entre 1 et 5.' });

  // Check product exists and is active
  const prod = await query(
    `SELECT id FROM products WHERE id = @id AND is_active = 1`,
    { id: productId }
  );
  if (!prod.recordset.length)
    return res.status(404).json({ message: 'Produit introuvable.' });

  try {
    const result = await query(
      `MERGE reviews AS target
       USING (SELECT @productId AS product_id, @userId AS user_id) AS source
         ON target.product_id = source.product_id AND target.user_id = source.user_id
       WHEN MATCHED THEN
         UPDATE SET rating = @rating, comment = @comment, created_at = GETDATE()
       WHEN NOT MATCHED THEN
         INSERT (product_id, user_id, rating, comment)
         VALUES (@productId, @userId, @rating, @comment)
       OUTPUT INSERTED.*;`,
      {
        productId,
        userId:  req.user.id,
        rating:  parseInt(rating),
        comment: comment || null,
      }
    );
    res.status(201).json(result.recordset[0]);
  } catch (err) {
    console.error(err);
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// DELETE /api/products/:id/reviews/:reviewId
async function deleteReview(req, res) {
  try {
    await query(
      `DELETE FROM reviews WHERE id = @id AND user_id = @userId`,
      { id: parseInt(req.params.reviewId), userId: req.user.id }
    );
    res.json({ message: 'Avis supprimé.' });
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

module.exports = { getProductReviews, addReview, deleteReview };
